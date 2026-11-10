using System.Collections.Generic;
using UnityEngine;

namespace BepJam
{
    public enum Country { VN, KR }
    public enum StationType { None, Stove, Oven }

    public class IngredientDef
    {
        public string id, name;
        public IngredientDef(string id, string name) { this.id = id; this.name = name; }
        public Sprite Icon => Art.Food(id);
    }

    public class DishDef
    {
        public string id, name, verb, a, b, story;
        public Country country;
        public StationType station;
        public DishDef(string id, string name, Country c, string a, string b, string verb, StationType st, string story)
        { this.id = id; this.name = name; country = c; this.a = a; this.b = b; this.verb = verb; station = st; this.story = story; }
        public Sprite Icon => Art.Food("d_" + id);
    }

    public class LevelDef
    {
        public int number;
        public string title, tip, newDish;
        public string[][] customers;     // each customer orders 1..2 dishes
        public float time;               // seconds, 0 = relaxed (no timer)
        public float cookTime = 3f;
        public int plates = 4;
        public int seed = 1;
        public bool tutorial;
        public string[] coach;           // tutorial steps shown at the start of the level (see GameView.Coach)
        public string boosterIntro;      // "hint" / "return" / "book": booster introduced (and unlocked) on this day
        public bool boss;                // special "Đại tiệc" day
    }

    /// <summary>All recipes come from the design sheet "Nguyên liệu game - Công thức" (Lv1–Lv8, 2 ingredients each).</summary>
    public static class GameData
    {
        public static readonly Dictionary<string, IngredientDef> Ingredients = new Dictionary<string, IngredientDef>();
        public static readonly Dictionary<string, DishDef> Dishes = new Dictionary<string, DishDef>();
        public static readonly List<LevelDef> Levels = new List<LevelDef>();

        static void I(string id, string name) => Ingredients[id] = new IngredientDef(id, name);
        static void D(string id, string name, Country c, string a, string b, string verb, StationType st, string story)
            => Dishes[id] = new DishDef(id, name, c, a, b, verb, st, story);

        static GameData()
        {
            I("banh_mi", "Bánh mì"); I("cha_lua", "Chả lụa"); I("com", "Cơm"); I("kimchi", "Kimchi");
            I("ca_phe", "Cà phê"); I("sua_dac", "Sữa đặc"); I("mut_yuja", "Mứt yuja"); I("nuoc_nong", "Nước nóng");
            I("gao_nep", "Gạo nếp"); I("dau_xanh", "Đậu xanh"); I("bot_banh_ca", "Bột bánh cá"); I("dau_do", "Đậu đỏ");
            I("trung", "Trứng"); I("banh_gao", "Bánh gạo"); I("sot_tteok", "Sốt tteok");

            D("banh_mi_cha", "Bánh mì chả lụa", Country.VN, "banh_mi", "cha_lua", "Kẹp", StationType.None,
              "Ổ bánh mì giòn kẹp chả lụa – bữa sáng quen thuộc của người Việt.");
            D("com_kimchi", "Cơm trộn kimchi", Country.KR, "com", "kimchi", "Trộn", StationType.None,
              "Cơm nóng trộn kimchi chua cay – món nhà làm của người Hàn.");
            D("ca_phe_sua", "Cà phê sữa", Country.VN, "ca_phe", "sua_dac", "Pha", StationType.None,
              "Cà phê phin đậm đà hòa cùng sữa đặc ngọt ngào.");
            D("tra_yuja", "Trà yuja (유자차)", Country.KR, "mut_yuja", "nuoc_nong", "Pha", StationType.None,
              "Mứt thanh yên Hàn Quốc pha nước nóng – ấm áp ngày đông.");
            D("xoi_dau_xanh", "Xôi đậu xanh", Country.VN, "gao_nep", "dau_xanh", "Hấp", StationType.Stove,
              "Gạo nếp dẻo thơm hấp cùng đậu xanh bùi bùi.");
            D("bungeoppang", "Bungeoppang đậu đỏ", Country.KR, "bot_banh_ca", "dau_do", "Nướng khuôn", StationType.Oven,
              "Bánh cá nướng nhân đậu đỏ – món ăn vặt mùa đông ở Seoul.");
            D("banh_mi_trung", "Bánh mì trứng", Country.VN, "banh_mi", "trung", "Chiên, kẹp", StationType.Oven,
              "Bánh mì nướng giòn kẹp trứng ốp la nóng hổi.");
            D("tteokbokki", "Tteokbokki", Country.KR, "banh_gao", "sot_tteok", "Nấu", StationType.Stove,
              "Bánh gạo dẻo nấu sốt cay ngọt – vua ăn vặt đường phố Hàn.");

            int n = 0;
            LevelDef L(string title, string tip, string newDish, float time, float cook, int seed, params string[][] cs)
            {
                var l = new LevelDef { number = ++n, title = title, tip = tip, newDish = newDish, time = time, cookTime = cook, seed = seed, customers = cs };
                Levels.Add(l); return l;
            }
            string[] O(params string[] d) => d;

            L("Ổ bánh mì đầu tiên", "Chạm Bánh mì rồi Chả lụa. Hai nguyên liệu gặp nhau trên đĩa sẽ tự ghép thành món!",
              "banh_mi_cha", 0, 3, 3, O("banh_mi_cha")).tutorial = true;
            L("Cơm trộn kimchi", "Mỗi đĩa chỉ chứa 1 nguyên liệu. Lấy đúng cặp để không bị đầy đĩa nhé.",
              "com_kimchi", 0, 3, 7, O("com_kimchi"), O("banh_mi_cha"));
            L("Cà phê sữa Sài Gòn", "Bắt đầu tính giờ! Phục vụ đủ khách trước khi hết giờ.",
              "ca_phe_sua", 120, 3, 11, O("ca_phe_sua"), O("com_kimchi"), O("ca_phe_sua"));
            L("Trà yuja ấm áp", "Thùng phía sau chỉ mở khi lấy hết thùng phía trước. Tính trước mỗi lần chạm!",
              "tra_yuja", 150, 3, 5, O("tra_yuja"), O("banh_mi_cha"), O("ca_phe_sua"), O("tra_yuja"));
            L("Xôi đậu xanh", "Gạo nếp + đậu xanh cần Hấp trên bếp ga. Bếp chỉ nấu 1 món mỗi lần!",
              "xoi_dau_xanh", 160, 3, 21, O("xoi_dau_xanh"), O("tra_yuja"), O("xoi_dau_xanh"), O("com_kimchi"));
            L("Bungeoppang đậu đỏ", "Bánh cá được nướng trong lò. Lò và bếp ga chạy song song.",
              "bungeoppang", 170, 3, 9, O("bungeoppang"), O("ca_phe_sua"), O("bungeoppang"), O("xoi_dau_xanh"));
            L("Bánh mì trứng", "Bánh mì dùng cho 2 món: chả lụa và trứng. Ghép đúng hướng khách cần!",
              "banh_mi_trung", 180, 3.5f, 4, O("banh_mi_trung"), O("banh_mi_cha"), O("banh_mi_trung", "ca_phe_sua"), O("banh_mi_cha"));
            L("Tteokbokki cay cay", "Tteokbokki cũng nấu trên bếp ga – sắp xếp thứ tự với xôi nhé.",
              "tteokbokki", 200, 3.5f, 13, O("tteokbokki"), O("xoi_dau_xanh"), O("tteokbokki", "tra_yuja"), O("bungeoppang"), O("com_kimchi"));
            L("Sáng Hà Nội", "Khách gọi 2 món. Món nào xong trước sẽ được mang ra trước.",
              null, 220, 3.5f, 17, O("banh_mi_cha", "ca_phe_sua"), O("xoi_dau_xanh"), O("banh_mi_trung", "ca_phe_sua"), O("xoi_dau_xanh", "tra_yuja"), O("banh_mi_cha"));
            L("Đêm Seoul", "Lò và bếp đều bận rộn. Đừng để đĩa chờ bị đầy!",
              null, 220, 4, 23, O("tteokbokki", "tra_yuja"), O("bungeoppang"), O("com_kimchi", "tteokbokki"), O("bungeoppang", "tra_yuja"), O("com_kimchi"));
            L("Lễ hội Việt – Hàn", "Thực đơn đầy đủ 8 món của hai nước!",
              null, 260, 4, 31, O("banh_mi_cha", "tra_yuja"), O("tteokbokki", "ca_phe_sua"), O("xoi_dau_xanh", "com_kimchi"),
              O("bungeoppang", "banh_mi_trung"), O("ca_phe_sua", "tteokbokki"), O("banh_mi_cha", "bungeoppang"));
            L("Bếp trưởng Việt – Hàn", "Đông khách, ít giờ – chứng tỏ tay nghề bếp trưởng nào!",
              null, 270, 4, 37, O("banh_mi_trung", "ca_phe_sua"), O("tteokbokki", "com_kimchi"), O("xoi_dau_xanh", "tra_yuja"),
              O("bungeoppang", "banh_mi_cha"), O("com_kimchi", "ca_phe_sua"), O("tteokbokki", "xoi_dau_xanh"), O("banh_mi_cha", "tra_yuja"));

            // ---------------- tutorials & booster introductions for the first days
            Levels[0].coach = new[] { "tap_pair" };                        // day 1: hand guides the first combo
            Levels[1].coach = new[] { "order_bubble", "plates" };          // day 2: tap orders to see recipes; plates are limited
            Levels[1].boosterIntro = "book";
            Levels[2].coach = new[] { "timer" };                           // day 3: the clock starts
            Levels[2].boosterIntro = "hint";
            Levels[3].coach = new[] { "hidden" };                          // day 4: crates behind crates
            Levels[4].coach = new[] { "stove" };                           // day 5: steaming on the gas stove
            Levels[5].coach = new[] { "oven" };                            // day 6: the oven runs in parallel
            Levels[5].boosterIntro = "return";
            Levels[6].coach = new[] { "shared" };                          // day 7: one ingredient, two recipes
            Levels[8].coach = new[] { "two_dishes" };                      // day 9: guests order two dishes

            // ---------------- days 13–50: themed 5-day blocks, rhythm easy → medium → harder → breather → hard,
            // with a "Đại tiệc" boss every 10 days. Every layout is proven solvable when the level is built.
            string[] VN = { "banh_mi_cha", "ca_phe_sua", "xoi_dau_xanh", "banh_mi_trung" };
            string[] KR = { "com_kimchi", "tra_yuja", "bungeoppang", "tteokbokki" };
            string[] DRINK = { "ca_phe_sua", "tra_yuja", "bungeoppang", "banh_mi_cha", "banh_mi_trung" };
            string[] HOT = { "xoi_dau_xanh", "bungeoppang", "banh_mi_trung", "tteokbokki", "com_kimchi" };
            var ALL = new List<string>(Dishes.Keys).ToArray();
            var blocks = new (int from, string name, string[] pool, string tip)[]
            {
                (13, "Bữa sáng Hà Nội", VN, "Món Việt buổi sáng – bánh mì dùng cho cả chả lụa lẫn trứng!"),
                (18, "Phố đêm Seoul", KR, "Tteokbokki và bungeoppang tranh nhau bếp – sắp thứ tự cho khéo."),
                (23, "Quán cà phê góc phố", DRINK, "Đồ uống pha nhanh – tranh thủ ghép chuỗi combo!"),
                (28, "Bếp lửa rực hồng", HOT, "Nhiều món cần bếp và lò – đừng để đĩa chờ bị đầy."),
                (33, "Giao lưu Việt – Hàn", ALL, "Thực đơn hai nước đan xen – nhìn kỹ thùng phía sau."),
                (38, "Chợ đêm náo nhiệt", ALL, "Khách gọi hai món nhiều hơn – ưu tiên khách gần xong."),
                (43, "Mùa lễ hội", ALL, "Giờ eo hẹp – dùng Gợi ý và Trả đĩa đúng lúc."),
                (48, "Bếp trưởng huyền thoại", ALL, "Thử thách cuối – chứng tỏ tay nghề bếp trưởng Việt – Hàn!"),
            };
            int[] guestOff = { 0, 1, 1, 0, 2 };                 // rhythm inside a block
            float[] twoOff = { 0f, 0.05f, 0.12f, -0.1f, 0.18f };
            float[] timeMul = { 1.12f, 1.0f, 0.95f, 1.2f, 0.9f };
            for (int day = 13; day <= 50; day++)
            {
                int b = 0; for (int i = 0; i < blocks.Length; i++) if (day >= blocks[i].from) b = i;
                var blk = blocks[b];
                int r = (day - blk.from) % 5;
                bool boss = day % 10 == 0;
                float k = (day - 13) / 37f;                                        // global ramp 0 → 1
                int guests = Mathf.Min(9, 4 + Mathf.RoundToInt(k * 3) + guestOff[r] + (boss ? 2 : 0));
                float two = Mathf.Clamp01(0.35f + k * 0.45f + twoOff[r] + (boss ? 0.1f : 0f));
                var rng = new System.Random(day * 7919);
                var cs = new List<string[]>(); int dishCount = 0;
                for (int g = 0; g < guests; g++)
                {
                    string d1 = blk.pool[rng.Next(blk.pool.Length)];
                    if (rng.NextDouble() < two)
                    {
                        string d2; do d2 = blk.pool[rng.Next(blk.pool.Length)]; while (d2 == d1);
                        cs.Add(O(d1, d2)); dishCount += 2;
                    }
                    else { cs.Add(O(d1)); dishCount++; }
                }
                float tm = timeMul[r] * Mathf.Lerp(1.08f, 0.74f, k) * (boss ? 0.95f : 1f);
                float time = Mathf.Round((dishCount * 23f + 45f) * tm / 5f) * 5f;
                string title = boss ? "Đại tiệc · " + blk.name : blk.name + " " + (day - blk.from + 1);
                string tip = boss ? "ĐẠI TIỆC! Đông khách gấp đôi – giữ bình tĩnh và tính trước từng bước." : r == 3 ? "Ngày nhẹ nhàng – thư giãn và ghép thật nhiều combo!" : blk.tip;
                var lv = L(title, tip, null, time, Mathf.Lerp(4f, 3.2f, k), 100 + day, cs.ToArray());
                lv.boss = boss;
            }
        }

        public const int BookDay = 2, HintDay = 3, ReturnDay = 6;
        public static bool BoosterUnlocked(string id, int day) => day >= (id == "book" ? BookDay : id == "hint" ? HintDay : ReturnDay);

        public static string ComboOf(string x, string y)
        {
            foreach (var d in Dishes.Values)
                if ((d.a == x && d.b == y) || (d.a == y && d.b == x)) return d.id;
            return null;
        }

        public static string StationName(StationType s) => s == StationType.Stove ? "Bếp ga" : s == StationType.Oven ? "Lò nướng" : "";
    }

    /// <summary>Sprite cache over Resources.</summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static Sprite Get(string path)
        {
            if (cache.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(path);
            if (s == null) Debug.LogWarning("[BepJam] Missing sprite: " + path);
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
