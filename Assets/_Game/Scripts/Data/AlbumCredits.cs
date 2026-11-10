using System.Collections.Generic;

namespace BepJam
{
    /// <summary>Photo attribution; full titles and modification notices are in Tools/album_credits.md.</summary>
    public static class AlbumCredits
    {
        public sealed class Credit
        {
            public readonly string Author, License, LicenseUrl, CommonsUrl;
            public Credit(string author, string license, string licenseUrl, string commonsUrl)
            {
                Author = author; License = license; LicenseUrl = licenseUrl; CommonsUrl = commonsUrl;
            }
        }

        public static readonly IReadOnlyDictionary<string, Credit> Photos =
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, Credit>(new Dictionary<string, Credit>
        {
            { "p05", new Credit("서울연구원", "CC BY 4.0", "https://creativecommons.org/licenses/by/4.0", "https://commons.wikimedia.org/wiki/File:Sindang-dong_Tteokbokki_town_in_2020.jpg") },
            { "p10", new Credit("Bgag", "CC0", "http://creativecommons.org/publicdomain/zero/1.0/deed.en", "https://commons.wikimedia.org/wiki/File:Gwangjang_Market,_Seoul_01.jpg") },
            { "p15", new Credit("Diego Delso", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0", "https://commons.wikimedia.org/wiki/File:Ben_Thanh,_Ciudad_Ho_Chi_Minh,_Vietnam,_2013-08-14,_DD_01.JPG") },
            { "p20", new Credit("Christophe95", "CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0", "https://commons.wikimedia.org/wiki/File:BIFF_Square_in_Busan.jpg") },
            { "p25", new Credit("Christophe95", "CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0", "https://commons.wikimedia.org/wiki/File:Ninh_Kieu_Quay.jpg") },
            { "p30", new Credit("Oh Dong-geon", "CC BY-SA 3.0", "https://creativecommons.org/licenses/by-sa/3.0", "https://commons.wikimedia.org/wiki/File:North_Gate_of_the_Dongnae_eupseong_site.JPG") },
            { "p35", new Credit("Richard Mortel from Riyadh, Saudi Arabia", "CC BY 2.0", "https://creativecommons.org/licenses/by/2.0", "https://commons.wikimedia.org/wiki/File:Old_Quarter_street_scene,_Hanoi_(7)_(24624495318).jpg") },
            { "p40", new Credit("Bernard Gagnon", "CC0", "http://creativecommons.org/publicdomain/zero/1.0/deed.en", "https://commons.wikimedia.org/wiki/File:Jeonju_Hanok_Maeul_02.jpg") },
            { "p45", new Credit("Huntsmanleader", "CC0", "http://creativecommons.org/publicdomain/zero/1.0/deed.en", "https://commons.wikimedia.org/wiki/File:Gwanghwamun_December_2023.jpg") },
        });

        public static string Line(string id)
        {
            if (string.IsNullOrEmpty(id) || !Photos.TryGetValue(id, out var credit) || credit == null)
                return string.Empty;
            return "Ảnh: " + credit.Author + " · " + credit.License + ", Wikimedia Commons";
        }
    }
}
