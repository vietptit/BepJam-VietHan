using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace BepJam
{
    public class LevelLayout
    {
        public Column[] columns;
        public int seedUsed, solverNodes, candidates;
        public float randomFailRate, replayTime;
        public double generationMilliseconds;
        public int[] solution; // booster-free witness, also replayed through Kitchen
        public string replayFailure;
    }

    /// <summary>Exact stock, bounded difficulty search, six-plate DFS proof and real Kitchen replay.</summary>
    public static class LevelGen
    {
        public const int COLS = 4, VISIBLE = 4, RandomRuns = 200;
        public const float TapSeconds = 0.25f;
        const int ProofNodeBudget = 4000, CandidateLimit = 256, GenerationMilliseconds = 150;
        public static readonly string[] ReportHeader =
        {
            "BepJam difficulty: 200 uniform random stable-state playouts per candidate; no boosters.",
            "No level time limit. Careless-fail measures plate/stuck planning; VIP patience is checked by real Kitchen replay.",
            "Every selected layout has a six-plate DFS proof and a booster-free Kitchen replay including VIPs.",
            "Pantry: four columns, min(16, stock) visible crates; balanced column lengths (difference <= 2).",
            "Days 5-9 may be up to 10 percentage points below their unchanged target band; shortfalls are reported."
        };

        public static void TargetRange(LevelDef level, out float low, out float high)
        {
            if (level.boss) { low = 0.85f; high = 1f; }
            else if (level.number <= 4) { low = 0; high = 0.05f; }
            else if (level.number <= 9) { low = 0.15f; high = 0.35f; }
            else if (level.number <= 25) { low = 0.40f; high = 0.70f; }
            else { low = 0.70f; high = 0.90f; }
        }

        public static float TargetRate(LevelDef level)
        {
            if (level.boss) return 0.90f;
            int day = level.number;
            if (day <= 4) return 0;
            if (day <= 9) return 0.17f + (day - 5) * 0.04f;
            if (day <= 25) return 0.42f + (day - 10) * (0.26f / 15);
            return Math.Min(0.88f, 0.72f + (day - 26) * (0.16f / 19));
        }

        public static bool InTargetBand(LevelDef level, float rate)
        {
            TargetRange(level, out float low, out float high);
            return rate >= low - 0.0001f && rate <= high + 0.0001f;
        }

        public static bool AcceptableDifficulty(LevelDef level, float rate)
        {
            TargetRange(level, out float low, out float high);
            float margin = !level.boss && level.number >= 5 && level.number <= 9 ? 0.10f : 0;
            return rate >= low - margin - 0.0001f && rate <= high + 0.0001f;
        }

        // Separate validation sample, used by the harness's optional --audit pass.
        // This does not participate in candidate selection or generation timings.
        public static float MeasureCarelessFailRate(LevelDef level, LevelLayout layout, int runs, int seed)
        {
            if (runs <= 0) throw new ArgumentOutOfRangeException(nameof(runs));
            var solver = new Solver(level, layout.columns, null);
            var rng = new Random(seed);
            int failures = 0;
            for (int r = 0; r < runs; r++) if (!solver.RandomPlayout(rng)) failures++;
            return failures / (float)runs;
        }

        // Test-only watchdog: allow twice the serial action/animation work plus slack.
        // This never changes gameplay or difficulty; it bounds broken replay/test loops.
        public static float SimulationWatchdogSeconds(LevelDef level)
        {
            float seconds = 0.25f + (Math.Min(3, level.customers.Length) - 1) * 0.45f;
            seconds += Math.Max(0, level.customers.Length - 3) * (1.15f + 0.75f);
            foreach (var customer in level.customers)
                foreach (string id in customer)
                {
                    var dish = GameData.Dishes[id];
                    foreach (string part in dish.parts)
                    {
                        seconds += TapSeconds + Kitchen.FLY;
                        if (level.NeedsCut(part)) seconds += (part == "banh_mi" ? 0.32f : level.cutTime) + Kitchen.FLY;
                    }
                    seconds += Kitchen.GATHER + Kitchen.MERGE + Kitchen.DELIVER;
                    if (dish.station != StationType.None) seconds += Kitchen.TO_STATION + level.cookTime;
                }
            return Math.Max(30, seconds * 2 + 10);
        }

        public static LevelLayout Build(LevelDef level)
        {
            var clock = Stopwatch.StartNew();
            level.decoys = 0;
            // Retain a proven demand-order layout before spending time on difficulty.
            var best = Evaluate(level, Generate(level, -1, 0), -1, null);
            if (best == null) throw new InvalidOperationException("Cannot prove pantry solvable for day " + level.number);
            float target = TargetRate(level);
            TargetRange(level, out float low, out float high);
            float Distance(float rate) => Math.Abs(rate - target) + 10 * Math.Max(0, Math.Max(low - rate, rate - high));
            float bestDistance = Distance(best.randomFailRate);
            float disorder = 0;
            int tried = 1;
            for (int attempt = 0; attempt < CandidateLimit && clock.ElapsedMilliseconds < GenerationMilliseconds - 12; attempt++)
            {
                // With no hidden stock, every crate is selectable. Reordering cannot
                // change uniform random difficulty; searching would only fit sampling noise.
                if (bestDistance <= 0.01f || best.columns.All(c => c.hidden.Count == 0)) break;
                int seed = unchecked(level.seed * 1000 + attempt);
                // Explore as well as calibrate: shared recipes can make difficulty non-monotonic.
                bool explore = attempt > 0 && attempt % 4 < 2;
                float candidateDisorder = explore ? attempt % 16 : disorder;
                var candidate = Evaluate(level, Generate(level, seed, candidateDisorder), seed, clock);
                tried++;
                if (candidate == null) { disorder = Math.Max(0, disorder - 0.2f); continue; }
                float distance = Distance(candidate.randomFailRate);
                if (distance < bestDistance) { best = candidate; bestDistance = distance; }
                // Calibrate disorder using measured failures, rather than assuming random = hard.
                if (!explore) disorder = Math.Max(0, Math.Min(32, disorder + (target - candidate.randomFailRate) * 8));
            }
            best.candidates = tried;
            best.generationMilliseconds = clock.Elapsed.TotalMilliseconds;
            return best;
        }

        static LevelLayout Evaluate(LevelDef level, Column[] cols, int seed, Stopwatch clock)
        {
            var solver = new Solver(level, cols, clock);
            if (!solver.Solve()) return null;
            var layout = new LevelLayout { columns = cols, seedUsed = seed, solverNodes = solver.nodes, solution = solver.Plan };
            if (!ReplayPlan(level, layout, out layout.replayTime))
            {
                return null;
            }
            var rng = new Random(unchecked(seed ^ level.seed ^ 0x5bd1e995));
            int failures = 0;
            for (int r = 0; r < RandomRuns; r++)
            {
                if (clock != null && (r & 7) == 0 && clock.ElapsedMilliseconds >= GenerationMilliseconds) return null;
                if (!solver.RandomPlayout(rng)) failures++;
            }
            layout.randomFailRate = failures / (float)RandomRuns;
            return layout;
        }

        static Column[] Generate(LevelDef level, int seed, float disorder)
        {
            var rng = new Random(seed < 0 ? level.seed : seed);
            var units = new List<string>();
            var orders = level.customers.Select(c => c.ToList()).ToArray();
            int next = Math.Min(3, orders.Length);
            // VIPs first, then customer id / largest recipe, matching real gather priority.
            // Equal starting patience makes earlier VIP arrivals the most impatient.
            while (orders.Any(c => c.Count > 0))
            {
                int target = Enumerable.Range(0, next).Where(c => orders[c].Count > 0)
                    .OrderByDescending(c => level.vip != null && c < level.vip.Length && level.vip[c]).ThenBy(c => c).First();
                string dish = orders[target].OrderByDescending(d => GameData.Dishes[d].parts.Length).First();
                units.AddRange(GameData.Dishes[dish].parts);
                orders[target].Remove(dish);
                if (orders[target].Count == 0 && next < orders.Length) next++;
            }
            var cols = new Column[COLS];
            var queues = new List<Crate>[COLS];
            for (int c = 0; c < COLS; c++) { cols[c] = new Column(); queues[c] = new List<Crate>(); }
            // Fill rows first, then queue the surplus evenly behind all four columns.
            // Only ingredient order varies between candidates; visibility never does.
            var sequence = Enumerable.Range(0, units.Count)
                .OrderBy(i => i + (seed < 0 ? 0 : rng.NextDouble() * disorder)).ToArray();
            var lanes = Enumerable.Range(0, COLS).ToArray();
            for (int i = 0; i < sequence.Length; i++)
            {
                if (seed >= 0 && i % COLS == 0)
                    for (int j = COLS - 1; j > 0; j--)
                    { int k = rng.Next(j + 1); int swap = lanes[j]; lanes[j] = lanes[k]; lanes[k] = swap; }
                int id = sequence[i];
                queues[lanes[i % COLS]].Add(new Crate { id = id, ing = units[id], count = 1 });
            }
            for (int c = 0; c < COLS; c++)
            {
                cols[c].visible.AddRange(queues[c].Take(VISIBLE));
                cols[c].hidden.AddRange(queues[c].Skip(VISIBLE));
            }
            return cols;
        }

        public static Column[] Clone(Column[] src)
        {
            var result = new Column[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                result[i] = new Column();
                foreach (var c in src[i].visible) result[i].visible.Add(new Crate { id = c.id, ing = c.ing, count = c.count });
                foreach (var c in src[i].hidden) result[i].hidden.Add(new Crate { id = c.id, ing = c.ing, count = c.count });
            }
            return result;
        }

        /// <summary>Replay the DFS witness using real cuts, station locks, arrivals and VIP
        /// patience. No boosters. Catches timing and order reservation mismatches in the abstraction.</summary>
        public static bool ReplayPlan(LevelDef level, LevelLayout layout, out float elapsed)
        {
            var kitchen = new Kitchen(level, new LevelLayout { columns = Clone(layout.columns) });
            kitchen.Start();
            int step = 0;
            float nextTap = 1.2f;
            float watchdog = SimulationWatchdogSeconds(level);
            while (!kitchen.ended && kitchen.time < watchdog)
            {
                if (step < layout.solution.Length && kitchen.time >= nextTap)
                {
                    int id = layout.solution[step];
                    var crate = kitchen.VisibleCrates.FirstOrDefault(c => c.id == id);
                    if (crate != null && kitchen.stations[StationType.Board].state == StationState.Idle &&
                        kitchen.CanPlace(crate.ing) && kitchen.Tap(id))
                    { step++; nextTap = kitchen.time + TapSeconds; }
                }
                kitchen.Update(0.05f);
                kitchen.events.Clear();
            }
            elapsed = kitchen.time;
            layout.replayFailure = kitchen.won ? null : kitchen.loseKind + " step=" + step + " time=" + elapsed +
                " plates=" + string.Join(",", kitchen.slots.Select(s => s.key)) + " seats=" + string.Join(",", kitchen.seats.Select(c => c == null ? "-" : c.id + ":" + c.vip + ":" + c.patience.ToString("F1")));
            return kitchen.won && step == layout.solution.Length;
        }

        public static string ReportLine(LevelDef level, LevelLayout layout)
        {
            int dishes = level.customers.Sum(c => c.Length);
            int parts = level.customers.Sum(c => c.Sum(d => GameData.Dishes[d].parts.Length));
            int visible = layout.columns.Sum(c => c.visible.Count);
            int hidden = layout.columns.Sum(c => c.hidden.Count);
            int maxHidden = layout.columns.Max(c => c.hidden.Count);
            TargetRange(level, out float low, out float high);
            string note = "";
            if (!InTargetBand(level, layout.randomFailRate))
                note = layout.randomFailRate < low ? string.Format(CultureInfo.InvariantCulture,
                    " (below target by {0:F1}pp; {1})", (low - layout.randomFailRate) * 100,
                    AcceptableDifficulty(level, layout.randomFailRate) ? "allowed early shortfall" : "outside target") : " (outside target)";
            return string.Format(CultureInfo.InvariantCulture,
                "day {0:00}{1}: dishes={2}, parts={3}, visible={4}, hidden={5}, hiddenPerColumn max={6}, careless-fail={7:F1}%{8}, target={9:F0}-{10:F0}%, seed={11}, gen={12:F1}ms",
                level.number, level.boss ? " BOSS" : "", dishes, parts, visible, hidden, maxHidden,
                layout.randomFailRate * 100, note, low * 100, high * 100, layout.seedUsed, layout.generationMilliseconds);
        }

        public static string ReportSummary(int count, int inBand, int earlyShortfalls, double maxGeneration)
            => string.Format(CultureInfo.InvariantCulture,
                "Summary: {0} proven levels; {1} in target band; {2} allowed early shortfalls; {3} outside allowance; max generation={4:F1}ms.",
                count, inBand, earlyShortfalls, count - inBand - earlyShortfalls, maxGeneration);

        /// <summary>Stable-state model: one portion per plate, only seated orders gather, and
        /// consuming a visible crate reveals the next in its column. Integer state makes 200
        /// uniform random playouts cheap. ReplayPlan checks actual real-time solvability.</summary>
        sealed class Solver
        {
            readonly LevelDef level;
            readonly Stopwatch clock;
            readonly int[] counts, initialCounts, ingredients, ids, held, remaining, capacity, customerPriority;
            readonly int[][] columns, orderPriority;
            readonly int[][][] recipes;
            readonly HashSet<string> dead = new HashSet<string>();
            readonly List<int> path = new List<int>();
            int used, nextCustomer, finished;
            public int nodes;
            public int[] Plan => path.ToArray();

            public Solver(LevelDef level, Column[] source, Stopwatch clock)
            {
                this.level = level; this.clock = clock;
                var index = GameData.Ingredients.Keys.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
                var crates = source.SelectMany(c => c.visible.Concat(c.hidden)).ToArray();
                counts = crates.Select(c => c.count).ToArray(); initialCounts = (int[])counts.Clone();
                ingredients = crates.Select(c => index[c.ing]).ToArray(); ids = crates.Select(c => c.id).ToArray();
                columns = new int[source.Length][]; capacity = new int[source.Length];
                int offset = 0;
                for (int c = 0; c < source.Length; c++)
                {
                    capacity[c] = source[c].visible.Count;
                    int length = source[c].visible.Count + source[c].hidden.Count;
                    columns[c] = Enumerable.Range(offset, length).ToArray(); offset += length;
                }
                held = new int[index.Count]; remaining = new int[level.customers.Length];
                recipes = level.customers.Select(c => c.Select(d => GameData.Dishes[d].parts.Select(p => index[p]).ToArray()).ToArray()).ToArray();
                orderPriority = recipes.Select(c => Enumerable.Range(0, c.Length).OrderByDescending(i => c[i].Length).ToArray()).ToArray();
                customerPriority = Enumerable.Range(0, remaining.Length)
                    .OrderByDescending(c => level.vip != null && c < level.vip.Length && level.vip[c]).ThenBy(c => c).ToArray();
                Reset();
            }

            void Reset()
            {
                Array.Copy(initialCounts, counts, counts.Length); Array.Clear(held, 0, held.Length);
                for (int c = 0; c < remaining.Length; c++) remaining[c] = (1 << recipes[c].Length) - 1;
                used = finished = 0; nextCustomer = Math.Min(3, remaining.Length);
            }

            bool Won => finished == remaining.Length && used == 0 && counts.All(n => n == 0);

            int Moves(int[] moves)
            {
                if (used >= level.plates) return 0;
                int n = 0;
                for (int c = 0; c < columns.Length; c++)
                {
                    int visible = 0;
                    foreach (int i in columns[c])
                    {
                        if (counts[i] == 0) continue;
                        if (visible++ == capacity[c]) break;
                        moves[n++] = i;
                    }
                }
                return n;
            }

            bool HasRecipe(int[] recipe)
            {
                for (int p = 0; p < recipe.Length; p++)
                {
                    int need = 1;
                    for (int j = 0; j < p; j++) if (recipe[j] == recipe[p]) need++;
                    if (held[recipe[p]] < need) return false;
                }
                return true;
            }

            void Apply(int move)
            {
                counts[move]--; held[ingredients[move]]++; used++;
                // Same VIP / customer-id / largest-recipe / original-order priority as FindGather.
                // Stable states omit time; earlier VIP arrivals have lower patience at equal rates.
                bool gathered;
                do
                {
                    gathered = false;
                    foreach (int c in customerPriority)
                    {
                        if (gathered) break;
                        if (c >= nextCustomer) continue;
                        foreach (int o in orderPriority[c])
                        {
                            if ((remaining[c] & (1 << o)) == 0 || !HasRecipe(recipes[c][o])) continue;
                            foreach (int p in recipes[c][o]) { held[p]--; used--; }
                            remaining[c] &= ~(1 << o);
                            if (remaining[c] == 0) { finished++; if (nextCustomer < remaining.Length) nextCustomer++; }
                            gathered = true; break;
                        }
                    }
                } while (gathered);
            }

            int Score(int move)
            {
                int ingredient = ingredients[move], best = -10000;
                for (int c = 0; c < nextCustomer; c++)
                    foreach (int o in orderPriority[c])
                    {
                        if ((remaining[c] & (1 << o)) == 0) continue;
                        var recipe = recipes[c][o];
                        int missing = 0, wanted = 0;
                        for (int p = 0; p < recipe.Length; p++)
                        {
                            int occurrence = 1;
                            for (int j = 0; j < p; j++) if (recipe[j] == recipe[p]) occurrence++;
                            if (held[recipe[p]] >= occurrence) continue;
                            missing++; if (recipe[p] == ingredient) wanted++;
                        }
                        if (wanted == 0) continue;
                        int vip = level.vip != null && c < level.vip.Length && level.vip[c] ? 5000 : 0;
                        best = Math.Max(best, vip + (missing == 1 ? 1000 : 800 - missing * 100) - c);
                    }
                return best;
            }

            string Key()
            {
                // Fixed-width sections; plate permutations are equivalent for whole-set gathers.
                var key = new char[counts.Length + held.Length + remaining.Length + 1];
                int i = 0;
                foreach (int n in counts) key[i++] = (char)n;
                foreach (int n in held) key[i++] = (char)n;
                foreach (int n in remaining) key[i++] = (char)n;
                key[i] = (char)nextCustomer;
                return new string(key);
            }

            int[] Save()
            {
                var state = new int[counts.Length + held.Length + remaining.Length + 3];
                Array.Copy(counts, state, counts.Length);
                Array.Copy(held, 0, state, counts.Length, held.Length);
                Array.Copy(remaining, 0, state, counts.Length + held.Length, remaining.Length);
                int end = state.Length; state[end - 3] = used; state[end - 2] = nextCustomer; state[end - 1] = finished;
                return state;
            }

            void Restore(int[] state)
            {
                Array.Copy(state, counts, counts.Length);
                Array.Copy(state, counts.Length, held, 0, held.Length);
                Array.Copy(state, counts.Length + held.Length, remaining, 0, remaining.Length);
                int end = state.Length; used = state[end - 3]; nextCustomer = state[end - 2]; finished = state[end - 1];
            }

            public bool Solve()
            {
                if (Won) return true;
                if (nodes >= ProofNodeBudget || (clock != null && clock.ElapsedMilliseconds >= GenerationMilliseconds)) return false;
                nodes++;
                string key = Key();
                if (dead.Contains(key)) return false;
                var moves = new int[COLS * VISIBLE];
                int n = Moves(moves);
                // Only DFS uses a heuristic. RandomPlayout samples uniformly without sorting.
                var scores = new int[n];
                for (int i = 0; i < n; i++) scores[i] = -Score(moves[i]);
                Array.Sort(scores, moves, 0, n);
                var state = Save();
                for (int i = 0; i < n; i++)
                {
                    Apply(moves[i]); path.Add(ids[moves[i]]);
                    if (Solve()) return true;
                    path.RemoveAt(path.Count - 1); Restore(state);
                    if (nodes >= ProofNodeBudget) return false;
                }
                dead.Add(key);
                return false;
            }

            public bool RandomPlayout(Random rng)
            {
                Reset();
                var moves = new int[COLS * VISIBLE];
                while (!Won)
                {
                    int n = Moves(moves);
                    if (n == 0) return false;
                    Apply(moves[rng.Next(n)]);
                }
                return true;
            }
        }
    }
}
