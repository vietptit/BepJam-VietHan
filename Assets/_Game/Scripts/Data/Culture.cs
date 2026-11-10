using System.Collections.Generic;
using UnityEngine;

namespace BepJam
{
    /// <summary>Cultural content for level introductions, food stories and the recipe notebook.
    /// Source text: Tools/review/culture_stories.md (approved by the client).</summary>
    public static class Culture
    {
        public class Entry { public string native, intro; public string[] stories; }

        public static readonly Dictionary<string, Entry> Dish = new Dictionary<string, Entry>
        {
            ["banh_mi_cha"] = new Entry { native = "Bánh mì chả lụa",
                intro = "Ổ bánh mì vỏ giòn ruột xốp kẹp chả lụa, dưa góp, rau thơm – bữa sáng \"quốc dân\" của người Việt.",
                stories = new[] {
                    "Bánh mì baguette theo chân người Pháp vào Việt Nam từ thế kỷ 19; người Việt làm lại cho vỏ mỏng, ruột nhẹ hơn và kẹp đủ thứ nhân.",
                    "Năm 2011, từ \"banh mi\" được đưa vào Từ điển tiếng Anh Oxford.",
                    "Chả lụa có tên như vậy vì mặt cắt mịn và bóng như lụa – thịt heo được giã thật nhuyễn rồi gói lá chuối luộc chín." } },
            ["com_kimchi"] = new Entry { native = "김치볶음밥",
                intro = "Cơm nóng trộn kimchi chua cay – món nhà làm quen thuộc của mọi gia đình Hàn.",
                stories = new[] {
                    "Hàn Quốc có hàng trăm loại kimchi, từ cải thảo, củ cải đến dưa chuột.",
                    "Truyền thống làm kimchi mùa đông \"Kimjang\" được UNESCO ghi danh là Di sản văn hoá phi vật thể năm 2013." } },
            ["ca_phe_sua"] = new Entry { native = "Cà phê sữa đá",
                intro = "Cà phê phin đậm đà nhỏ từng giọt, hoà cùng sữa đặc ngọt ngào – rất \"Sài Gòn\".",
                stories = new[] {
                    "Cà phê được người Pháp đưa vào Việt Nam từ thế kỷ 19; ngày nay Việt Nam là một trong những nước xuất khẩu cà phê lớn nhất thế giới.",
                    "Người Việt dùng sữa đặc vì ngày xưa sữa tươi rất khó bảo quản trong khí hậu nóng ẩm." } },
            ["tra_yuja"] = new Entry { native = "유자차",
                intro = "Mứt thanh yên (yuja) pha nước nóng – thức uống ấm áp mùa đông của người Hàn.",
                stories = new[] {
                    "Yuja là một loại quả họ cam chanh, thơm và rất giàu vitamin C, nên thường được uống khi trời lạnh.",
                    "Người Hàn ngâm lát yuja với mật ong hoặc đường thành mứt, để dành uống cả mùa đông." } },
            ["xoi_dau_xanh"] = new Entry { native = "Xôi đậu xanh",
                intro = "Gạo nếp dẻo thơm hấp cùng đậu xanh bùi bùi, rắc hành phi.",
                stories = new[] {
                    "Xôi là món sáng mang đi phổ biến, ngày xưa thường được gói trong lá chuối hoặc lá sen.",
                    "Xôi cũng xuất hiện trên mâm cỗ ngày Tết và các dịp lễ, tượng trưng cho sự đủ đầy." } },
            ["bungeoppang"] = new Entry { native = "붕어빵",
                intro = "Bánh hình con cá nướng giòn, nhân đậu đỏ ngọt – món ăn vặt mùa đông ở Seoul.",
                stories = new[] {
                    "\"Bungeo\" nghĩa là cá diếc, \"ppang\" là bánh – dù vậy trong bánh chẳng có chút cá nào!",
                    "Món bánh này bắt nguồn từ taiyaki của Nhật và trở nên phổ biến ở Hàn từ khoảng thập niên 1930." } },
            ["banh_mi_trung"] = new Entry { native = "Bánh mì trứng",
                intro = "Bánh mì nướng giòn kẹp trứng ốp la nóng hổi, thêm chút xì dầu và ớt.",
                stories = new[] { "Đây là bữa sáng nhanh – rẻ – no của học sinh, sinh viên khắp Việt Nam." } },
            ["tteokbokki"] = new Entry { native = "떡볶이",
                intro = "Bánh gạo dẻo nấu sốt gochujang cay ngọt – \"vua ăn vặt\" đường phố Hàn Quốc.",
                stories = new[] {
                    "Ban đầu tteokbokki là món cung đình, xào với nước tương chứ không hề cay.",
                    "Phiên bản sốt ớt cay đỏ rực phổ biến từ thập niên 1950, gắn với khu phố Sindang-dong ở Seoul." } },
        };

        // Short cultural notes for the later recipes; shared by the intro and notebook.
        // References: https://vietnam.travel/things-to-do/21-must-try-vietnamese-dishes/
        // https://www.korea.net/koreanet/fileDownload?fileUrl=pdfdata%2F2016%2F01%2F2015K-Food_en_0112.pdf
        static readonly Dictionary<string, string> LaterHistory = new Dictionary<string, string>
        {
            ["goi_cuon"] = "Gỏi cuốn là món cuốn tươi quen thuộc của miền Nam, thường ăn cùng nước chấm.",
            ["gimbap"] = "Gimbap thường theo người Hàn trong những chuyến dã ngoại, tiện mang theo và chia sẻ.",
            ["tra_tac"] = "Ly trà tắc mát lạnh gắn với những quán nước vỉa hè và nhịp sống đường phố Sài Gòn.",
            ["misugaru"] = "Misugaru là thức uống từ bột ngũ cốc rang của Hàn Quốc, thường được uống mát vào mùa hè.",
            ["com_tam"] = "Cơm tấm dùng những hạt gạo vỡ, trở thành món ăn bình dân tiêu biểu của Sài Gòn.",
            ["hotteok"] = "Hotteok nóng là món ăn đường phố được yêu thích vào mùa đông Hàn Quốc.",
            ["banh_mi_thit"] = "Từ ổ baguette du nhập, người Việt tạo nên bánh mì kẹp với nhiều loại nhân và rau ăn kèm.",
            ["bulgogi"] = "Tên bulgogi có nghĩa là thịt nướng trên lửa; món thịt ướp này thường được ăn cùng cơm và rau cuốn.",
            ["banh_xeo"] = "Tên bánh xèo gợi tiếng bột reo trên chảo nóng. Món bánh này có nhiều biến thể ở miền Trung và miền Nam.",
            ["pajeon"] = "Pajeon là bánh hành của Hàn Quốc; phiên bản thêm hải sản được gọi là haemul pajeon.",
            ["tra_dao"] = "Trà đào cam sả là thức uống hiện đại quen thuộc trong các quán cà phê Việt Nam.",
            ["sikhye"] = "Sikhye là thức uống gạo ngọt truyền thống của Hàn Quốc, thường được dùng sau bữa ăn.",
            ["pho_bo"] = "Phở gắn với văn hoá ăn sáng của người Việt, rồi lan rộng với nhiều cách nêm nếm theo từng vùng.",
            ["bibimbap"] = "Bibimbap có nghĩa là cơm trộn; các phần rau nhiều màu được bày trên cơm trước khi trộn đều.",
            ["bun_cha"] = "Bún chả gắn với Hà Nội, nơi hương thịt nướng trên than hoa quen thuộc với bữa trưa phố phường.",
            ["kimchi_jjigae"] = "Kimchi jjigae là món canh hầm quen thuộc trong bữa cơm gia đình Hàn Quốc, thường dùng kimchi đã chua.",
        };

        public static string History(string id)
        {
            if (Dish.TryGetValue(id, out var entry) && entry.stories.Length > 0) return entry.stories[0];
            return LaterHistory.TryGetValue(id, out var story) ? story : GameData.Dishes[id].story;
        }

        /// <summary>Dishes the player has unlocked = every dish that appears in a level they have cleared or are on.</summary>
        public static List<string> UnlockedDishes()
        {
            var list = new List<string>();
            for (int i = 1; i <= GameData.Levels.Count; i++)
            {
                if (!Progress.Unlocked(i)) break;
                foreach (var c in GameData.Levels[i - 1].customers) foreach (var d in c) if (!list.Contains(d)) list.Add(d);
            }
            return list;
        }

        /// <summary>Next story in a round-robin over unlocked dishes (persisted), so the player rarely sees repeats.</summary>
        public static (string dish, int index, int count) NextStory()
        {
            var dishes = UnlockedDishes();
            dishes.RemoveAll(id => !Dish.ContainsKey(id) || Dish[id].stories.Length == 0);
            if (dishes.Count == 0) dishes.Add("banh_mi_cha");
            int cursor = PlayerPrefs.GetInt("bj_story_cursor", 0);
            int total = 0; foreach (var d in dishes) total += Dish[d].stories.Length;
            int k = cursor % total;
            foreach (var d in dishes)
            {
                int n = Dish[d].stories.Length;
                if (k < n)
                {
                    if (!AutoPilot.Requested) { PlayerPrefs.SetInt("bj_story_cursor", cursor + 1); PlayerPrefs.Save(); }
                    return (d, k, n);
                }
                k -= n;
            }
            return (dishes[0], 0, Dish[dishes[0]].stories.Length);
        }
    }
}
