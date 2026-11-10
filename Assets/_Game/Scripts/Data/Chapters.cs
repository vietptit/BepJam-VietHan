using System;
using System.Collections.Generic;
using UnityEngine;

namespace BepJam
{
    /// <summary>Presentation-only itinerary. Menus, unlock days and difficulty stay in GameData.</summary>
    public static class Chapters
    {
        public const int LevelsPerChapter = 5;

        public sealed class Entry
        {
            public readonly int index; // one-based, just like level numbers and chNN resource names
            public readonly string placeId, place, hangul, location, dish;
            public readonly Country country;

            public Entry(int index, string placeId, Country country, string place, string hangul, string location, string dish)
            {
                this.index = index; this.placeId = placeId; this.country = country; this.place = place;
                this.hangul = hangul; this.location = location; this.dish = dish;
            }

            public int FirstLevel => (index - 1) * LevelsPerChapter + 1;
            public int LastLevel => index * LevelsPerChapter;
            public string CountryCode => country == Country.VN ? "vn" : "kr";
            public string Title => string.IsNullOrEmpty(hangul) ? place : place + " · " + hangul;
            public string Header => "Chương " + index + " · " + place;
            public string AlbumId => "p" + LastLevel.ToString("00");
            public string DayBackground => "BG/ch" + index.ToString("00");
            public string NightBackground => DayBackground + "_night";
        }

        public static readonly IReadOnlyList<Entry> Entries = Array.AsReadOnly(new[]
        {
            new Entry(1, "ho_guom", Country.VN, "Hồ Gươm", null, "Hà Nội · Việt Nam", "banh_mi_cha"),
            new Entry(2, "sindang", Country.KR, "Phố tteokbokki Sindang", "신당동 떡볶이 골목", "Jung-gu · Seoul, Hàn Quốc", "tteokbokki"),
            new Entry(3, "gwangjang", Country.KR, "Chợ Gwangjang", "광장시장", "Jongno · Seoul, Hàn Quốc", "gimbap"),
            new Entry(4, "ben_thanh", Country.VN, "Chợ Bến Thành", null, "Sài Gòn · Việt Nam", "com_tam"),
            new Entry(5, "biff", Country.KR, "Quảng trường BIFF", "비프광장", "Nampo-dong · Busan, Hàn Quốc", "hotteok"),
            new Entry(6, "ninh_kieu", Country.VN, "Bến Ninh Kiều", null, "Cần Thơ · Việt Nam", "banh_xeo"),
            new Entry(7, "dongnae", Country.KR, "Thành cổ Dongnae", "동래읍성", "Dongnae · Busan, Hàn Quốc", "pajeon"),
            new Entry(8, "hanoi_old_quarter", Country.VN, "Phố cổ Hà Nội", null, "Hà Nội · Việt Nam", "pho_bo"),
            new Entry(9, "jeonju_hanok", Country.KR, "Làng Hanok Jeonju", "전주한옥마을", "Jeonju · Hàn Quốc", "bibimbap"),
            new Entry(10, "gwanghwamun", Country.KR, "Quảng trường Gwanghwamun", "광화문광장", "Jongno · Seoul, Hàn Quốc", "kimchi_jjigae")
        });

        /// <summary>Returns 1..10 for levels 1..50; invalid levels are programming errors.</summary>
        public static int IndexFromLevel(int level)
        {
            if (level < 1 || level > Entries.Count * LevelsPerChapter)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Expected a level from 1 to 50.");
            return (level - 1) / LevelsPerChapter + 1;
        }

        public static Entry ForLevel(int level) => Entries[IndexFromLevel(level) - 1];

        // Use the existing titles, including boss titles, without changing the day plan.
        public static bool IsNight(LevelDef level) => level?.title != null &&
            (level.title.IndexOf("đêm", StringComparison.OrdinalIgnoreCase) >= 0 ||
             level.title.IndexOf("tối", StringComparison.OrdinalIgnoreCase) >= 0);

        public static string BackgroundPath(LevelDef level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            var chapter = ForLevel(level.number);
            return IsNight(level) ? chapter.NightBackground : chapter.DayBackground;
        }

        /// <summary>Optional art: night -> day -> null. Null keeps the original Hồ Gươm scene
        /// embedded in UI/bg_shop visible; GameView must not draw a blank Image over it.</summary>
        public static Sprite WindowBackground(LevelDef level)
        {
            if (level == null) return null;
            var sprite = Art.Get(BackgroundPath(level), warnIfMissing: false);
            if (!sprite && IsNight(level)) sprite = Art.Get(ForLevel(level.number).DayBackground, warnIfMissing: false);
            return sprite;
        }
    }
}
