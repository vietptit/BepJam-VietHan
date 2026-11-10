using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BepJam
{
    public enum Country { VN, KR }
    public enum StationType { None, Stove, Oven, Board }

    public class IngredientDef
    {
        public string id, name;
        public bool cut;                 // must be chopped on the cutting board first (from the day the board is introduced)
        public IngredientDef(string id, string name, bool cut) { this.id = id; this.name = name; this.cut = cut; }
        public Sprite Icon => Art.Food(id);
    }

    public class DishDef
    {
        public string id, name, verb, story;
        public string[] parts;           // 2–4 ingredients
        public Country country;
        public StationType station;
        public string a => parts[0];
        public string b => parts[parts.Length - 1];
        public DishDef(string id, string name, Country c, string[] parts, string verb, StationType st, string story)
        { this.id = id; this.name = name; country = c; this.parts = parts; this.verb = verb; station = st; this.story = story; }
        public Sprite Icon => Art.Food("d_" + id);
        public string ShortName => name.Split('(')[0].Trim();
    }

    public class LevelDef
    {
        public int number;
        public string title, tip, newDish;
        public string[][] customers;     // each customer orders 1..2 dishes
        public bool[] vip;               // per customer: VIP with a patience timer
        public float vipPatience = 72f;  // seconds a VIP waits once seated
        public float cookTime = 3f, cutTime = 1.4f;
        public int plates = 6;
        public int decoys = 0;           // retained for compatibility; stock never contains extras
        public int hiddenRows;           // planned extra crates per column, after filling the 4x4 visible grid
        public bool cutOn;               // cutting board active
        public int seed = 1;
        public bool tutorial;
        public string[] coach;           // once-per-save tips queued by GameView
        public string boosterIntro;      // "hint" / "return" / "book": booster introduced (and unlocked) on this day
        public bool boss;                // special "Đại tiệc" day
        public bool NeedsCut(string ing) => cutOn && GameData.Ingredients.TryGetValue(ing, out var d) && d.cut;
    }

    /// <summary>Recipes from the design sheet "Nguyên liệu game - Công thức" (Lv1–24: 2, 3 and 4 ingredients).</summary>
    public static class GameData
    {
        public static readonly Dictionary<string, IngredientDef> Ingredients = new Dictionary<string, IngredientDef>();
        public static readonly Dictionary<string, DishDef> Dishes = new Dictionary<string, DishDef>();
        public static readonly List<LevelDef> Levels = new List<LevelDef>();
        public static readonly List<string> UnlockOrder = new List<string>();   // dish ids in the order they are introduced

        static void I(string id, string name, bool cut = false) => Ingredients[id] = new IngredientDef(id, name, cut);
        static void D(string id, string name, Country c, string verb, StationType st, string story, params string[] parts)
            => Dishes[id] = new DishDef(id, name, c, parts, verb, st, story);

        static GameData()
        {
            // ---------------- ingredients (cut = needs the cutting board once it is introduced)
            I("banh_mi", "Bánh mì", true); I("cha_lua", "Chả lụa", true); I("com", "Cơm"); I("kimchi", "Kimchi");
            I("ca_phe", "Cà phê"); I("sua_dac", "Sữa đặc"); I("mut_yuja", "Mứt yuja"); I("nuoc_nong", "Nước nóng");
            I("gao_nep", "Gạo nếp"); I("dau_xanh", "Đậu xanh"); I("bot_banh_ca", "Bột bánh cá"); I("dau_do", "Đậu đỏ");
            I("trung", "Trứng"); I("banh_gao", "Bánh gạo"); I("sot_tteok", "Sốt tteok");
            I("banh_trang", "Bánh tráng"); I("tom", "Tôm"); I("rau_song", "Rau sống"); I("rong_bien", "Rong biển");
            I("cu_cai", "Củ cải muối"); I("tra", "Trà"); I("tac", "Tắc", true); I("duong", "Đường");
            I("bot_ngu_coc", "Bột ngũ cốc"); I("sua_tuoi", "Sữa tươi"); I("mat_ong", "Mật ong"); I("com_tam", "Cơm tấm");
            I("suon", "Sườn nướng"); I("do_chua", "Đồ chua"); I("bot_hotteok", "Bột hotteok"); I("duong_nau", "Đường nâu");
            I("hat", "Hạt nghiền"); I("thit_nuong", "Thịt nướng", true); I("dua_leo", "Dưa leo", true); I("thit_bo", "Thịt bò", true);
            I("nuoc_tuong", "Nước tương"); I("le", "Lê", true); I("bot_banh_xeo", "Bột bánh xèo"); I("gia_do", "Giá đỗ");
            I("bot_jeon", "Bột jeon"); I("hanh_la", "Hành lá", true); I("muc", "Mực", true); I("dao", "Đào", true);
            I("cam", "Cam", true); I("sa", "Sả", true); I("mach_nha", "Mạch nha"); I("nuoc", "Nước");
            I("banh_pho", "Bánh phở"); I("nuoc_dung_pho", "Nước dùng phở"); I("rau_thom", "Rau thơm"); I("rau_cu", "Rau củ trộn");
            I("gochujang", "Gochujang"); I("bun", "Bún"); I("thit_heo_nuong", "Thịt heo nướng"); I("nuoc_mam", "Nước mắm pha");
            I("thit_heo", "Thịt heo", true); I("dau_phu", "Đậu phụ", true); I("nuoc_dung", "Nước dùng");

            var N = StationType.None; var S = StationType.Stove; var O = StationType.Oven;
            // ---------------- Lv1–8: 2 ingredients
            D("banh_mi_cha", "Bánh mì chả lụa", Country.VN, "Kẹp", N, "Ổ bánh mì giòn kẹp chả lụa – bữa sáng quen thuộc của người Việt.", "banh_mi", "cha_lua");
            D("com_kimchi", "Cơm trộn kimchi", Country.KR, "Trộn", N, "Cơm nóng trộn kimchi chua cay – món nhà làm của người Hàn.", "com", "kimchi");
            D("ca_phe_sua", "Cà phê sữa", Country.VN, "Pha", N, "Cà phê phin đậm đà hòa cùng sữa đặc ngọt ngào.", "ca_phe", "sua_dac");
            D("tra_yuja", "Trà yuja (유자차)", Country.KR, "Pha", N, "Mứt thanh yên Hàn Quốc pha nước nóng – ấm áp ngày đông.", "mut_yuja", "nuoc_nong");
            D("xoi_dau_xanh", "Xôi đậu xanh", Country.VN, "Hấp", S, "Gạo nếp dẻo thơm hấp cùng đậu xanh bùi bùi.", "gao_nep", "dau_xanh");
            D("bungeoppang", "Bungeoppang đậu đỏ", Country.KR, "Nướng khuôn", O, "Bánh cá nướng nhân đậu đỏ – món ăn vặt mùa đông ở Seoul.", "bot_banh_ca", "dau_do");
            D("banh_mi_trung", "Bánh mì trứng", Country.VN, "Chiên, kẹp", O, "Bánh mì nướng giòn kẹp trứng ốp la nóng hổi.", "banh_mi", "trung");
            D("tteokbokki", "Tteokbokki", Country.KR, "Nấu", S, "Bánh gạo dẻo nấu sốt cay ngọt – vua ăn vặt đường phố Hàn.", "banh_gao", "sot_tteok");
            // ---------------- Lv9–16: 3 ingredients
            D("goi_cuon", "Gỏi cuốn tôm", Country.VN, "Cuốn", N, "Bánh tráng cuốn tôm và rau sống thanh mát.", "banh_trang", "tom", "rau_song");
            D("gimbap", "Gimbap mini (김밥)", Country.KR, "Cuộn", N, "Cơm cuộn rong biển cùng củ cải muối vàng giòn.", "com", "rong_bien", "cu_cai");
            D("tra_tac", "Trà tắc", Country.VN, "Pha", N, "Trà xanh vắt tắc, thêm chút đường – giải khát vỉa hè Sài Gòn.", "tra", "tac", "duong");
            D("misugaru", "Misugaru mật ong (미숫가루)", Country.KR, "Pha", N, "Bột ngũ cốc rang khuấy sữa và mật ong – thức uống truyền thống Hàn.", "bot_ngu_coc", "sua_tuoi", "mat_ong");
            D("com_tam", "Cơm tấm sườn", Country.VN, "Nướng", O, "Cơm tấm, sườn nướng than và đồ chua – đặc sản Sài Gòn.", "com_tam", "suon", "do_chua");
            D("hotteok", "Hotteok (호떡)", Country.KR, "Áp chảo", S, "Bánh áp chảo nhân đường nâu và hạt – món vỉa hè mùa đông.", "bot_hotteok", "duong_nau", "hat");
            D("banh_mi_thit", "Bánh mì thịt nướng", Country.VN, "Kẹp", N, "Bánh mì kẹp thịt nướng thơm lừng và dưa leo.", "banh_mi", "thit_nuong", "dua_leo");
            D("bulgogi", "Bulgogi (불고기)", Country.KR, "Ướp, nướng", O, "Thịt bò ướp nước tương và lê, nướng mềm ngọt.", "thit_bo", "nuoc_tuong", "le");
            // ---------------- Lv17–24: 4 ingredients
            D("banh_xeo", "Bánh xèo", Country.VN, "Đổ bánh", S, "Bánh xèo vàng giòn nhân tôm, giá, ăn kèm rau sống.", "bot_banh_xeo", "tom", "gia_do", "rau_song");
            D("pajeon", "Pajeon hải sản (해물파전)", Country.KR, "Áp chảo", S, "Bánh xèo Hàn với hành lá, tôm và mực.", "bot_jeon", "hanh_la", "tom", "muc");
            D("tra_dao", "Trà đào cam sả", Country.VN, "Pha", N, "Trà đào thơm mùi cam và sả – thức uống quốc dân của giới trẻ.", "tra", "dao", "cam", "sa");
            D("sikhye", "Sikhye (식혜)", Country.KR, "Ủ, đun", S, "Nước gạo ngọt từ mạch nha, thường uống mát sau bữa ăn.", "com", "mach_nha", "duong", "nuoc");
            D("pho_bo", "Phở bò", Country.VN, "Trụng, nấu", S, "Bánh phở, thịt bò, nước dùng thơm quế hồi và rau thơm.", "banh_pho", "thit_bo", "nuoc_dung_pho", "rau_thom");
            D("bibimbap", "Bibimbap (비빔밥)", Country.KR, "Trộn", N, "Cơm trộn rau củ, trứng và tương ớt gochujang.", "com", "rau_cu", "trung", "gochujang");
            D("bun_cha", "Bún chả", Country.VN, "Nướng", O, "Bún, chả nướng than hoa, rau sống và nước mắm chua ngọt – hồn ẩm thực Hà Nội.", "bun", "thit_heo_nuong", "rau_song", "nuoc_mam");
            D("kimchi_jjigae", "Kimchi jjigae (김치찌개)", Country.KR, "Nấu", S, "Canh kimchi hầm thịt heo và đậu phụ, nóng hổi ngày lạnh.", "kimchi", "thit_heo", "dau_phu", "nuoc_dung");

            BuildLevels();
        }

        // =====================================================================================  50 days
        // Day plan: a new dish every day 1–8, then roughly every 2 days; boss on 20/30/40/50.
        static readonly Dictionary<int, string> NewDishDay = new Dictionary<int, string>
        {
            [1] = "banh_mi_cha", [2] = "com_kimchi", [3] = "ca_phe_sua", [4] = "tra_yuja", [5] = "xoi_dau_xanh", [6] = "bungeoppang",
            [7] = "banh_mi_trung", [8] = "tteokbokki", [10] = "goi_cuon", [12] = "gimbap", [14] = "tra_tac", [16] = "misugaru",
            [18] = "com_tam", [22] = "hotteok", [24] = "banh_mi_thit", [26] = "bulgogi", [28] = "banh_xeo", [32] = "pajeon",
            [34] = "tra_dao", [36] = "sikhye", [38] = "pho_bo", [42] = "bibimbap", [44] = "bun_cha", [46] = "kimchi_jjigae",
        };
        public const int CutDay = 1, VipDay = 11;

        static void BuildLevels()
        {
            string[] themes = { "Bữa sáng Hà Nội", "Phố đêm Seoul", "Quán cà phê góc phố", "Bếp lửa rực hồng", "Giao lưu Việt – Hàn",
                                "Chợ đêm náo nhiệt", "Mùa lễ hội", "Bếp trưởng huyền thoại" };
            var unlocked = new List<string>();
            for (int day = 1; day <= 50; day++)
            {
                NewDishDay.TryGetValue(day, out var nd);
                if (nd != null) { unlocked.Add(nd); UnlockOrder.Add(nd); }
                bool boss = day >= 20 && day % 10 == 0;
                int hiddenRows = day < 10 ? 0 : 1 + (day >= 18 ? 1 : 0) + (day >= 40 ? 1 : 0) + (boss ? 1 : 0);
                var rng = new System.Random(day * 7919 + 13);
                float k = (day - 1) / 49f;                                       // 0 → 1

                // ---- how many guests & how many order two dishes
                int guests = day switch { 1 => 1, 2 => 2, 3 => 3, 4 => 3, 5 => 5, 6 => 5, 7 => 6, 8 => 6, _ => Mathf.Min(13, 4 + day / 4) };
                if (boss) guests += 2;
                float two = day < 9 ? 0f : Mathf.Clamp01(0.25f + (day - 9) / 41f * 0.55f + (boss ? 0.1f : 0));

                // ---- recent, larger recipes become more common; prefer ingredients shared with today's menu
                var cs = new List<string[]>();
                string Pick()
                {
                    int n = unlocked.Count;
                    int Weight(int i)
                    {
                        var dish = Dishes[unlocked[i]];
                        int size = dish.parts.Length - 2;
                        int weight = (i >= n - 4 ? 3 : 1) + size * (1 + day / 10);
                        if (day >= 7 && cs.SelectMany(c => c).Any(d => d != dish.id && Dishes[d].parts.Intersect(dish.parts).Any()))
                            weight += 2 + day / 12;
                        return weight;
                    }
                    int total = 0; for (int i = 0; i < n; i++) total += Weight(i);
                    int r = rng.Next(total);
                    for (int i = 0; i < n; i++) { r -= Weight(i); if (r < 0) return unlocked[i]; }
                    return unlocked[n - 1];
                }
                for (int g = 0; day < 10 && g < guests; g++)
                {
                    string d1 = (nd != null && g < 2) ? nd : Pick();
                    if (day == 7 && g == 2) d1 = "banh_mi_cha";
                    // Early lookahead lessons need genuinely different future orders; repeated
                    // copies of seated recipes cannot create a meaningful six-plate trap.
                    if (day >= 5 && day <= 9 && g >= 3)
                    {
                        var future = unlocked.Where(d => !cs.Take(3).SelectMany(c => c).Contains(d)).ToArray();
                        if (future.Length > 0) d1 = future[rng.Next(future.Length)];
                    }
                    if (rng.NextDouble() < two && unlocked.Count > 1)
                    {
                        string d2; int guard = 0; do d2 = Pick(); while (d2 == d1 && guard++ < 20);
                        cs.Add(new[] { d1, d2 });
                    }
                    else cs.Add(new[] { d1 });
                }
                // One familiar future order keeps the first full-stock lessons in range.
                if (day == 8 || day == 9) cs[3] = new[] { cs[2][0] };
                if (day >= 10) cs = PlanningMenu(day, unlocked, ref hiddenRows, boss);
                guests = cs.Count;

                // ---- VIP guests (never the very first guest)
                var vip = new bool[guests];
                int vipCount = day < VipDay ? 0 : Mathf.Min(guests - 1, 1 + (day - VipDay) / 9 + (boss ? 1 : 0));
                for (int v = 0, guard = 0; v < vipCount && guard < 100; guard++)
                {
                    int idx = 1 + rng.Next(guests - 1);
                    if (!vip[idx]) { vip[idx] = true; v++; }
                }

                string title = nd != null ? Dishes[nd].ShortName : boss ? "Đại tiệc · " + themes[Mathf.Min(7, (day - 1) / 7)] : themes[Mathf.Min(7, (day - 1) / 7)] + " " + day;
                var lv = new LevelDef
                {
                    number = day, title = title, newDish = nd, customers = cs.ToArray(), vip = vip,
                    cookTime = Mathf.Lerp(3f, 3.6f, k), cutTime = 1.4f, seed = day * 31 + 7, boss = boss,
                    cutOn = day >= CutDay,
                    decoys = 0, plates = 6, hiddenRows = hiddenRows,
                    vipPatience = Mathf.Max(45f, 1.8f * Mathf.Lerp(48f, 30f, k)),
                    tip = Tip(day, nd, boss),
                };
                Levels.Add(lv);
            }
            // ---------------- tutorials & booster introductions
            Levels[0].coach = new[] { "tap_pair" }; Levels[0].tutorial = true;
            Levels[1].coach = new[] { "plates" }; Levels[1].boosterIntro = "book";
            Levels[2].boosterIntro = "hint";
            Levels[4].coach = new[] { "stove" };
            Levels[5].coach = new[] { "oven" }; Levels[5].boosterIntro = "return";
            Levels[6].coach = new[] { "shared" };
            Levels[9].coach = new[] { "multi", "hidden" };
            Levels[VipDay - 1].coach = new[] { "vip" };
            Levels[7].coach = new[] { "lookahead" };
            Levels[27].coach = new[] { "multi4" };
        }

        // Repeated recipes and shared parts keep a full pantry manageable. Limit stock
        // through actual orders: hiddenRows adds depth behind every column, never hides
        // crates that could occupy a visible cell. Recipe sizes can round depth by one.
        static List<string[]> PlanningMenu(int day, List<string> unlocked, ref int hiddenRows, bool boss)
        {
            string anchor = unlocked[unlocked.Count - 1];
            var main = Dishes[anchor];
            string companion = unlocked.Where(d => d != anchor)
                .OrderByDescending(d => Dishes[d].parts.Intersect(main.parts).Count())
                .ThenBy(d => Dishes[d].parts.Length).First();
            if (day < 26 && !boss && !Dishes[companion].parts.Intersect(main.parts).Any()) companion = anchor;
            if ((day >= 26 || boss) && companion != anchor && !Dishes[companion].parts.Intersect(main.parts).Any())
                companion = unlocked.Last(d => d != anchor && Dishes[d].parts.Length >= 3);
            // A four-part recipe with only one shared part already demands substantial
            // planning. Trade one hidden row for that complexity rather than emptying cells.
            bool complex = main.parts.Length == 4 && Dishes[companion].parts.Length == 3 &&
                main.parts.Intersect(Dishes[companion].parts).Count() < 2;
            if (complex && !boss) hiddenRows--;
            int budget = LevelGen.COLS * (LevelGen.VISIBLE + hiddenRows);
            var menu = new List<string>();
            int parts = 0;
            while (parts < budget)
            {
                bool support = menu.Count >= 2 && (menu.Count - 2) % 3 < (day < 14 ? 1 : 2);
                if (complex && !boss) support = menu.Count >= 2;
                if (day >= 40 && !boss && Dishes[companion].parts.Length == 3) support = menu.Count >= 2;
                string dish = support ? companion : anchor;
                int size = Dishes[dish].parts.Length;
                if ((complex || day >= 40) && !boss && parts + size > budget) break;
                menu.Add(dish); parts += size;
            }
            var customers = new List<string[]>();
            for (int i = 0; i < menu.Count; i++)
            {
                bool pair = day >= 12 && i >= 2 && i + 1 < menu.Count && menu[i] != menu[i + 1] &&
                    (boss || (i + day) % 3 == 0);
                if (pair) { customers.Add(new[] { menu[i], menu[i + 1] }); i++; }
                else customers.Add(new[] { menu[i] });
            }
            return customers;
        }

        static string Tip(int day, string nd, bool boss)
        {
            if (boss) return "ĐẠI TIỆC! Đông khách, có khách VIP – tính trước từng bước, đừng chạm bừa!";
            switch (day)
            {
                case 1: return "Chạm Bánh mì – bánh mì lên thớt cắt đôi rồi về đĩa. Thêm Chả lụa để ghép thành món!";
                case 2: return "Mỗi đĩa giữ nguyên liệu đang chờ. Lấy đúng cặp để không bị đầy đĩa.";
                case 3: return "Bí quá? Dùng Gợi ý để tìm nguyên liệu cho khách đang ngồi.";
                case 4: return "Nhìn đủ 3 khách đang ngồi và lấy đúng nguyên liệu cho từng món!";
                case 5: return "Món Hấp/Nấu tự vào bếp ga – bếp chỉ nấu 1 món mỗi lần.";
                case 6: return "Món Nướng vào lò – lò và bếp ga chạy song song.";
                case 7: return "Bánh mì dùng cho 2 món – ghép đúng món khách cần!";
                case 10: return "Món 3 nguyên liệu: đặt đủ từng thứ lên đĩa – đủ bộ là tự ghép!";
                case VipDay: return "Khách VIP có thanh kiên nhẫn – phục vụ kịp kẻo khách bỏ đi!";
                case 8: return "Kho có cả phần của khách chưa tới. Chỉ có 6 đĩa – ưu tiên 3 khách đang ngồi!";
                case 28: return "Món 4 nguyên liệu: cần 4 đĩa trống, tính trước nhé!";
            }
            if (nd != null) return "Món mới: " + Dishes[nd].ShortName + " – xem công thức trên bong bóng của khách.";
            return "Giữ chỗ trên đĩa, nhìn kỹ thùng phía sau và ưu tiên khách VIP.";
        }

        public const int BookDay = 2, HintDay = 3, ReturnDay = 6;
        public static bool BoosterUnlocked(string id, int day) => day >= (id == "book" ? BookDay : id == "hint" ? HintDay : ReturnDay);

        /// <summary>Recipe contains every supplied item, respecting repeated parts.</summary>
        public static bool Fits(DishDef d, List<string> items)
        {
            var need = new List<string>(d.parts);
            foreach (var it in items) { int i = need.IndexOf(it); if (i < 0) return false; need.RemoveAt(i); }
            return true;
        }
        public static bool Complete(DishDef d, List<string> items) => items.Count == d.parts.Length && Fits(d, items);

        /// <summary>One distinct plate per part, in recipe order. Null keys are unavailable plates.</summary>
        public static bool TryGather(DishDef d, IReadOnlyList<string> keys, out int[] indices)
        {
            indices = null;
            var found = new int[d.parts.Length];
            for (int p = 0; p < d.parts.Length; p++)
            {
                int index = -1;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] != d.parts[p]) continue;
                    bool used = false;
                    for (int j = 0; j < p; j++) if (found[j] == i) { used = true; break; }
                    if (!used) { index = i; break; }
                }
                if (index < 0) return false;
                found[p] = index;
            }
            if (!Complete(d, found.Select(i => keys[i]).ToList())) return false;
            indices = found; return true;
        }

        public static string StationName(StationType s) => s == StationType.Stove ? "Bếp ga" : s == StationType.Oven ? "Lò nướng" : s == StationType.Board ? "Thớt" : "";
    }

    /// <summary>Sprite cache over Resources.</summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static Sprite Get(string path, bool warnIfMissing = true)
        {
            if (cache.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(path);
            if (s == null && warnIfMissing) Debug.LogWarning("[BepJam] Missing sprite: " + path);
            cache[path] = s;
            return s;
        }
        public static Sprite Food(string id) => Get("Food/" + id);
        public static Sprite UI(string id) => Get("UI/" + id);
        public static Sprite FX(string id) => Get("FX/" + id);
        public static Sprite Char(string id) => Get("Characters/" + id);

        static Font font;
        public static Font Font => font ? font : (font = Resources.Load<Font>("Fonts/CookieRun"));
    }

    public static class Pal
    {
        public static readonly Color Outline = Hex("4A2F26");
        public static readonly Color Cream = Hex("FFF4E1");
        public static readonly Color Beige = Hex("F8D6A4");
        public static readonly Color Caramel = Hex("DDA772");
        public static readonly Color Coral = Hex("F28B7B");
        public static readonly Color Tomato = Hex("E65A4F");
        public static readonly Color Leaf = Hex("A6D365");
        public static readonly Color Mint = Hex("7ED6C6");
        public static readonly Color Sky = Hex("7DC8E8");
        public static readonly Color Butter = Hex("FFD66B");
        public static readonly Color TextBrown = Hex("6B4A3A");
        public static readonly Color Blue = Hex("2F7FC1");
        public static readonly Color Red = Hex("E0413A");
        public static readonly Color Green = Hex("4E9A3A");
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    }
}
