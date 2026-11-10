using System;
using System.Collections.Generic;
using System.Linq;

namespace BepJam
{
    // ---------------------------------------------------------------- model types
    public class Crate { public int id; public string ing; public int count; }
    public class Column { public readonly List<Crate> visible = new List<Crate>(); public readonly List<Crate> hidden = new List<Crate>(); }
    public enum SlotState { Empty, Incoming, Ingredient, Merging, Dish, Outgoing }
    public class Slot
    {
        public int index; public SlotState state; public string key; public float timer; public Order order;
        public readonly List<string> items = new List<string>(); // zero or one ingredient; dishes use key/order
        public bool uncut;
        public long cutQueueOrder;
    }
    public class Order { public Customer customer; public int index; public string dish; public bool reserved, done; }
    public class Customer
    {
        public int id; public string charId; public readonly List<Order> orders = new List<Order>();
        public int seat = -1; public bool arrived, finished;
        public bool vip; public float patience, patienceMax;
        public bool AllDone => orders.All(o => o.done);
    }
    public enum StationState { Idle, Receiving, Cooking, Waiting, Outgoing }
    public class Station { public StationType type; public StationState state; public string dish; public Order order; public float timer, total; }

    public enum EvType
    {
        CrateTap, Blocked, Fly, Land, Merge, ToStation, CookStart, CookDone, WaitStation, Deliver, Delivered,
        CustomerArrive, CustomerHappy, CustomerLeave, CrateEmptied, CrateShift, CrateReveal, CrateAdd, Chain,
        Return, Stuck, Win, Lose, Hint, Stack, CutStart, CutDone, VipLeave, Gather
    }
    public enum Src { Crate, Slot, Station }

    public class Ev
    {
        public EvType type; public Src src;
        public int crateId = -1, col = -1, row = -1, slot = -1, toSlot = -1, value, value2;
        public string key, key2, text;
        public int[] slots;
        public string[] keys;
        public StationType station;
        public bool uncut;
        public Customer customer; public Order order; public Crate crate;
    }

    /// <summary>Pure gameplay model. Time-driven: every state change that needs an animation is held by a timer
    /// that matches the animation length, so the view can simply play an animation per event.</summary>
    public class Kitchen
    {
        public const float FLY = 0.42f, GATHER = 0.35f, MERGE = 1.15f, TO_STATION = 0.45f, DELIVER = 0.62f, CHAIN_WINDOW = 4.5f;

        public readonly LevelDef level;
        public readonly Column[] columns;
        public readonly Slot[] slots;
        public readonly Dictionary<StationType, Station> stations = new Dictionary<StationType, Station>();
        public readonly List<Customer> customers = new List<Customer>();
        public readonly Customer[] seats;
        public readonly Queue<Ev> events = new Queue<Ev>();

        public float time; // elapsed simulation time for animations and VIP patience
        public bool started, paused, ended, won;
        public int served, coins, chain, bestChain, moves, merges, returnsLeft = 3, hintsLeft = 3;
        public string loseReason, loseKind;
        public bool CanRevive => ended && !won && loseKind == "stuck" && returnsLeft > 0;
        float lastMerge = -99f;
        int nextCustomer, pendingArrivals, returnCrateId = 100000;
        long nextCutQueueOrder;
        readonly List<(float t, Action a)> scheduled = new List<(float, Action)>();
        readonly HashSet<Order> delivering = new HashSet<Order>();
        static readonly string[] CharIds = { "c_vn_woman", "c_kr_man", "c_vn_grandpa", "c_kr_girl", "c_vn_boy", "c_kr_woman" };

        public Kitchen(LevelDef level, LevelLayout layout)
        {
            this.level = level;
            columns = layout.columns;
            slots = new Slot[level.plates];
            for (int i = 0; i < slots.Length; i++) slots[i] = new Slot { index = i };
            stations[StationType.Stove] = new Station { type = StationType.Stove };
            stations[StationType.Oven] = new Station { type = StationType.Oven };
            stations[StationType.Board] = new Station { type = StationType.Board };
            var rng = new Random(level.seed * 7 + 3);
            int charStart = rng.Next(CharIds.Length);
            for (int c = 0; c < level.customers.Length; c++)
            {
                var cu = new Customer { id = c, charId = CharIds[(charStart + c) % CharIds.Length],
                    vip = level.vip != null && c < level.vip.Length && level.vip[c],
                    patience = level.vipPatience, patienceMax = level.vipPatience };
                for (int k = 0; k < level.customers[c].Length; k++) cu.orders.Add(new Order { customer = cu, index = k, dish = level.customers[c][k] });
                customers.Add(cu);
            }
            seats = new Customer[Math.Min(3, customers.Count)];
        }

        public IEnumerable<Crate> VisibleCrates => columns.SelectMany(c => c.visible);
        public int HiddenCount(int col) => columns[col].hidden.Count;
        public int TotalServedTarget => customers.Count;

        void Emit(Ev e) => events.Enqueue(e);
        void Schedule(float delay, Action a) => scheduled.Add((time + delay, a));

        // ---------------------------------------------------------------- lifecycle
        public void Start()
        {
            started = true;
            for (int s = 0; s < seats.Length; s++) { int seat = s; pendingArrivals++; Schedule(0.25f + s * 0.45f, () => SeatNext(seat)); }
        }

        void SeatNext(int seat)
        {
            pendingArrivals--;
            if (ended || nextCustomer >= customers.Count || seats[seat] != null) return;
            var cu = customers[nextCustomer++];
            cu.seat = seat; cu.arrived = true; seats[seat] = cu;
            Emit(new Ev { type = EvType.CustomerArrive, customer = cu, value = seat });
        }

        public void Update(float dt)
        {
            if (!started || paused || ended) return;
            time += dt;
            foreach (var cu in seats)
                if (cu != null && cu.vip && !cu.finished)
                {
                    cu.patience = Math.Max(0, cu.patience - dt);
                    if (cu.patience <= 0)
                    {
                        Emit(new Ev { type = EvType.VipLeave, customer = cu, value = cu.seat });
                        Lose("Khách VIP đã bỏ đi!", "vip"); return;
                    }
                }
            // scheduled callbacks (may schedule more)
            for (int guard = 0; guard < 64; guard++)
            {
                int idx = scheduled.FindIndex(s => s.t <= time);
                if (idx < 0) break;
                var a = scheduled[idx].a; scheduled.RemoveAt(idx); a();
                if (ended) return;
            }
            foreach (var s in PrioritizedDishes(slots)) TickSlot(s, dt);
            foreach (var st in stations.Values) TickStation(st, dt);
            DispatchWaitingCuts();
            DispatchWaitingDishes();
            CombineWaiting();
            if (chain > 0 && time - lastMerge > CHAIN_WINDOW) { chain = 0; Emit(new Ev { type = EvType.Chain, value = 0 }); }
            CheckStuck();
        }

        void TickSlot(Slot s, float dt)
        {
            if (s.timer <= 0) return;
            s.timer -= dt;
            if (s.timer > 0) return;
            s.timer = 0;
            switch (s.state)
            {
                case SlotState.Incoming:
                    s.state = SlotState.Ingredient;
                    Emit(new Ev { type = EvType.Land, slot = s.index, key = s.key, uncut = s.uncut });
                    break;
                case SlotState.Merging:
                    s.state = SlotState.Dish;
                    DishReady(s);
                    break;
                case SlotState.Outgoing:
                    ClearSlot(s);
                    break;
            }
        }

        void TickStation(Station st, float dt)
        {
            if (st.state == StationState.Idle) return;
            if (st.type == StationType.Board) { TickBoard(st, dt); return; }
            st.timer -= dt;
            if (st.timer > 0) return;
            switch (st.state)
            {
                case StationState.Receiving:
                    st.state = StationState.Cooking; st.total = st.timer = level.cookTime;
                    Emit(new Ev { type = EvType.CookStart, station = st.type, key = st.dish, value = (int)(level.cookTime * 1000) });
                    break;
                case StationState.Cooking:
                    st.state = StationState.Outgoing; st.timer = DELIVER;
                    Emit(new Ev { type = EvType.CookDone, station = st.type, key = st.dish });
                    SendToCustomer(Src.Station, -1, st.type, st.dish, st.order);
                    break;
                case StationState.Outgoing:
                    st.state = StationState.Idle; st.dish = null; st.order = null; st.timer = 0;
                    break;
            }
        }

        void TickBoard(Station st, float dt)
        {
            st.timer -= dt;
            if (st.timer > 0) return;
            if (st.state == StationState.Receiving)
            {
                float cutT = st.dish == "banh_mi" ? 0.32f : level.cutTime;   // bread: one quick slash, then straight to a plate
                st.state = StationState.Cooking; st.total = st.timer = cutT;
                Emit(new Ev { type = EvType.CutStart, station = st.type, key = st.dish, value = (int)(cutT * 1000) });
            }
            else if (st.state == StationState.Cooking)
            {
                st.state = StationState.Waiting; st.timer = 0;
                Emit(new Ev { type = EvType.CutDone, station = st.type, key = st.dish });
            }
            else if (st.state == StationState.Outgoing)
            {
                st.state = StationState.Idle; st.dish = null; st.timer = 0;
            }
            if (st.state == StationState.Waiting && PlaceIngredient(st.dish, Src.Station, station: StationType.Board))
            { st.state = StationState.Outgoing; st.timer = FLY; }
        }

        // ---------------------------------------------------------------- player actions
        // Empty gather sources stay locked until their icons have left the counter.
        static bool Available(Slot s) => s.state == SlotState.Empty && s.timer <= 0;
        public bool CanPlace(string ing) => slots.Any(Available) ||
            (level.NeedsCut(ing) && stations[StationType.Board].state == StationState.Idle);

        public bool Tap(int crateId)
        {
            if (!started || paused || ended) return false;
            int ci = -1; Crate crate = null;
            for (int c = 0; c < columns.Length && crate == null; c++)
                foreach (var cr in columns[c].visible) if (cr.id == crateId) { crate = cr; ci = c; break; }
            if (crate == null) return false;
            string k = crate.ing;
            if (level.NeedsCut(k))
            {
                var board = stations[StationType.Board];
                if (board.state != StationState.Idle)
                {
                    if (!PlaceIngredient(k, Src.Crate, crate.id, ci, uncut: true))
                    {
                        Emit(new Ev { type = EvType.Blocked, crateId = crate.id, col = ci, text = "Hết đĩa trống!" });
                        return false;
                    }
                }
                else
                {
                    board.state = StationState.Receiving; board.timer = FLY; board.dish = k;
                    Emit(new Ev { type = EvType.ToStation, src = Src.Crate, crateId = crate.id, col = ci, station = StationType.Board, key = k });
                }
            }
            else if (!PlaceIngredient(k, Src.Crate, crate.id, ci))
            {
                Emit(new Ev { type = EvType.Blocked, crateId = crate.id, col = ci, text = "Hết đĩa trống!" });
                return false;
            }
            moves++;
            crate.count--;
            Emit(new Ev { type = EvType.CrateTap, crateId = crate.id, col = ci, value = crate.count, crate = crate });
            if (crate.count <= 0) RemoveCrate(ci, crate);
            return true;
        }

        // The board's finished ingredient follows exactly the same placement path as a crate tap.
        bool PlaceIngredient(string k, Src src, int crateId = -1, int col = -1, StationType station = StationType.None, bool uncut = false)
        {
            var target = slots.FirstOrDefault(Available);
            if (target == null) return false;
            ClearSlot(target);
            target.uncut = uncut;
            target.cutQueueOrder = uncut ? nextCutQueueOrder++ : 0;
            target.items.Add(k);
            target.key = k;
            target.state = SlotState.Incoming;
            target.timer = FLY;
            Emit(new Ev { type = EvType.Fly, src = src, crateId = crateId, col = col, station = station,
                toSlot = target.index, key = k, uncut = uncut });
            return true;
        }

        void RemoveCrate(int ci, Crate crate)
        {
            var col = columns[ci];
            int row = col.visible.IndexOf(crate);
            col.visible.RemoveAt(row);
            Emit(new Ev { type = EvType.CrateEmptied, crateId = crate.id, col = ci, row = row });
            for (int r = row; r < col.visible.Count; r++)
                Emit(new Ev { type = EvType.CrateShift, crateId = col.visible[r].id, col = ci, row = r });
            if (col.hidden.Count > 0)
            {
                var nc = col.hidden[0]; col.hidden.RemoveAt(0); col.visible.Add(nc);
                Emit(new Ev { type = EvType.CrateReveal, crateId = nc.id, col = ci, row = col.visible.Count - 1, crate = nc, value = col.hidden.Count });
            }
        }

        void DoMergeFx(int slotIndex, string a, string b, string dish)
        {
            merges++;
            chain = time - lastMerge <= CHAIN_WINDOW ? chain + 1 : 1;
            lastMerge = time;
            bestChain = Math.Max(bestChain, chain);
            Emit(new Ev { type = EvType.Merge, slot = slotIndex, key = dish, text = a + "|" + b, value = chain });
            Emit(new Ev { type = EvType.Chain, value = chain });
        }

        public int Hint(bool emit = true)
        {
            if (ended || !started) return -1;
            var orders = SeatedOpenOrders().ToList();
            int best = -1;
            foreach (var cu in seats.Where(c => c != null && c.vip && !c.finished)
                .OrderBy(c => c.patience).ThenBy(c => c.id))
            {
                var needed = orders.Where(o => o.customer == cu).Select(o => GameData.Dishes[o.dish])
                    .Where(CanSupply).ToList();
                best = FindHint(needed);
                if (best >= 0) break;
            }
            if (best < 0) best = FindHint(orders.Select(o => GameData.Dishes[o.dish]).ToList());
            if (best >= 0 && emit) Emit(new Ev { type = EvType.Hint, crateId = best });
            return best;
        }

        List<string> MissingParts(DishDef dish)
        {
            var missing = new List<string>(dish.parts);
            foreach (var s in slots)
                if (s.state == SlotState.Ingredient || s.state == SlotState.Incoming)
                    missing.Remove(s.key);
            var board = stations[StationType.Board];
            if (board.state != StationState.Idle && board.state != StationState.Outgoing) missing.Remove(board.dish);
            return missing;
        }

        bool CanSupply(DishDef dish)
        {
            var missing = MissingParts(dish);
            foreach (var cr in VisibleCrates)
                for (int n = 0; n < cr.count && missing.Remove(cr.ing); n++) { }
            return missing.Count == 0;
        }

        int FindHint(List<DishDef> needed)
        {
            int best = -1, bestScore = -1;
            foreach (var c in columns)
                foreach (var cr in c.visible)
                {
                    if (!CanPlace(cr.ing)) continue;
                    // Reserve plate space for the guests currently seated.
                    if (!needed.Any(d => d.parts.Contains(cr.ing))) continue;
                    int score = -1;
                    foreach (var d in needed)
                    {
                        // Include in-flight and queued cuts so rapid hints do not suggest surplus copies.
                        var missing = MissingParts(d);
                        if (!missing.Contains(cr.ing)) continue;
                        int value = missing.Count == 1 ? 200 : 100 - missing.Count * 10;
                        score = Math.Max(score, value);
                    }
                    if (score < 0) continue;
                    if (c.visible.IndexOf(cr) == 0) score += 1;   // tie-break: front crate
                    if (score > bestScore) { bestScore = score; best = cr.id; }
                }
            return bestScore > 0 ? best : -1;
        }

        /// <summary>Booster: return the single ingredient on the last waiting plate.</summary>
        public bool ReturnPlate()
        {
            if (ended || returnsLeft <= 0) return false;
            var s = slots.LastOrDefault(x => x.state == SlotState.Ingredient);
            if (s == null) return false;
            ReturnPlate(s);
            return true;
        }

        public bool Revive()
        {
            if (!CanRevive) return false;
            var needed = SeatedOpenOrders().Select(o => GameData.Dishes[o.dish]).ToList();
            var s = slots.Where(x => x.state == SlotState.Ingredient)
                .OrderBy(x => needed.Any(d => d.parts.Contains(x.key)) ? 1 : 0)
                .ThenBy(x => x.index).FirstOrDefault();
            if (s == null) return false;
            ended = false; paused = false; won = false;
            loseReason = null; loseKind = null;
            ReturnPlate(s);
            return true;
        }

        void ReturnPlate(Slot s)
        {
            returnsLeft--;
            s.state = SlotState.Outgoing; s.timer = FLY; s.order = null;
            ReturnItem(s.index, s.key);
            CombineWaiting();
        }

        void ReturnItem(int slot, string k)
        {
            // same ingredient visible → stack onto it; otherwise shortest column
            for (int c = 0; c < columns.Length; c++)
                foreach (var cr in columns[c].visible)
                    if (cr.ing == k)
                    {
                        cr.count++;
                        Emit(new Ev { type = EvType.Return, slot = slot, key = k, col = c, crateId = cr.id, row = columns[c].visible.IndexOf(cr) });
                        Schedule(FLY, () => Emit(new Ev { type = EvType.CrateAdd, crateId = cr.id, value = cr.count }));
                        return;
                    }
            for (int c = 0; c < columns.Length; c++)
                foreach (var cr in columns[c].hidden)
                    if (cr.ing == k)
                    {
                        cr.count++;
                        Emit(new Ev { type = EvType.Return, slot = slot, key = k, col = c, value = columns[c].hidden.Count });
                        return;
                    }
            int best = 0;
            for (int c = 1; c < columns.Length; c++)
                if (columns[c].visible.Count < columns[best].visible.Count ||
                    (columns[c].visible.Count == columns[best].visible.Count && columns[c].hidden.Count < columns[best].hidden.Count)) best = c;
            var col = columns[best];
            while (columns.Any(c => c.visible.Concat(c.hidden).Any(cr => cr.id == returnCrateId))) returnCrateId++;
            var nc = new Crate { id = returnCrateId++, ing = k, count = 1 };
            if (col.visible.Count < LevelGen.VISIBLE)
            {
                col.visible.Add(nc);
                Emit(new Ev { type = EvType.Return, slot = slot, key = k, col = best, crateId = nc.id, row = col.visible.Count - 1, crate = nc });
            }
            else
            {
                col.hidden.Insert(0, nc);
                Emit(new Ev { type = EvType.Return, slot = slot, key = k, col = best, crateId = -1, row = -1, value = col.hidden.Count });
            }
        }

        // ---------------------------------------------------------------- merging / delivery
        IEnumerable<Order> SeatedOpenOrders()
        {
            foreach (var cu in seats)
                if (cu != null && !cu.finished)
                    foreach (var o in cu.orders) if (!o.done && !o.reserved) yield return o;
        }

        bool FindGather(out Order order, out int[] match)
        {
            var keys = new string[slots.Length];
            foreach (var s in slots)
                if (s.state == SlotState.Ingredient && !s.uncut && s.order == null) keys[s.index] = s.key;
            foreach (var o in SeatedOpenOrders().OrderByDescending(x => x.customer.vip)
                .ThenBy(x => x.customer.vip ? x.customer.patience : 0).ThenBy(x => x.customer.id)
                .ThenByDescending(x => GameData.Dishes[x.dish].parts.Length))
                if (GameData.TryGather(GameData.Dishes[o.dish], keys, out match)) { order = o; return true; }
            order = null; match = null; return false;
        }

        void CombineWaiting()
        {
            while (FindGather(out var o, out var match))
            {
                var d = GameData.Dishes[o.dish];
                // TryGather returns recipe order, so the first part owns the target plate.
                var target = slots[match[0]];
                var sources = match.Skip(1).ToArray();
                var keys = sources.Select(i => slots[i].key).ToArray();
                foreach (int i in sources) { ClearSlot(slots[i]); slots[i].timer = GATHER; }
                o.reserved = true; target.order = o;
                target.state = SlotState.Merging; target.key = d.id; target.items.Clear();
                target.timer = GATHER + MERGE;
                Emit(new Ev { type = EvType.Gather, slot = target.index, slots = sources, keys = keys, key = d.id, order = o });
                Schedule(GATHER, () => DoMergeFx(target.index, d.parts[d.parts.Length - 1], d.parts[d.parts.Length - 2], d.id));
            }
        }

        void DishReady(Slot s)
        {
            var def = GameData.Dishes[s.key];
            if (def.station == StationType.None)
            {
                SendToCustomer(Src.Slot, s.index, StationType.None, s.key, s.order);
                s.state = SlotState.Outgoing; s.timer = DELIVER;
            }
            else
            {
                var st = stations[def.station];
                if (st.state == StationState.Idle)
                {
                    // A VIP dish already waiting must also beat a normal dish whose
                    // merge just completed, before the regular queue dispatch runs.
                    var vip = PrioritizedDishes(slots.Where(x => x.state == SlotState.Dish && x.timer <= 0 &&
                        GameData.Dishes[x.key].station == def.station))
                        .FirstOrDefault(x => PreferredOrder(x.key, x.order).customer.vip);
                    SendToStation(vip ?? s, st);
                }
                if (s.state == SlotState.Dish)
                    Emit(new Ev { type = EvType.WaitStation, slot = s.index, station = def.station, key = s.key });
            }
        }

        static void ClearSlot(Slot s)
        {
            s.state = SlotState.Empty; s.key = null; s.order = null; s.timer = 0;
            s.items.Clear(); s.uncut = false; s.cutQueueOrder = 0;
        }

        void DispatchWaitingCuts()
        {
            var board = stations[StationType.Board];
            if (board.state != StationState.Idle) return;
            // Include incoming items in ordering so a later landing cannot jump the queue.
            var s = slots.Where(x => x.uncut && (x.state == SlotState.Incoming || x.state == SlotState.Ingredient))
                .OrderBy(x => x.cutQueueOrder).FirstOrDefault();
            if (s == null || s.state != SlotState.Ingredient) return;
            board.state = StationState.Receiving; board.timer = FLY; board.dish = s.key;
            Emit(new Ev { type = EvType.ToStation, src = Src.Slot, slot = s.index, station = StationType.Board, key = s.key });
            ClearSlot(s);
        }

        void DispatchWaitingDishes()
        {
            foreach (var s in PrioritizedDishes(slots.Where(x => x.state == SlotState.Dish && x.timer <= 0)))
            {
                var def = GameData.Dishes[s.key];
                if (def.station == StationType.None) continue;
                var st = stations[def.station];
                if (st.state == StationState.Idle) SendToStation(s, st);
            }
        }

        void SendToStation(Slot s, Station st)
        {
            s.order = AssignDish(s.key, s.order);
            st.state = StationState.Receiving; st.timer = TO_STATION; st.dish = s.key; st.order = s.order;
            Emit(new Ev { type = EvType.ToStation, src = Src.Slot, slot = s.index, station = st.type, key = s.key });
            s.state = SlotState.Outgoing; s.timer = TO_STATION;
        }

        void SendToCustomer(Src src, int slot, StationType station, string dish, Order order)
        {
            order = AssignDish(dish, order);
            delivering.Add(order);
            Emit(new Ev { type = EvType.Deliver, src = src, slot = slot, station = station, key = dish, order = order, customer = order.customer, value = order.customer.seat });
            Schedule(DELIVER, () => Delivered(order));
        }

        Order PreferredOrder(string dish, Order current)
        {
            // Reservations can move between matching dishes, but a dish already flying
            // to a customer must keep its recipient until its delivery callback runs.
            return seats.Where(c => c != null && c.vip && !c.finished)
                .OrderBy(c => c.patience).ThenBy(c => c.id)
                .SelectMany(c => c.orders)
                .FirstOrDefault(o => o.dish == dish && !o.done && !delivering.Contains(o)) ?? current;
        }

        IEnumerable<Slot> PrioritizedDishes(IEnumerable<Slot> source)
        {
            return source.Select(s => (slot: s, order: s.order == null ? null : PreferredOrder(s.key, s.order)))
                .OrderByDescending(x => x.order != null && x.order.customer.vip)
                .ThenBy(x => x.order != null && x.order.customer.vip ? x.order.customer.patience : 0)
                .Select(x => x.slot); // Stable sorting retains the existing plate order for normal customers.
        }

        Order AssignDish(string dish, Order current)
        {
            var preferred = PreferredOrder(dish, current);
            if (preferred == current) return current;
            bool swap = preferred.reserved;
            // A station and its departing plate can both reference the same dish.
            // Update every reference, swapping another reservation when necessary.
            foreach (var s in slots)
                if (s.order == current) s.order = preferred;
                else if (swap && s.order == preferred) s.order = current;
            foreach (var st in stations.Values)
                if (st.order == current) st.order = preferred;
                else if (swap && st.order == preferred) st.order = current;
            current.reserved = swap;
            preferred.reserved = true;
            return preferred;
        }

        void Delivered(Order o)
        {
            delivering.Remove(o);
            // Delivery callbacks run before slot/station ticks. Release the delivered item now,
            // so the last delivery can win immediately with a truly empty counter.
            foreach (var s in slots)
                if (s.state == SlotState.Outgoing && s.order == o) ClearSlot(s);
            foreach (var st in stations.Values)
                if (st.state == StationState.Outgoing && st.order == o)
                { st.state = StationState.Idle; st.dish = null; st.order = null; st.timer = 0; }
            o.done = true;
            int gain = 10 + Math.Max(0, chain - 1) * 5;
            if (o.customer.vip) gain *= 2;
            coins += gain;
            Emit(new Ev { type = EvType.Delivered, order = o, customer = o.customer, value = gain, key = o.dish });
            var cu = o.customer;
            if (!cu.AllDone) return;
            cu.finished = true; served++;
            Emit(new Ev { type = EvType.CustomerHappy, customer = cu, value = cu.seat });
            if (served == customers.Count) { Win(); return; }
            Schedule(1.15f, () =>
            {
                int seat = cu.seat;
                seats[seat] = null;
                Emit(new Ev { type = EvType.CustomerLeave, customer = cu, value = seat });
                if (nextCustomer < customers.Count) { pendingArrivals++; Schedule(0.75f, () => SeatNext(seat)); }
            });
        }

        // ---------------------------------------------------------------- end conditions
        bool Busy()
        {
            if (scheduled.Count > 0 || pendingArrivals > 0) return true;
            if (slots.Any(s => s.timer > 0 || s.state == SlotState.Dish)) return true;
            return stations.Values.Any(st => st.state != StationState.Idle && st.state != StationState.Waiting);
        }

        void CheckStuck()
        {
            if (ended || Busy()) return;
            if (FindGather(out _, out _)) { CombineWaiting(); return; }
            bool anyMove = VisibleCrates.Any(c => CanPlace(c.ing));
            if (anyMove) return;
            if (!slots.Any(Available)) { Lose("Hết đĩa trống!", "stuck"); return; }
            if (!VisibleCrates.Any() && columns.All(c => c.hidden.Count == 0)) Lose("Hết nguyên liệu!", "empty");
        }

        void Win()
        {
            if (ended) return;
            if (served != customers.Count || columns.Any(c => c.visible.Count > 0 || c.hidden.Count > 0) ||
                slots.Any(s => s.state != SlotState.Empty || s.items.Count > 0 || s.key != null) ||
                stations.Values.Any(st => st.state != StationState.Idle || st.dish != null))
                throw new InvalidOperationException("Exact-stock invariant broken at final delivery on day " + level.number);
            ended = true; won = true;
            Emit(new Ev { type = EvType.Win, value = Stars() });
        }

        void Lose(string reason, string kind)
        {
            if (ended) return;
            ended = true; won = false; loseReason = reason; loseKind = kind;
            Emit(new Ev { type = EvType.Lose, text = reason });
        }

        public int Stars()
        {
            int assists = (3 - hintsLeft) + (3 - returnsLeft);
            return assists == 0 ? 3 : assists <= 2 ? 2 : 1;
        }
    }
}
