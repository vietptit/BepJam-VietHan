using System;
using UnityEngine;
using UnityEngine.UI;
using AlbumData = BepJam.Album;

namespace BepJam
{
    public static partial class Popups
    {
        static readonly Color AlbumPaper = Pal.Hex("FFF6E5"), AlbumGrey = Pal.Hex("B2ADA8");

        static Text AlbumText(RectTransform parent, string name, string text, int size, Color color,
            float y, float width, float height, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var label = UIKit.Label(name, parent, text, size, color, align);
            label.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(width, height));
            label.supportRichText = false;
            return UIKit.Fit(label, size, 22);
        }

        static Handle AlbumSheet(string title, float width, float height)
        {
            var h = Open(title, width, height);
            var ribbonTitle = h.panel.Find("Ribbon/Title").GetComponent<Text>();
            ribbonTitle.rectTransform.Stretch(48, 24, 48, 24);
            UIKit.Fit(ribbonTitle, 60, 28);
            var bg = h.panel.Find("Bg").GetComponent<Image>();
            bg.sprite = Art.FX("paper_panel");
            if (!bg.sprite) bg.color = AlbumPaper;
            bg.raycastTarget = true;
            h.root.Find("Dim").GetComponent<Image>().color = new Color(0.12f, 0.09f, 0.13f, 0.82f);
            // A small, quiet page entrance leaves the individual photographs free to move.
            Tween.KillAll(h.panel);
            h.panel.anchoredPosition = new Vector2(0, -64);
            h.panel.localEulerAngles = new Vector3(0, 0, 2);
            Tween.Move(h.panel, Vector2.zero, 0.42f).SetEase(Ease.OutCubic).SetUnscaled();
            Tween.Rotate(h.panel, 0, 0.42f).SetEase(Ease.OutCubic).SetUnscaled();
            return h;
        }

        static Action AlbumClose(Handle h, Action after)
        {
            bool closing = false;
            return () =>
            {
                if (closing || !h.root) return;
                closing = true;
                h.Close(after);
            };
        }

        static JuicyButton AlbumButton(RectTransform parent, string text, string sprite, float bottom,
            float width, float height, Action click, float delay = 0.4f, string icon = "check")
        {
            var b = PaperBtn(parent, text, sprite, bottom, width, height, click, icon, delay);
            if (icon == null) b.label.rectTransform.Stretch(32, 24, 32, 24);
            UIKit.Fit(b.label, 44, 24);
            return b;
        }

        static ScrollRect AlbumScroll(RectTransform parent, string name, float top, float bottom, out RectTransform content)
        {
            var view = UIKit.Node(name, parent).Stretch(48, top, 48, bottom);
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            content = UIKit.Node("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 44; scroll.decelerationRate = 0.12f;
            return scroll;
        }

        static void AlbumCaption(RectTransform panel, AlbumData.Entry entry, float top, float bottom)
        {
            var scroll = AlbumScroll(panel, "CaptionScroll", top, bottom, out var content);
            float width = panel.sizeDelta.x - 96 - 64;
            const int fontSize = 34;
            var text = UIKit.Label("Caption", content, entry.caption, fontSize, CInk, TextAnchor.UpperLeft);
            text.supportRichText = false;
            UIKit.Fit(text, fontSize, fontSize);
            // Measure the entire paragraph at a readable size; the viewport scrolls rather than truncating it.
            var settings = text.GetGenerationSettings(new Vector2(width, 0));
            settings.resizeTextForBestFit = false;
            float textHeight = Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredHeight(entry.caption, settings) / text.pixelsPerUnit) + 16;
            float viewHeight = panel.sizeDelta.y - top - bottom;
            content.sizeDelta = new Vector2(0, Mathf.Max(viewHeight, textHeight + 48));
            text.rectTransform.anchorMin = new Vector2(0, 1); text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 1);
            text.rectTransform.sizeDelta = new Vector2(-64, textHeight);
            text.rectTransform.anchoredPosition = new Vector2(0, -24);
            scroll.verticalNormalizedPosition = 1;
        }

        static void AlbumCountry(RectTransform panel, AlbumData.Entry entry, float y)
        {
            var chip = UIKit.Sliced("Country", panel, Art.FX("pill"), Pal.Hex("EEDFC7"));
            chip.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(320, 96));
            var flag = UIKit.Node("Flag_" + entry.country, chip.transform).Anchor(new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(66, 44));
            flag.gameObject.AddComponent<CanvasRenderer>(); flag.gameObject.AddComponent<AlbumFlagGraphic>().Init(entry.country);
            var label = UIKit.Label("CountryName", chip.transform, entry.country == "vn" ? "VIỆT NAM" : "HÀN QUỐC", 28, CInk);
            label.rectTransform.Stretch(118, 24, 24, 24);
            UIKit.Fit(label, 28, 22);
        }

        static void AlbumPhotoCredit(RectTransform panel, AlbumData.Entry entry, float y)
        {
            var credit = AlbumCredits.Line(entry?.id);
            if (string.IsNullOrEmpty(credit)) return;
            var label = AlbumText(panel, "PhotoCredit", credit, 24, CMute,
                y, panel.sizeDelta.x - 144, 64);
            UIKit.Fit(label, 24, 18);
        }

        static void AlbumDishChip(RectTransform parent, AlbumData.Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.dish)) return;
            // Finished dishes use d_ to distinguish them from ingredient sprites with the same id.
            var sprite = Resources.Load<Sprite>("Food/d_" + entry.dish);
            if (!sprite) return;
            var chip = UIKit.Sliced("DishChip_" + entry.dish, parent, Art.FX("pill"), AlbumPaper);
            chip.rectTransform.Anchor(Vector2.zero, new Vector2(48, 44), new Vector2(72, 64));
            var icon = UIKit.Img("DishIcon", chip.transform, sprite);
            icon.rectTransform.Stretch(8, 6, 8, 6);
        }

        static RectTransform AlbumPolaroid(Transform parent, AlbumData.Entry entry, bool unlocked,
            float width, float height, out Image photo, out Image white)
        {
            var card = UIKit.Node("Photo_" + entry.id, parent).Center(0, 0, width, height);
            var space = UIKit.Node("ArtSpace", card).Center(0, 0, 440, 512);
            space.localScale = Vector3.one * Mathf.Min(width / 440, height / 512);
            // The supplied 440x512 frame has an 8.4-degree tilt baked into its pixels.
            // Deskew it first so the book's alternating +/-3-degree tilts are the final angles.
            space.localEulerAngles = new Vector3(0, 0, -8.4f);
            var frame = UIKit.Img("Polaroid", space, Resources.Load<Sprite>("Album/polaroid"), false, false);
            frame.rectTransform.Stretch();
            if (!frame.sprite)
            {
                frame.rectTransform.Center(0, -10, 370, 436);
                frame.rectTransform.localEulerAngles = new Vector3(0, 0, 8.4f);
                frame.color = AlbumPaper;
            }
            var print = UIKit.Node("Print", space).Center(-5, 21, 308, 308);
            print.localEulerAngles = new Vector3(0, 0, 8.4f);
            var backing = UIKit.Img("PhotoPaper", print, null, false, false);
            backing.rectTransform.Stretch(); backing.color = unlocked ? Color.white : Pal.Hex("D3CFCA");
            photo = UIKit.Img("Illustration", print, unlocked ? Resources.Load<Sprite>("Album/" + entry.id) : null);
            photo.rectTransform.Stretch();
            // Missing art still gives a clean, readable paper card.
            photo.enabled = photo.sprite != null;
            if (!unlocked)
            {
                var silhouette = UIKit.Node("Silhouette", print).Stretch(24, 24, 24, 24);
                var landscape = silhouette.gameObject.AddComponent<Image>(); landscape.sprite = Resources.Load<Sprite>("Album/p" + entry.level.ToString("00"));
                landscape.preserveAspect = true; landscape.color = new Color(0.55f, 0.55f, 0.55f, 0.35f); landscape.raycastTarget = false;
                var lockIcon = UIKit.Img("Lock", print, Art.FX("lock"));
                lockIcon.rectTransform.Center(0, 12, 80, 80);
                if (!lockIcon.sprite)
                {
                    lockIcon.enabled = false;
                    var fallback = UIKit.Label("LockFallback", print, "?", 64, CInk);
                    fallback.rectTransform.Center(0, 12, 80, 80); UIKit.Fit(fallback, 64, 32);
                }
            }
            // Shared by grid cards, detail pages and rewards; keep locked photos a surprise.
            if (unlocked) AlbumDishChip(print, entry);
            white = UIKit.Img("DevelopWhite", print, null, false, false);
            white.rectTransform.Stretch(); white.color = new Color(1, 1, 1, 0);
            var title = UIKit.Label("ShortTitle", print, unlocked ? entry.shortTitle : "Ảnh chưa mở", 30, unlocked ? CInk : CMute);
            title.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(0, -48), new Vector2(278, 40));
            UIKit.Fit(title, 30, 18);
            return card;
        }

        /// <summary>Home's photo book. A photo stays NEW until its detail page is opened.</summary>
        public static void Album(Action onClose = null)
        {
            const float W = 980, H = 2020;
            var h = AlbumSheet("Album Việt – Hàn", W, H);
            h.root.name = "Popup_Album";
            var spine = UIKit.Img("BookSpine", h.panel, null, false, false);
            spine.rectTransform.Stretch(24, 144, W - 32, 220); spine.color = Pal.Hex("D6BFA2");
            var fold = UIKit.Img("PageFold", h.panel, null, false, false);
            fold.rectTransform.Stretch(W / 2 - 3, 300, W / 2 - 3, 264);
            fold.color = new Color(0.64f, 0.48f, 0.35f, 0.13f);
            AlbumText(h.panel, "Progress", AlbumData.UnlockedCount + "/" + AlbumData.Entries.Count + " ảnh", 42, CRed, 150, W - 144, 60);
            AlbumText(h.panel, "Hint", "Thắng màn cuối chương để lưu ảnh địa điểm", 30, CMute, 228, W - 144, 48);
            AlbumScroll(h.panel, "PhotoGrid", 300, 264, out var content);
            const float cellW = 388, cellH = 516, gapX = 40, gapY = 32, pad = 32;
            int index = 0;
            foreach (var entry in AlbumData.Entries)
            {
                var item = entry;
                bool unlocked = AlbumData.IsUnlocked(item);
                var cell = UIKit.Node("Cell_" + item.id, content).Anchor(new Vector2(0.5f, 1),
                    new Vector2((index % 2 == 0 ? -1 : 1) * (cellW + gapX) / 2, -pad - cellH / 2 - index / 2 * (cellH + gapY)), new Vector2(cellW, cellH));
                var card = AlbumPolaroid(cell, item, unlocked, 376, 420, out _, out _);
                card.anchoredPosition = new Vector2(0, 40);
                card.localEulerAngles = new Vector3(0, 0, index % 2 == 0 ? -3 : 3);
                Image badge = null;
                if (unlocked)
                {
                    badge = UIKit.Sliced("NEW", cell, Art.FX("pill"), CRed);
                    badge.rectTransform.Anchor(new Vector2(1, 1), new Vector2(-80, -68), new Vector2(112, 76));
                    var label = UIKit.Label("NewLabel", badge.transform, "NEW", 25, CCream);
                    label.rectTransform.Stretch(24, 24, 24, 24); UIKit.Fit(label, 25, 18);
                    badge.gameObject.SetActive(!AlbumData.IsSeen(item));
                }
                AlbumText(cell, "UnlockHint", unlocked ? "Chạm để xem" : "Thắng màn " + item.level + " để mở", 28,
                    unlocked ? CMute : CInk, cellH - 48, cellW - 48, 48);
                var tap = cell.gameObject.AddComponent<Image>(); tap.color = Color.clear; tap.raycastTarget = true;
                // Keep scroll dragging available on locked cards; only unlocked cards are clickable.
                if (unlocked)
                {
                    var button = cell.gameObject.AddComponent<JuicyButton>(); button.sound = "page";
                    var newBadge = badge;
                    button.onClick = () =>
                    {
                        AlbumDetail(item);
                        if (newBadge) newBadge.gameObject.SetActive(!AlbumData.IsSeen(item));
                    };
                }
                var group = UIKit.Group(card.gameObject); group.alpha = 0;
                Tween.Fade(group, 1, 0.4f).SetDelay(0.12f + index * 0.035f).SetUnscaled();
                index++;
            }
            int rows = (index + 1) / 2;
            content.sizeDelta = new Vector2(0, pad * 2 + rows * cellH + Mathf.Max(0, rows - 1) * gapY);
            AlbumButton(h.panel, "Đóng", "btn_green", 80, 380, 128, AlbumClose(h, onClose));
            Sfx.Play("page", 0.7f);
        }

        static void AlbumDetail(AlbumData.Entry entry)
        {
            if (!AlbumData.IsUnlocked(entry)) return;
            AlbumData.MarkSeen(entry);
            const float W = 940, H = 1980;
            var h = AlbumSheet("Dấu ấn hành trình", W, H);
            h.root.name = "Popup_AlbumDetail_" + entry.id;
            var card = AlbumPolaroid(h.panel, entry, true, 620, 700, out _, out _);
            card.Anchor(new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(620, 700));
            card.localEulerAngles = new Vector3(0, 0, -2);
            AlbumCountry(h.panel, entry, 910);
            AlbumText(h.panel, "Title", entry.title, 46, CInk, 1044, W - 144, 124);
            AlbumText(h.panel, "Location", entry.location, 31, CMute, 1160, W - 144, 56);
            AlbumPhotoCredit(h.panel, entry, 1230);
            AlbumCaption(h.panel, entry, 1280, 264);
            AlbumButton(h.panel, "Đóng", "btn_green", 80, 380, 128, AlbumClose(h, null));
            Sfx.Play("page", 0.65f);
        }

        /// <summary>Shown once on leaving a first-time photo-level Win. Closing the book also resumes that action.</summary>
        public static void AlbumReward(AlbumData.Entry entry, Action onClose)
        {
            if (entry == null) { onClose?.Invoke(); return; }
            const float W = 940, H = 2100;
            var h = AlbumSheet("Ảnh mới cho Album!", W, H);
            h.root.name = "Popup_AlbumReward_" + entry.id;
            var card = AlbumPolaroid(h.panel, entry, true, 620, 700, out var photo, out var white);
            var target = new Vector2(0, -460);
            card.Anchor(new Vector2(0.5f, 1), target + new Vector2(-780, 180), new Vector2(620, 700));
            card.localEulerAngles = new Vector3(0, 0, 338);
            card.localScale = Vector3.one * 0.65f;
            Tween.Move(card, target, 0.8f).SetEase(Ease.OutCubic).SetDelay(0.2f).SetUnscaled();
            Tween.Rotate(card, 358, 0.85f).SetEase(Ease.OutCubic).SetDelay(0.2f).SetUnscaled();
            Tween.Scale(card, Vector3.one, 0.85f).SetEase(Ease.OutCubic).SetDelay(0.2f).SetUnscaled();
            // A white exposure and a soft scale/alpha reveal imitate a developing print, without a blur shader.
            white.color = Color.white;
            UIKit.SetAlpha(photo, 0);
            photo.rectTransform.localScale = Vector3.one * 0.94f;
            Tween.Fade(photo, 1, 1.1f).SetDelay(0.85f).SetUnscaled();
            Tween.Scale(photo.rectTransform, Vector3.one, 1.1f).SetDelay(0.85f).SetEase(Ease.OutSine).SetUnscaled();
            Tween.Fade(white, 0, 1.1f).SetDelay(0.85f).SetUnscaled();
            Tween.Float(0, 1, 0, _ => { }, card).SetDelay(1.5f).SetUnscaled().OnComplete(() =>
            {
                if (!h.root) return;
                Sfx.Play("star3", 0.85f);
                VFX.Sparkle(card.position, 18, 1.2f);
                VFX.Confetti(0.8f, 18);
            });
            AlbumCountry(h.panel, entry, 904);
            AlbumText(h.panel, "Title", entry.title, 46, CInk, 1038, W - 144, 124);
            AlbumText(h.panel, "Location", entry.location, 31, CMute, 1154, W - 144, 56);
            var nextChapter = entry.level < GameData.Levels.Count ? Chapters.ForLevel(entry.level + 1) : null;
            AlbumPhotoCredit(h.panel, entry, 1224);
            AlbumCaption(h.panel, entry, 1274, nextChapter != null ? 640 : 432);
            if (nextChapter != null)
            {
                AlbumText(h.panel, "NextDestinationHeader", "Điểm đến tiếp theo!", 38, CRed, 1516, W - 144, 52);
                AlbumText(h.panel, "NextDestination", nextChapter.Header, 34, CInk, 1604, W - 144, 100);
            }
            bool closing = false;
            void Finish(bool viewAlbum)
            {
                if (closing || !h.root) return;
                closing = true;
                h.Close(() => { if (viewAlbum) Album(onClose); else onClose?.Invoke(); });
            }
            AlbumButton(h.panel, "Cất vào Album", "btn_green", 240, 640, 128, () => Finish(false), 1.8f);
            AlbumButton(h.panel, "Xem Album", "btn_yellow", 80, 640, 128, () => Finish(true), 1.9f, "book");
            Sfx.Play("win", 0.5f);
        }
    }

    /// <summary>Small drawn flag chips; never depend on platform emoji fonts.</summary>
    sealed class AlbumFlagGraphic : MaskableGraphic
    {
        string country;
        public void Init(string value) { country = value; raycastTarget = false; SetVerticesDirty(); }
        Vector2 P(float x, float y) => new Vector2(rectTransform.rect.xMin + x * rectTransform.rect.width, rectTransform.rect.yMin + y * rectTransform.rect.height);
        void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
        }
        void Rect(VertexHelper vh, float x, float y, float w, float h, Color tint)
        {
            Tri(vh, P(x, y), P(x, y + h), P(x + w, y + h), tint);
            Tri(vh, P(x, y), P(x + w, y + h), P(x + w, y), tint);
        }
        void Arc(VertexHelper vh, float x, float y, float radius, float from, float to, Color tint)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = Mathf.Lerp(from, to, i / 24f) * Mathf.Deg2Rad, b = Mathf.Lerp(from, to, (i + 1) / 24f) * Mathf.Deg2Rad;
                // Height compensation keeps circles round in a 3:2 flag rectangle.
                Tri(vh, P(x, y), P(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius * 1.5f),
                    P(x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius * 1.5f), tint);
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (country == "vn")
            {
                Rect(vh, 0, 0, 1, 1, Pal.Hex("DA251D"));
                for (int i = 0; i < 10; i++)
                {
                    float a = (90 + i * 36) * Mathf.Deg2Rad, b = (90 + (i + 1) * 36) * Mathf.Deg2Rad;
                    float ra = i % 2 == 0 ? 0.23f : 0.09f, rb = i % 2 == 0 ? 0.09f : 0.23f;
                    Tri(vh, P(0.5f, 0.5f), P(0.5f + Mathf.Cos(a) * ra, 0.5f + Mathf.Sin(a) * ra * 1.5f),
                        P(0.5f + Mathf.Cos(b) * rb, 0.5f + Mathf.Sin(b) * rb * 1.5f), Pal.Hex("FFFF00"));
                }
            }
            else
            {
                Rect(vh, 0, 0, 1, 1, Color.white);
                var red = Pal.Hex("CD2E3A"); var blue = Pal.Hex("0047A0");
                Arc(vh, 0.5f, 0.5f, 0.21f, 0, 180, red); Arc(vh, 0.5f, 0.5f, 0.21f, 180, 360, blue);
                Arc(vh, 0.395f, 0.5f, 0.105f, 0, 360, red); Arc(vh, 0.605f, 0.5f, 0.105f, 0, 360, blue);
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = corner % 2 == 0 ? 0.08f : 0.76f, y = corner < 2 ? 0.72f : 0.13f;
                    for (int bar = 0; bar < 3; bar++)
                    {
                        bool split = corner == 3 || corner == 1 && bar != 1 || corner == 2 && bar == 1;
                        if (split) { Rect(vh, x, y + bar * 0.055f, 0.065f, 0.026f, CBlack); Rect(vh, x + 0.095f, y + bar * 0.055f, 0.065f, 0.026f, CBlack); }
                        else Rect(vh, x, y + bar * 0.055f, 0.16f, 0.026f, CBlack);
                    }
                }
            }
        }
        static readonly Color CBlack = Pal.Hex("29252A");
    }

    /// <summary>Neutral landscape silhouette for locked photographs.</summary>
    sealed class AlbumLandscapeGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Vector2 Point(float x, float y) => new Vector2(r.xMin + r.width * x, r.yMin + r.height * y);
            void Triangle(Vector2 a, Vector2 b, Vector2 c)
            {
                int n = vh.currentVertCount;
                vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2);
            }
            Triangle(Point(0, 0.12f), Point(0.35f, 0.65f), Point(0.72f, 0.12f));
            Triangle(Point(0.32f, 0.12f), Point(0.72f, 0.8f), Point(1, 0.12f));
        }
    }
}
