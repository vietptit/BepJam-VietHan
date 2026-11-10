using System;
using System.Collections.Generic;
using UnityEngine;

namespace BepJam
{
    /// <summary>Landmark postcards earned at each chapter's end. References: Tools/chapter_art.md.</summary>
    public static class Album
    {
        public sealed class Entry
        {
            public readonly string id, country, dish, title, shortTitle, location, caption;
            public readonly int level;

            // dish is a GameData.Dishes key; its finished-dish sprite is Resources/Food/d_<dish>.
            public Entry(string id, int level, string country, string dish, string title, string shortTitle, string location, string caption)
            {
                this.id = id; this.level = level; this.country = country; this.dish = dish; this.title = title;
                this.shortTitle = shortTitle; this.location = location; this.caption = caption;
            }
        }

        public static readonly IReadOnlyList<Entry> Entries = Array.AsReadOnly(new[]
        {
            ChapterPhoto(2,
                "Sindang ở Seoul nổi tiếng với con phố tập trung nhiều quán tteokbokki. " +
                "Bánh gạo sốt cay mới học trong chương này đưa căn bếp vào nhịp ăn vặt Hàn Quốc, bên cạnh bungeoppang nhân đậu đỏ quen thuộc của những ngày lạnh. " +
                "Bạn có biết? Jeukseok tteokbokki là kiểu bánh gạo được nấu và thưởng thức ngay tại bàn, một nét đặc trưng của các quán Sindang."),
            ChapterPhoto(3,
                "Chợ Gwangjang ở Jongno là điểm hẹn ẩm thực giữa lòng Seoul, với những lối đi san sát quầy hàng. " +
                "Gimbap mini mới mở trong chương gợi món cơm cuộn nhỏ nổi tiếng của chợ, còn trà tắc mang đến chút hương vị Việt cho cuộc giao lưu. " +
                "Bạn có biết? Gwangjang còn có các quầy vải và hanbok, nên câu chuyện của chợ không chỉ nằm ở món ăn."),
            ChapterPhoto(4,
                "Tháp đồng hồ ở chợ Bến Thành là hình ảnh quen thuộc của Sài Gòn. " +
                "Cơm tấm sườn mới học trong chương gợi bữa ăn bình dân của thành phố, với sườn nướng và đồ chua bên hạt cơm nhỏ; misugaru là vị khách Hàn Quốc cùng bàn. " +
                "Bạn có biết? Tấm là những mảnh hạt gạo bị vỡ trong quá trình xay xát, làm nên tên gọi của món cơm này."),
            ChapterPhoto(5,
                "Quảng trường BIFF ở khu Nampo-dong gắn với lịch sử Liên hoan phim quốc tế Busan và những hàng ăn đường phố. " +
                "Hotteok vừa mở trong chương gợi chiếc bánh nóng nhân đường nâu và hạt rất được yêu thích ở khu vực này, bên cạnh bánh mì thịt nướng trong thực đơn giao lưu. " +
                "Bạn có biết? Phiên bản ssiat hotteok nổi tiếng của Busan được thêm nhiều loại hạt, khác với cách làm ở một số nơi khác."),
            ChapterPhoto(6,
                "Bến Ninh Kiều ở Cần Thơ nằm gần nơi sông Cần Thơ gặp sông Hậu, mở ra khung cảnh sông nước miền Tây. " +
                "Bánh xèo tôm, giá và rau sống mới học trong chương hợp với hành trình ẩm thực phương Nam, còn bulgogi góp thêm hương vị từ Hàn Quốc. " +
                "Bạn có biết? Tên bánh xèo thường được giải thích bằng tiếng bột reo lên khi chạm chảo nóng."),
            ChapterPhoto(7,
                "Những đoạn tường đá và cổng thành Dongnae gợi lại một vùng lịch sử của Busan. " +
                "Pajeon hải sản vừa mở trong chương đưa hành lá, tôm và mực lên chảo, gợi liên tưởng đến món bánh hành nổi tiếng của Dongnae, cùng ly trà đào cam sả trong thực đơn Việt – Hàn. " +
                "Bạn có biết? Cổng Bắc của thành Dongnae hiện nay là một trong những hạng mục đã được phục dựng."),
            ChapterPhoto(8,
                "Phố cổ Hà Nội có những ngõ nhỏ và phố nghề lâu đời, nơi hàng quán hòa vào nhịp sống hằng ngày. " +
                "Phở bò mới mở trong chương đưa hương nước dùng về với bữa ăn quen thuộc ở Hà Nội, trong khi sikhye mang đến một thức uống gạo từ Hàn Quốc. " +
                "Bạn có biết? Nhiều tên phố bắt đầu bằng chữ Hàng lưu lại dấu vết những mặt hàng và nghề từng tập trung ở đó."),
            ChapterPhoto(9,
                "Những mái ngói hanok và ngõ nhỏ làm nên khung cảnh đặc trưng của làng Hanok Jeonju. " +
                "Bibimbap vừa mở trong chương gắn với danh tiếng ẩm thực Jeonju, còn bún chả là món Việt cùng góp mặt trong căn bếp giao lưu. " +
                "Bạn có biết? Bibimbap có nhiều cách kể về nguồn gốc, nên khó quy về một người sáng tạo duy nhất."),
            ChapterPhoto(10,
                "Quảng trường Gwanghwamun mở ra trước cổng cung Gyeongbokgung, giữa trung tâm Seoul. " +
                "Kimchi jjigae mới học ở chương cuối gợi bữa cơm ấm áp với kimchi, thịt heo và đậu phụ; khu Gwanghwamun cũng có những quán nổi tiếng về món canh này. " +
                "Bạn có biết? Gwanghwamun là cổng chính ở phía nam của cung Gyeongbokgung, chứ không phải tên của toàn bộ cung điện."),
            new Entry("p50", 50, "vn", "kimchi_jjigae", "Làng bích họa Tam Thanh", "Tam Thanh", "Tam Kỳ · Quảng Nam, Việt Nam",
                "Làng chài Tam Thanh ở Quảng Nam trở nên rực rỡ nhờ những bức bích họa vẽ trên tường nhà, trong đó có dự án hợp tác giữa nghệ sĩ Việt Nam và Hàn Quốc. " +
                "Khép lại hành trình, ổ bánh mì chả lụa và nồi kimchi jjigae cùng nằm trên một bàn, như tình bạn Việt – Hàn qua từng món ăn. " +
                "Bạn có biết? Năm 2016, các tình nguyện viên Hàn Quốc đã cùng người dân vẽ nhiều bức tranh tường đầu tiên ở làng.")
        });

        static Entry ChapterPhoto(int index, string caption)
        {
            // Reward won at the end of chapter k-1 announces chapter k (the next place to cook in).
            var chapter = Chapters.Entries[index - 1];
            var prev = Chapters.Entries[index - 2];
            return new Entry(prev.AlbumId, prev.LastLevel, chapter.CountryCode, chapter.dish,
                chapter.Title, chapter.place, chapter.location, caption);
        }

        public static string UnlockKey(int level) => "bj_album_" + level;
        public static string SeenKey(int level) => "bj_album_seen_" + level;

        public static Entry ForLevel(int level)
        {
            foreach (var entry in Entries) if (entry.level == level) return entry;
            return null;
        }

        public static bool IsUnlocked(Entry entry) => entry != null &&
            (!AutoPilot.Requested && Progress.UnlockAll ||
             AutoPilot.Requested && qaUnlocked != null && qaUnlocked.Contains(entry.level) ||
             PlayerPrefs.GetInt(UnlockKey(entry.level), 0) > 0 || Progress.Stars(entry.level) > 0);

        public static bool IsSeen(Entry entry) => entry != null && PlayerPrefs.GetInt(SeenKey(entry.level), 0) > 0;
        public static int UnlockedCount
        {
            get { int count = 0; foreach (var entry in Entries) if (IsUnlocked(entry)) count++; return count; }
        }
        public static bool HasUnseen
        {
            get { foreach (var entry in Entries) if (IsUnlocked(entry) && !IsSeen(entry)) return true; return false; }
        }

        // Called on the win itself, even if the player quits before dismissing Win.
        // The caller must capture IsUnlocked BEFORE Progress.Complete to identify a first-time reward.
        public static void Collect(int level)
        {
            if (AutoPilot.Requested || ForLevel(level) == null) return;
            PlayerPrefs.SetInt(UnlockKey(level), 1);
            PlayerPrefs.Save();
        }

        // NEW remains until the photo's detail page is opened in the album.
        public static void MarkSeen(Entry entry)
        {
            if (AutoPilot.Requested || !IsUnlocked(entry)) return;
            PlayerPrefs.SetInt(SeenKey(entry.level), 1);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            if (AutoPilot.Requested) return;
            foreach (var entry in Entries)
            {
                PlayerPrefs.DeleteKey(UnlockKey(entry.level));
                PlayerPrefs.DeleteKey(SeenKey(entry.level));
            }
            PlayerPrefs.Save();
        }

        static HashSet<int> qaUnlocked;
        /// <summary>In-memory QA preview. No PlayerPrefs writes; disposing restores the previous preview.</summary>
        internal static IDisposable QaPreview(params int[] levels) => new PreviewScope(levels);
        sealed class PreviewScope : IDisposable
        {
            readonly HashSet<int> previous;
            readonly bool active;
            bool disposed;
            public PreviewScope(int[] levels)
            {
                active = AutoPilot.Requested;
                previous = qaUnlocked;
                if (active) qaUnlocked = new HashSet<int>(levels);
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (active) qaUnlocked = previous;
            }
        }
    }
}
