using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BepJam
{
    /// <summary>Venba-style title screen: illustrated table background, torn-paper logo, chibi greeters and a paged
    /// calendar level select (12 days per page, swipe or arrows).</summary>
    public class HomeView : MonoBehaviour
    {
        public const int PerPage = 12;
        const float PageWidth = 800f;
        static readonly Color Ink = Pal.Hex("3B2A3A"), Cream = Pal.Hex("FBF1DE"), Red = Pal.Hex("D9414F"), Mute = Pal.Hex("8C7680");

        RectTransform root, pagesRoot;
        Text pageLabel;
        GameObject albumDot;
        readonly List<Image> dots = new List<Image>();
        readonly List<RectTransform> calendarPages = new List<RectTransform>();
        HomeLevelScrollRect levelScroll;
        int page, pageCount;
        readonly RectTransform[] greetBubbles = new RectTransform[2];
        readonly RectTransform[] greeters = new RectTransform[2];
        float greetT = 2f; int greetIdx;

        public static HomeView Create(Transform parent)
        {
            var rt = UIKit.Node("Home", parent); rt.Stretch();
            var h = rt.gameObject.AddComponent<HomeView>();
            h.root = rt;
            h.Build();
            return h;
        }

        // canvas helpers (1080 x 2336 phone, y measured from the top)
        RectTransform Top(RectTransform rt, float cx, float cy, float w, float h)
            => rt.Anchor(new Vector2(0.5f, 1f), new Vector2(cx - 540f, -cy), new Vector2(w, h));

        Text Label(Transform parent, string s, int size, Color c, float l = 8, float t = 4, float r = 8, float b = 8)
        {
            var lb = UIKit.Label("T", parent, s, size, c);
            ((RectTransform)lb.transform).Stretch(l, t, r, b);
            return UIKit.Fit(lb, size, 16);
        }

        void Build()
        {
            // ---- illustrated background (cover)
            var bg = UIKit.Img("Bg", root, Art.UI("home_bg"), false, false);
            ((RectTransform)bg.transform).Center(0, 0, 1304, 2336);
            Tween.Scale(bg.transform, Vector3.one * 1.04f, 10f).SetLoops(-1, true).SetEase(Ease.InOutSine);
            var amb = UIKit.Node("Ambient", root); amb.Stretch();
            amb.gameObject.AddComponent<AmbientDrift>().Init(amb);

            BuildTopBar();
            BuildLogo();
            Greeter(0, "c_vn_woman", 290, "Xin chào!", "speech_pink", "speech_pointer_pink", Cream);
            Greeter(1, "c_kr_woman", 790, "안녕하세요!", "speech_cream", "speech_pointer_cream", Ink);
            BuildPlay();
            BuildLevels();
        }

        void BuildTopBar()
        {
            Chip(214, "coin", Progress.Coins.ToString());
            Chip(540, "star_full", Progress.TotalStars + "/" + GameData.Levels.Count * 3);
            var gear = UIKit.Button("Settings", root, Art.FX("btn_round_cream"), null, 0, () => Popups.Settings(() => GameBoot.I.ShowHome()), Art.FX("gear"));
            Top((RectTransform)gear.transform, 962, 96, 116, 116);
            gear.icon.rectTransform.Stretch(30, 26, 30, 34);
            gear.icon.gameObject.AddComponent<Spin>().speed = 10;
        }

        void Chip(float cx, string icon, string value)
        {
            var bg = UIKit.Sliced("Chip", root, Art.FX("hud_bar"));
            var rt = Top((RectTransform)bg.transform, cx, 92, 300, 104);
            var ic = UIKit.Img("I", rt, Art.FX(icon));
            ((RectTransform)ic.transform).Anchor(new Vector2(0, 0.5f), new Vector2(64, 2), new Vector2(76, 76));
            var t = UIKit.Label("V", rt, value, 46, Ink);
            ((RectTransform)t.transform).Stretch(110, 14, 28, 18);
            UIKit.Fit(t, 46, 22);
            rt.localScale = Vector3.zero;
            Tween.Scale(rt, Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(0.2f);
        }

        void BuildLogo()
        {
            var paper = UIKit.Sliced("LogoPaper", root, Art.FX("paper_panel"));
            var prt = Top((RectTransform)paper.transform, 540, 330, 760, 300);
            var title = UIKit.Label("Title", prt, "BẾP JAM", 130, Red);
            ((RectTransform)title.transform).Stretch(40, 30, 40, 90);
            UIKit.Fit(title, 130, 60);
            var tag = UIKit.Sliced("Tag", prt, Art.FX("paper_tag"));
            var trt = ((RectTransform)tag.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0, -10), new Vector2(560, 110));
            var tt = UIKit.Label("T", trt, "Việt Nam  ♥  Hàn Quốc", 46, Cream);
            ((RectTransform)tt.transform).Stretch(70, 16, 70, 26);
            UIKit.Fit(tt, 46, 24);
            // drop in with a little tilt, tape slaps on
            var target = prt.anchoredPosition;
            prt.anchoredPosition = target + new Vector2(0, 700); prt.localEulerAngles = new Vector3(0, 0, -8);
            Tween.Move(prt, target, 0.6f).SetEase(Ease.OutBack).SetDelay(0.1f);
            Tween.Rotate(prt, 0f, 0.6f).SetEase(Ease.OutBack).SetDelay(0.1f);
            Tween.Delay(0.15f, () => Sfx.Play("paper", 0.8f));
            trt.localScale = new Vector3(0, 1, 1);
            Tween.Scale(trt, Vector3.one, 0.3f).SetEase(Ease.OutBack).SetDelay(0.65f);
            Tween.Delay(0.65f, () => Sfx.Play("tape", 0.7f));
        }

        void Greeter(int i, string id, float cx, string hello, string bubbleSprite, string pointer, Color textColor)
        {
            var g = UIKit.Node("Greeter_" + id, root);
            Top(g, cx, 810, 310, 310);
            var vis = UIKit.Node("Vis", g); vis.Stretch(); vis.pivot = new Vector2(0.5f, 0f);
            var img = UIKit.Img("Bust", vis, Art.Char(id + "_happy") ?? Art.Char(id), false, true);
            ((RectTransform)img.transform).Stretch();
            Idle.Add(vis, breathe: 0.03f, sway: 2.5f, speed: 0.9f).phase = i * 2f;
            var hit = g.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0);
            var btn = g.gameObject.AddComponent<JuicyButton>(); btn.sound = "cheer";
            btn.onClick = () => { Tween.Squash(vis, 0.25f, 0.45f); VFX.Hearts(vis.position + Vector3.up * 1.2f, 5); };
            greeters[i] = vis;

            var b = UIKit.Sliced("Bubble", g, Art.FX(bubbleSprite));
            var brt = ((RectTransform)b.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, 30), new Vector2(300, 104), new Vector2(0.5f, 0f));
            var tail = UIKit.Img("Tail", brt, Art.FX(pointer));
            ((RectTransform)tail.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0, -20), new Vector2(46, 34));
            Label(brt, hello, 42, textColor, 22, 10, 22, 16);
            brt.localScale = Vector3.zero;
            greetBubbles[i] = brt;

            g.localScale = Vector3.zero;
            Tween.Scale(g, Vector3.one, 0.5f).SetEase(Ease.OutBack).SetDelay(0.45f + i * 0.15f);
        }

        void BuildPlay()
        {
            int next = Progress.NextLevel;
            var play = UIKit.Button("Play", root, Art.FX("btn_red"), "CHƠI\nNGÀY " + next, 60, () => GameBoot.I.PlayLevel(next));
            var prt = Top((RectTransform)play.transform, 300, 1080, 440, 170);
            UIKit.CtaIcon(play, Art.FX("play"), 150);
            play.label.rectTransform.offsetMax = new Vector2(-26, play.label.rectTransform.offsetMax.y);
            UIKit.FitLines(play.label, 56, 30, 2);
            prt.localScale = Vector3.zero;
            Tween.Scale(prt, Vector3.one, 0.5f).SetEase(Ease.OutBack).SetDelay(0.8f).OnComplete(() =>
            {
                if (prt == null) return;
                play.SetBaseScale(Vector3.one);
                Tween.Scale(prt, Vector3.one * 1.05f, 0.8f).SetLoops(-1, true).SetEase(Ease.InOutSine);
            });
            BuildAlbumButton();
            var notebook = TileCta("Notebook", "Sổ tay", Art.FX("book"), () => Popups.Notebook(null), 650);
            var nrt = (RectTransform)notebook.transform;
            nrt.localScale = Vector3.zero;
            Tween.Scale(nrt, Vector3.one, 0.45f).SetDelay(0.95f).SetEase(Ease.OutBack);
        }

        /// <summary>Secondary home CTA: a square paper tile with a large icon on a cream disc and a one-line label under it.
        /// Same height as the play button; play + two tiles span the screen with equal 80 px side margins.</summary>
        JuicyButton TileCta(string name, string text, Sprite icon, System.Action click, float centreX)
        {
            const float W = 220, H = 170, Disc = 92;
            var b = UIKit.Button(name, root, Art.FX("btn_yellow"), text, 38, click, null, Ink);
            var rt = Top((RectTransform)b.transform, centreX, 1080, W, H);
            var disc = UIKit.Img("IconDisc", rt, Art.FX("btn_round_cream"));
            disc.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(Disc, Disc), new Vector2(0.5f, 1));
            if (icon != null)
            {
                b.icon = UIKit.Img("Icon", disc.transform, icon);
                b.icon.preserveAspect = true;
                b.icon.rectTransform.Stretch(Disc * 0.19f, Disc * 0.16f, Disc * 0.19f, Disc * 0.22f);
            }
            b.label.rectTransform.Anchor(new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(W - 48, 46), new Vector2(0.5f, 0));
            UIKit.Fit(b.label, 38, 24);
            return b;
        }

        void BuildAlbumButton()
        {
            var button = TileCta("Album", "Album", Resources.Load<Sprite>("Album/album_icon") ?? Art.FX("book"), OpenAlbum, 890);
            var rt = (RectTransform)button.transform;
            var dot = UIKit.Img("UnseenPhoto", rt, Art.FX("circle"));
            dot.rectTransform.Anchor(Vector2.one, new Vector2(-26, -26), new Vector2(24, 24));
            dot.color = Red; albumDot = dot.gameObject;
            RefreshAlbumDot();
            rt.localScale = Vector3.zero;
            Tween.Scale(rt, Vector3.one, 0.45f).SetDelay(0.9f).SetEase(Ease.OutBack);
        }

        void RefreshAlbumDot()
        {
            if (albumDot) albumDot.SetActive(Album.HasUnseen);
        }

        void OpenAlbum()
        {
            if (!isActiveAndEnabled) return;
            foreach (Transform popup in GameBoot.I.popupLayer)
                if (popup.gameObject.activeInHierarchy && popup.name == "Popup_Album") return;
            Popups.Album(RefreshAlbumDot);
        }

        // ------------------------------------------------------------------ paged calendar
        void BuildLevels()
        {
            pageCount = Mathf.Max(1, Mathf.CeilToInt(GameData.Levels.Count / (float)PerPage));
            int initialPage = Mathf.Clamp((Progress.NextLevel - 1) / PerPage, 0, pageCount - 1);
            page = -1;

            var panel = UIKit.Sliced("LevelPanel", root, Art.FX("paper_panel"));
            var prt = Top((RectTransform)panel.transform, 540, 1710, 960, 980);
            var title = UIKit.Label("Title", prt, "Chọn ngày", 54, Ink);
            ((RectTransform)title.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(700, 70));
            UIKit.Fit(title, 54, 30);
            pageLabel = UIKit.Label("Range", prt, "", 32, Mute);
            ((RectTransform)pageLabel.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -125), new Vector2(500, 46));
            UIKit.Fit(pageLabel, 32, 18);

            var viewport = UIKit.Node("Viewport", prt).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -540), new Vector2(PageWidth, 740));
            // Isolate the calendar from the animated background/greeters' canvas rebuilds.
            viewport.gameObject.AddComponent<Canvas>();
            viewport.gameObject.AddComponent<GraphicRaycaster>();
            viewport.gameObject.AddComponent<RectMask2D>();
            var swipeHit = viewport.gameObject.AddComponent<Image>(); swipeHit.color = new Color(1, 1, 1, 0);
            pagesRoot = UIKit.Node("Pages", viewport).Anchor(new Vector2(0, 0.5f), Vector2.zero,
                new Vector2(pageCount * PageWidth, 740), new Vector2(0, 0.5f));
            levelScroll = viewport.gameObject.AddComponent<HomeLevelScrollRect>();
            levelScroll.content = pagesRoot;
            levelScroll.viewport = viewport;
            levelScroll.horizontal = true;
            levelScroll.vertical = false;
            levelScroll.movementType = ScrollRect.MovementType.Elastic;
            levelScroll.inertia = true;
            levelScroll.decelerationRate = 0.135f;
            levelScroll.elasticity = 0.08f;
            levelScroll.scrollSensitivity = 44f;
            levelScroll.PageCount = pageCount;
            levelScroll.PageWidth = PageWidth;

            Arrow(prt, -1, new Vector2(56, -540));
            Arrow(prt, +1, new Vector2(-56, -540));
            for (int k = 0; k < pageCount; k++)
            {
                var d = UIKit.Img("Dot", prt, Art.FX("circle"), false, false);
                ((RectTransform)d.transform).Anchor(new Vector2(0.5f, 0f), new Vector2((k - (pageCount - 1) / 2f) * 44, 96), new Vector2(22, 22));
                dots.Add(d);
            }
            // Fixed rectangles, built once. Paging never creates cards or runs a layout group/fitter.
            int next = Progress.NextLevel;
            for (int p = 0; p < pageCount; p++)
            {
                var pg = UIKit.Node("Page" + p, pagesRoot).Anchor(new Vector2(0, 0.5f),
                    new Vector2(p * PageWidth, 0), new Vector2(PageWidth, 740), new Vector2(0, 0.5f));
                calendarPages.Add(pg);
                int start = p * PerPage + 1;
                int end = Mathf.Min(start + PerPage - 1, GameData.Levels.Count);
                for (int n = start; n <= end; n++)
                {
                    int j = n - start;
                    var card = Card(pg, n, n == next);
                    ((RectTransform)card.transform).anchoredPosition = new Vector2((j % 4 - 1.5f) * 196, 240 - (j / 4) * 235);
                }
            }
            // Keep the opening scroll short, even when the current day is on the final sheet.
            pagesRoot.anchoredPosition = new Vector2(-Mathf.Max(0, initialPage - 1) * PageWidth, 0);
            levelScroll.onValueChanged.AddListener(RefreshCalendar);
            RefreshCalendar(Vector2.zero);
            levelScroll.ScrollToPage(initialPage, 0.65f);
            var target = prt.anchoredPosition;
            prt.anchoredPosition = target - new Vector2(0, 1100);
            Tween.Move(prt, target, 0.7f).SetEase(Ease.OutBack).SetDelay(0.3f);
        }

        void Arrow(RectTransform panel, int dir, Vector2 pos)
        {
            var b = UIKit.Button(dir < 0 ? "Prev" : "Next", panel, Art.FX("btn_round_cream"), null, 0, () => GoPage(levelScroll.SelectedPage + dir), Art.FX("play"));
            ((RectTransform)b.transform).Anchor(new Vector2(dir < 0 ? 0f : 1f, 1f), pos, new Vector2(96, 96));
            b.icon.rectTransform.Stretch(26, 24, 22, 28);
            if (dir < 0) b.icon.rectTransform.localScale = new Vector3(-1, 1, 1);
            b.sound = null;
        }

        void GoPage(int p)
        {
            if (levelScroll.IsDragging || levelScroll.PointerHeld) return;
            if (p < 0 || p >= pageCount) { Sfx.Play("error", 0.4f); return; }
            Sfx.Play("page", 0.8f);
            levelScroll.ScrollToPage(p);
        }

        void RefreshCalendar(Vector2 unused)
        {
            float offset = -pagesRoot.anchoredPosition.x;
            // At rest only one sheet is active; while swiping, both intersecting sheets are active.
            int first = Mathf.Clamp(Mathf.FloorToInt((offset + 0.5f) / PageWidth), 0, pageCount - 1);
            int last = Mathf.Clamp(Mathf.FloorToInt((offset + PageWidth - 0.5f) / PageWidth), 0, pageCount - 1);
            for (int k = 0; k < calendarPages.Count; k++)
            {
                var sheet = calendarPages[k].gameObject;
                bool visible = k >= first && k <= last;
                if (sheet.activeSelf != visible) sheet.SetActive(visible);
            }
            int nearest = levelScroll.NearestPage;
            if (page == nearest) return;
            page = nearest;
            int start = page * PerPage + 1;
            int end = Mathf.Min(start + PerPage - 1, GameData.Levels.Count);
            pageLabel.text = $"Ngày {start}–{end}";
            for (int k = 0; k < dots.Count; k++)
            {
                dots[k].color = k == page ? Red : Pal.Hex("D8C3B1");
                dots[k].rectTransform.localScale = Vector3.one * (k == page ? 1.35f : 1f);
            }
        }

        JuicyButton Card(RectTransform parent, int n, bool isNext)
        {
            bool unlocked = Progress.Unlocked(n);
            int stars = Progress.Stars(n);
            var b = UIKit.Button("Day" + n, parent, Art.FX(unlocked ? "level_cal_cream" : "level_cal_cream_locked"), null, 0, () =>
            {
                if (Progress.Unlocked(n)) GameBoot.I.PlayLevel(n);
            });
            b.interactable = unlocked;
            b.quickPress = true;
            b.onPressChanged = levelScroll.SetPointerPressed;
            const float S = 184f;
            var rt = ((RectTransform)b.transform).Anchor(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(S, S));
            // Fixed-size labels: no best-fit text generation or outline/shadow meshes on the cards.
            var day = UIKit.Label("Ngay", rt, "NGÀY", 21, Cream);
            ((RectTransform)day.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -S * 0.33f), new Vector2(110, 30));
            var num = UIKit.Label("N", rt, n.ToString(), 54, unlocked ? Red : Pal.Hex("968C82"));
            ((RectTransform)num.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -S * 0.57f), new Vector2(120, 66));
            UIKit.Fit(day, 21, 18);
            UIKit.Fit(num, 54, 30);
            // photo level: a tiny polaroid in the top-right corner (colour once collected, faded teaser before)
            var photo = Album.ForLevel(n);
            if (photo != null)
            {
                var pol = UIKit.Img("Photo", rt, Resources.Load<Sprite>("Album/polaroid"));
                var prt = ((RectTransform)pol.transform).Anchor(new Vector2(1, 1), new Vector2(-2, 8), new Vector2(72, 82));
                prt.localRotation = Quaternion.Euler(0, 0, -12);
                var pic = UIKit.Img("Pic", prt, Resources.Load<Sprite>("Album/p" + n.ToString("00")));
                ((RectTransform)pic.transform).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(54, 54));
                bool got = Album.IsUnlocked(photo);
                pic.color = got ? Color.white : new Color(0.75f, 0.72f, 0.7f, 0.85f);
                pol.raycastTarget = pic.raycastTarget = false;
            }
            if (!unlocked)
            {
                var lk = UIKit.Img("Lock", rt, Art.FX("lock"));
                ((RectTransform)lk.transform).Anchor(new Vector2(1, 0), new Vector2(-40, 40), new Vector2(36, 36));
            }
            else
            {
                for (int s = 0; s < 3; s++)
                {
                    var st = UIKit.Img("S", rt, Art.FX(s < stars ? "star_full" : "star_empty"));
                    ((RectTransform)st.transform).Anchor(new Vector2(0.5f, 1f), new Vector2((s - 1) * 30, -S * 0.79f), new Vector2(28, 28));
                }
                if (isNext)
                {
                    var marker = UIKit.Img("CurrentDay", rt, Art.FX("circle"));
                    marker.color = Red;
                    marker.rectTransform.Anchor(new Vector2(0, 1), new Vector2(26, -26), new Vector2(16, 16));
                    // One visible reward burst; the card and its polaroid remain still.
                    if (PlayerPrefs.GetInt("bj_unlock_seen", 1) < n && !AutoPilot.Requested)
                    {
                        Tween.Delay(1.3f, () =>
                        {
                            if (!rt || !rt.gameObject.activeInHierarchy || levelScroll.IsMoving || page != (n - 1) / PerPage) return;
                            PlayerPrefs.SetInt("bj_unlock_seen", n); PlayerPrefs.Save();
                            VFX.Sparkle(rt.position, 12, 1f); Sfx.Play("unlock");
                        }, true, rt);
                    }
                }
            }
            return b;
        }

        void Update()
        {
            greetT -= Time.deltaTime;
            if (greetT > 0) return;
            greetT = 2.6f;
            int i = greetIdx++ % 2;
            var b = greetBubbles[i];
            if (b == null) return;
            Tween.KillAll(b);
            b.localScale = Vector3.zero;
            Tween.Scale(b, Vector3.one, 0.4f).SetEase(Ease.OutBack).OnComplete(() => Tween.Scale(b, Vector3.zero, 0.25f).SetEase(Ease.InBack).SetDelay(1.5f));
            if (greeters[i]) Tween.Squash(greeters[i], 0.18f, 0.4f);
        }
    }

    /// <summary>Finger-following calendar, with one owner of content motion at a time.</summary>
    public sealed class HomeLevelScrollRect : ScrollRect, IPointerDownHandler, IPointerUpHandler
    {
        const float SnapDuration = 0.35f;
        public int PageCount { get; set; } = 1;
        public float PageWidth { get; set; } = 800f;
        public bool IsDragging { get; private set; }
        public bool PointerHeld { get; private set; }
        public bool IsMoving => IsDragging || PointerHeld || snapping || settlePending || Mathf.Abs(velocity.x) > 1f;
        public int NearestPage => content ? Mathf.Clamp(Mathf.RoundToInt(-content.anchoredPosition.x / PageWidth), 0, PageCount - 1) : 0;
        public int SelectedPage => snapping ? snapPage : NearestPage;

        bool snapping, settlePending, wheelPending;
        int snapPage, dragStartPage, wheelStartPage;
        float snapFrom, snapTo, snapTime, settleTime, lastMotionTime, lastDragVelocity, wheelStartPosition;

        // Cards forward press/release only. ScrollRect keeps ownership of all drag events.
        public void SetPointerPressed(bool pressed)
        {
            PointerHeld = pressed;
            if (pressed)
            {
                snapping = settlePending = wheelPending = false;
                StopMovement();
            }
            else if (!IsDragging)
            {
                settlePending = true;
                settleTime = 0.08f;
            }
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) SetPointerPressed(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) SetPointerPressed(false);
        }

        public override void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !IsActive()) return;
            snapping = settlePending = wheelPending = false;
            StopMovement();
            IsDragging = true;
            dragStartPage = NearestPage;
            lastMotionTime = Time.unscaledTime;
            lastDragVelocity = 0;
            base.OnBeginDrag(e);
        }

        public override void OnDrag(PointerEventData e)
        {
            if (!IsDragging || e.button != PointerEventData.InputButton.Left) return;
            float before = content.anchoredPosition.x;
            base.OnDrag(e);
            float delta = content.anchoredPosition.x - before;
            if (Mathf.Abs(delta) > 0.01f)
            {
                lastMotionTime = Time.unscaledTime;
                lastDragVelocity = delta / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            }
        }

        public override void OnEndDrag(PointerEventData e)
        {
            if (!IsDragging || e.button != PointerEventData.InputButton.Left) return;
            base.OnEndDrag(e);
            IsDragging = PointerHeld = false;
            float releaseVelocity = velocity.x;
            // A short flick can finish before ScrollRect has sampled velocity in LateUpdate.
            if (Mathf.Abs(releaseVelocity) < 1f) releaseVelocity = lastDragVelocity;
            if (Time.unscaledTime - lastMotionTime > 0.1f) releaseVelocity = 0;
            float projectedPage = -content.anchoredPosition.x / PageWidth
                + Mathf.Clamp(-releaseVelocity * 0.18f / PageWidth, -0.85f, 0.85f);
            int target = Mathf.RoundToInt(projectedPage);
            if (target == dragStartPage && Mathf.Abs(releaseVelocity) > PageWidth * 0.5f)
                target += releaseVelocity < 0 ? 1 : -1;
            target = Mathf.Clamp(target, 0, PageCount - 1);
            if (target != dragStartPage) Sfx.Play("page", 0.8f);
            ScrollToPage(target);
        }

        public override void OnScroll(PointerEventData e)
        {
            if (IsDragging || PointerHeld || !IsActive()) return;
            if (!wheelPending)
            {
                wheelStartPage = NearestPage;
                wheelStartPosition = content.anchoredPosition.x;
            }
            snapping = false;
            StopMovement();
            base.OnScroll(e);
            // Wait for a pause in wheel/trackpad input before settling to a sheet.
            settlePending = true;
            wheelPending = true;
            settleTime = 0.12f;
        }

        public void ScrollToPage(int target, float delay = 0)
        {
            if (!content || IsDragging || PointerHeld) return;
            snapPage = Mathf.Clamp(target, 0, PageCount - 1);
            snapFrom = content.anchoredPosition.x;
            snapTo = -snapPage * PageWidth;
            snapTime = -Mathf.Max(0, delay);
            settlePending = wheelPending = false;
            StopMovement();
            snapping = Mathf.Abs(snapFrom - snapTo) > 0.1f;
        }

        protected override void LateUpdate()
        {
            if (snapping && !IsDragging && !PointerHeld)
            {
                snapTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(snapTime / SnapDuration);
                float eased = 1 - (1 - t) * (1 - t) * (1 - t);
                SetContentAnchoredPosition(new Vector2(Mathf.LerpUnclamped(snapFrom, snapTo, eased), content.anchoredPosition.y));
                StopMovement();
                // Still let uGUI update bounds, previous position and change events. Its elastic
                // solver must not apply a second motion when snapping back from an overscroll.
                var normalMovement = movementType;
                movementType = MovementType.Unrestricted;
                try { base.LateUpdate(); }
                finally { movementType = normalMovement; }
                if (t >= 1) snapping = false;
                return;
            }
            base.LateUpdate();
            if (!settlePending || IsDragging || PointerHeld) return;
            settleTime -= Time.unscaledDeltaTime;
            if (settleTime <= 0)
            {
                int target = NearestPage;
                float wheelTravel = content.anchoredPosition.x - wheelStartPosition;
                // A single wheel tick should turn a page instead of repeatedly snapping back.
                if (wheelPending && target == wheelStartPage && Mathf.Abs(wheelTravel) > 1f)
                    target += wheelTravel < 0 ? 1 : -1;
                ScrollToPage(target);
            }
        }

        protected override void OnDisable()
        {
            snapping = settlePending = wheelPending = IsDragging = PointerHeld = false;
            base.OnDisable();
        }
    }
}
