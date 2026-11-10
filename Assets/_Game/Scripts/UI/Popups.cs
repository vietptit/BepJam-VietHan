using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BepJam
{
    /// <summary>All modal popups. Animations use unscaled time; the kitchen is paused by the caller.</summary>
    public static partial class Popups
    {
        public class Handle
        {
            public RectTransform root, panel, body;
            public CanvasGroup dim;
            public bool closing;
            public void Close(Action after = null)
            {
                if (root == null || closing) return;
                closing = true;
                foreach (var b in root.GetComponentsInChildren<JuicyButton>()) b.interactable = false;
                foreach (var tw in root.GetComponentsInChildren<Typewriter>()) tw.enabled = false;
                foreach (var steam in root.GetComponentsInChildren<SteamEmitter>()) steam.enabled = false;
                Tween.KillHierarchy(root);
                Sfx.Play("paper", 0.55f);
                // the sheet is tossed away: drops and spins out of frame
                Tween.KillAll(panel);
                Tween.Move(panel, panel.anchoredPosition + new Vector2(260, -1900), 0.34f).SetEase(Ease.InBack).SetUnscaled();
                Tween.Rotate(panel, -14f, 0.34f).SetEase(Ease.InQuad).SetUnscaled();
                Tween.Fade(dim, 0f, 0.3f).SetUnscaled().OnComplete(() =>
                {
                    if (root) UnityEngine.Object.Destroy(root.gameObject);
                    after?.Invoke();
                });
            }
        }

        static float S(float a) => UIKit.S(a);

        /// <summary>Generic popup: dim + panel dropping in with a ribbon title.</summary>
        public static Handle Open(string title, float width, float height, bool ribbon = true, Color? ribbonColor = null)
        {
            var layer = GameBoot.I.popupLayer;
            var root = UIKit.Node("Popup_" + title, layer); root.Stretch();
            var dimImg = UIKit.Img("Dim", root, null, true, false);
            ((RectTransform)dimImg.transform).Stretch();
            dimImg.color = new Color(0.16f, 0.1f, 0.07f, 0.62f);
            var dim = UIKit.Group(root.gameObject);
            dim.alpha = 0;
            Tween.Fade(dim, 1, 0.25f).SetUnscaled();

            var panel = UIKit.Node("Panel", root).Center(0, 0, width, height);
            var bg = UIKit.Sliced("Bg", panel, BepJam.Art.FX("panel"));
            ((RectTransform)bg.transform).Stretch();
            var body = UIKit.Node("Body", panel); body.Stretch(40, ribbon ? 110 : 40, 40, 40);
            if (ribbon)
            {
                // Venba torn red paper tag taped across the top edge
                var rib = UIKit.Sliced("Ribbon", panel, BepJam.Art.FX("paper_tag"));
                if (ribbonColor.HasValue) rib.color = ribbonColor.Value;
                var rrt = ((RectTransform)rib.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, 14), new Vector2(Mathf.Min(width - 180, 620), 124));
                var t = UIKit.Label("Title", rrt, title, 60, Pal.Hex("FBF1DE"), TextAnchor.MiddleCenter, 0f, null, true);
                ((RectTransform)t.transform).Stretch(70, 16, 70, 28);
                UIKit.Fit(t, 60, 28);
                rrt.localScale = new Vector3(0, 1, 1);
                Tween.Scale(rrt, Vector3.one, 0.32f).SetEase(Ease.OutBack).SetDelay(0.3f).SetUnscaled();
                Tween.Delay(0.3f, () => Sfx.Play("tape", 0.7f), true, root);
            }
            // paper sheet flies up, tilted, and settles
            panel.anchoredPosition = new Vector2(0, -1400);
            panel.localEulerAngles = new Vector3(0, 0, 7f);
            Tween.Move(panel, Vector2.zero, 0.5f).SetEase(Ease.OutBack).SetUnscaled();
            Tween.Rotate(panel, 0f, 0.55f).SetEase(Ease.OutBack).SetUnscaled();
            Sfx.Play("paper", 0.85f);
            return new Handle { root = root, panel = panel, body = body, dim = dim };
        }

        static JuicyButton Btn(RectTransform parent, string text, string sprite, Vector2 pos, Vector2 size, Action click, string icon = null, float delay = 0.35f)
        {
            var b = UIKit.Button(text, parent, BepJam.Art.FX(sprite), text, 50, click);
            var rt = ((RectTransform)b.transform).Anchor(new Vector2(0.5f, 0f), pos, size);
            if (icon != null)
            {
                var ic = UIKit.Img("Icon", rt, BepJam.Art.FX(icon));
                ((RectTransform)ic.transform).Anchor(new Vector2(0, 0.5f), new Vector2(62, 6), new Vector2(76, 76));
                if (b.label) b.label.rectTransform.offsetMin = new Vector2(124, 24);
            }
            if (b.label) UIKit.Fit(b.label, 50, 26);
            rt.localScale = Vector3.zero;
            Tween.Scale(rt, Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(delay).SetUnscaled();
            return b;
        }

        static ScrollRect ScrollList(RectTransform parent, string name, float top, float bottom, float side, out RectTransform content)
        {
            var view = UIKit.Node(name, parent).Stretch(side, top, side, bottom);
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            content = UIKit.Node("Content", view);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 44;
            return scroll;
        }

        const float RecipeRowHeight = 256, RecipeStride = 280;
        static RectTransform RecipeRow(RectTransform parent, DishDef d, float y, float width, float iconSize, bool animate, float delay)
        {
            var row = UIKit.Node("Row_" + d.id, parent).Anchor(new Vector2(0.5f, 1),
                new Vector2(0, y), new Vector2(width, RecipeRowHeight), new Vector2(0.5f, 1));
            var title = UIKit.Label("DishName", row, d.name, 38, Pal.TextBrown);
            title.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -27), new Vector2(width - 32, 50));
            UIKit.FitLines(title, 38, 22, 2);
            var chip = UIKit.Sliced("Verb", row, Art.FX("pill"), d.station == StationType.None ? Pal.Mint : Pal.Butter);
            chip.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(Mathf.Min(width - 48, 420), 40));
            var ct = UIKit.Label("T", chip.transform, d.station == StationType.None ? d.verb : d.verb + " · " + GameData.StationName(d.station), 26, Pal.TextBrown);
            ct.rectTransform.Stretch(20, 2, 20, 4); UIKit.Fit(ct, 26, 16);
            var equation = UIKit.Node("Equation", row).Anchor(new Vector2(0.5f, 1), new Vector2(0, -176), new Vector2(width - 16, 144));
            RecipeUI.Draw(equation, d, width - 16, Mathf.Min(iconSize, 94), true, animate, delay, true, true);
            return row;
        }

        // ---------------------------------------------------------------- level intro
        public static void LevelIntro(LevelDef def, Action onStart, Action onHome)
        {
            // Introduction follows the level schedule, so replaying a day still teaches its new dishes.
            var seen = new HashSet<string>();
            foreach (var earlier in GameData.Levels)
            {
                if (earlier.number >= def.number) continue;
                if (!string.IsNullOrEmpty(earlier.newDish)) seen.Add(earlier.newDish);
                foreach (var customer in earlier.customers) foreach (var id in customer) seen.Add(id);
            }
            var dishes = new List<string>();
            if (!string.IsNullOrEmpty(def.newDish) && !seen.Contains(def.newDish)) dishes.Add(def.newDish);
            foreach (var customer in def.customers) foreach (var id in customer)
                if (!seen.Contains(id) && !dishes.Contains(id)) dishes.Add(id);

            const float cardHeight = 680;
            var h = Open($"Ngày {def.number}", 920, dishes.Count > 0 ? 1500 : 1100);
            h.root.name = "Popup_LevelIntro_" + def.number;
            ScrollList(h.panel, "NewRecipes", 150, 420, 48, out var menu);
            float y = 24;
            foreach (var id in dishes)
            {
                var d = GameData.Dishes[id];
                var card = UIKit.Node("New_" + id, menu).Anchor(new Vector2(0.5f, 1),
                    new Vector2(0, -y), new Vector2(800, cardHeight), new Vector2(0.5f, 1));
                var name = UIKit.Label("DishCountry", card, d.ShortName + " - " + (d.country == Country.VN ? "Việt Nam" : "Hàn Quốc"), 48, Pal.TextBrown);
                name.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(752, 60));
                UIKit.Fit(name, 48, 26);
                var description = UIKit.Label("Description", card, d.story, 30, Pal.Hex("8A6B53"));
                description.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -138), new Vector2(752, 48));
                UIKit.FitLines(description, 30, 18, 1);
                var equation = UIKit.Node("Recipe", card).Anchor(new Vector2(0.5f, 1),
                    new Vector2(0, -330), new Vector2(752, 264));
                RecipeUI.Draw(equation, d, 752, 184, true, true, 0.3f, true, true);
                var history = UIKit.Label("Culture", card, Culture.History(id), 34, Pal.TextBrown);
                history.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -566), new Vector2(752, 160));
                UIKit.FitLines(history, 34, 24, 4);
                y += cardHeight + 24;
            }
            if (dishes.Count == 0)
            {
                var title = UIKit.Label("Name", menu, def.title, 48, Pal.TextBrown);
                title.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(752, 100));
                UIKit.FitLines(title, 48, 28, 2);
                var recap = UIKit.Label("Recap", menu, "Các món hôm nay đã có trong Sổ tay ẩm thực.", 34, Pal.Hex("8A6B53"));
                recap.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(752, 100));
                UIKit.FitLines(recap, 34, 24, 2);
                y = 300;
            }
            menu.sizeDelta = new Vector2(0, y + 24);
            var facts = UIKit.Node("Facts", h.panel).Anchor(new Vector2(0.5f, 0), new Vector2(0, 310), new Vector2(820, 136));
            int dishesCount = 0; foreach (var c in def.customers) dishesCount += c.Length;
            Fact(facts, -205, "Khách", def.customers.Length + " khách · " + dishesCount + " món");
            Fact(facts, 205, "Khách VIP", (def.vip?.Count(v => v) ?? 0).ToString());
            // A text-only CTA gives the label the exact centre of the button.
            var start = Btn(h.panel, "Vào bếp!", "btn_green", new Vector2(70, 120), new Vector2(520, 150), () => h.Close(onStart), null, 0.7f);
            start.label.rectTransform.Stretch(24, 24, 24, 24);
            start.label.alignment = TextAnchor.MiddleCenter;
            var home = Btn(h.panel, "", "btn_round", new Vector2(-300, 120), new Vector2(140, 140), () => h.Close(onHome), null, 0.8f);
            var hic = UIKit.Img("Home", home.transform, Art.FX("home")); hic.rectTransform.Stretch(28, 28, 28, 28);
        }

        static void Fact(RectTransform parent, float x, string label, string value)
        {
            var bg = UIKit.Sliced("Fact", parent, BepJam.Art.FX("pill"), Pal.Hex("FDEBD0"));
            var rt = ((RectTransform)bg.transform).Anchor(new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(386, 136));
            var l = UIKit.Label("L", rt, label, 28, Pal.Hex("A08060")); ((RectTransform)l.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(338, 32)); UIKit.Fit(l, 28, 18);
            var v = UIKit.Label("V", rt, value, 38, Pal.TextBrown); ((RectTransform)v.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(338, 32)); UIKit.Fit(v, 38, 22);
        }

        // ---------------------------------------------------------------- pause
        public static void Pause(Action onResume, Action onRestart, Action onHome)
        {
            var h = Open("Tạm dừng", 820, 1060);
            var b = h.body;
            SoundToggles(b, -90);
            Btn(h.panel, "Tiếp tục", "btn_green", new Vector2(0, 530), new Vector2(560, 140), () => h.Close(onResume), "play", 0.25f);
            Btn(h.panel, "Chơi lại", "btn_yellow", new Vector2(0, 360), new Vector2(560, 140), () => h.Close(onRestart), "retry", 0.33f);
            Btn(h.panel, "Về sảnh", "btn_red", new Vector2(0, 190), new Vector2(560, 140), () => h.Close(onHome), "home", 0.41f);
        }

        public static void SoundToggles(RectTransform parent, float y)
        {
            Toggle(parent, -130, y, "music", () => AudioManager.I.MusicOn, v => AudioManager.I.MusicOn = v);
            Toggle(parent, 130, y, "sound", () => AudioManager.I.SfxOn, v => AudioManager.I.SfxOn = v);
        }

        static void Toggle(RectTransform parent, float x, float y, string icon, Func<bool> get, Action<bool> set)
        {
            var b = UIKit.Button(icon, parent, BepJam.Art.FX("btn_round"), null, 0, null, BepJam.Art.FX(icon));
            var rt = ((RectTransform)b.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(170, 170));
            var cross = UIKit.Img("Off", rt, BepJam.Art.FX("cross"));
            ((RectTransform)cross.transform).Anchor(new Vector2(1, 0), new Vector2(-18, 18), new Vector2(64, 64));
            void Refresh() { cross.enabled = !get(); b.GetComponent<Image>().color = get() ? Color.white : new Color(0.7f, 0.7f, 0.7f); }
            Refresh();
            b.onClick = () => { set(!get()); Refresh(); Tween.PunchScale(rt, Vector3.one * 0.2f, 0.3f).SetUnscaled(); };
        }

        // ---------------------------------------------------------------- recipe book
        public static void Recipes(List<string> dishes, Action onClose)
        {
            float height = Mathf.Min(420 + dishes.Count * RecipeStride, 2000);
            var p = Open("Công thức", 940, height);
            p.root.name = "Popup_Recipes";
            ScrollList(p.panel, "RecipeScroll", 130, 230, 48, out var content);
            for (int i = 0; i < dishes.Count; i++)
                RecipeRow(content, GameData.Dishes[dishes[i]], -12 - i * RecipeStride, 820, 94, true, 0.25f + Mathf.Min(i, 3) * 0.08f);
            content.sizeDelta = new Vector2(0, dishes.Count * RecipeStride + 24);
            Btn(p.panel, "Đã hiểu", "btn_green", new Vector2(0, 100), new Vector2(460, 130), () => p.Close(onClose), null, 0.4f);
        }

        // ---------------------------------------------------------------- win
        public static void Win(LevelDef def, int stars, Kitchen k, Action onNext, Action onRetry, Action onHome, BepJam.Album.Entry albumReward = null)
        {
            AudioManager.I.DuckMusic(true);
            Sfx.Play("win");
            VFX.Confetti(3.2f, 70);
            var h = Open(def.number >= GameData.Levels.Count ? "Bếp trưởng!" : "Tuyệt vời!", 900, 1260, true, Pal.Hex("6FB34A"));
            bool leaving = false;
            void Leave(Action action)
            {
                if (leaving) return;
                leaving = true;
                void Continue()
                {
                    AudioManager.I.DuckMusic(false);
                    action?.Invoke();
                }
                h.Close(() => { if (albumReward != null) AlbumReward(albumReward, Continue); else Continue(); });
            }
            var b = h.body;
            var rays = UIKit.Img("Rays", h.panel, BepJam.Art.FX("rays"), false, false);
            var rrt = ((RectTransform)rays.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(900, 900));
            rays.color = new Color(1f, 0.9f, 0.5f, 0.45f);
            rays.gameObject.AddComponent<Spin>().speed = 20;
            rrt.SetSiblingIndex(1);
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var slot = UIKit.Img("Slot", b, BepJam.Art.FX("star_empty"));
                var srt = ((RectTransform)slot.transform).Anchor(new Vector2(0.5f, 1f), new Vector2((i - 1) * 230, i == 1 ? -60 : -110), new Vector2(i == 1 ? 230 : 190, i == 1 ? 230 : 190));
                if (i < stars)
                {
                    var star = UIKit.Img("Star", srt, BepJam.Art.FX("star_full"));
                    var st = (RectTransform)star.transform; st.Stretch();
                    st.localScale = Vector3.one * 3f; UIKit.SetAlpha(star, 0);
                    float delay = 0.6f + i * 0.38f;
                    Tween.Scale(st, Vector3.one, 0.35f).SetEase(Ease.OutBack).SetDelay(delay).SetUnscaled()
                         .OnComplete(() => { Sfx.Play("star" + (idx + 1)); VFX.Stars(st.position, 14, 1.2f); VFX.Ring(st.position, Pal.Butter, 320, 0.45f); VFX.ScreenShake(10, 0.2f); });
                    Tween.Fade(star, 1, 0.15f).SetDelay(delay).SetUnscaled();
                }
            }
            var title = UIKit.Label("Msg", b, $"Đã phục vụ {k.customers.Count} vị khách!", 42, Pal.TextBrown);
            ((RectTransform)title.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -360), new Vector2(760, 70));
            UIKit.Fit(title, 42, 26);
            // coins counter
            var coinRow = UIKit.Node("Coins", b).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -510), new Vector2(640, 180));
            var cbg = UIKit.Sliced("Bg", coinRow, BepJam.Art.FX("pill"), Pal.Hex("FDEBD0")); ((RectTransform)cbg.transform).Stretch();
            var coin = UIKit.Img("Coin", coinRow, BepJam.Art.FX("coin"));
            ((RectTransform)coin.transform).Anchor(new Vector2(0, 0.5f), new Vector2(100, 0), new Vector2(144, 144));
            var ctext = UIKit.Label("N", coinRow, "+0", 96, Pal.Hex("C98A1A"), TextAnchor.MiddleCenter, 0);
            ((RectTransform)ctext.transform).Stretch(200, 24, 24, 24);
            UIKit.Fit(ctext, 96, 54);
            int target = k.coins;
            Tween.Float(0, target, 1.1f, v => { if (ctext) ctext.text = "+" + Mathf.RoundToInt(v); }, ctext).SetDelay(1.6f).SetUnscaled().SetEase(Ease.OutCubic)
                 .OnComplete(() => { if (coin) Tween.PunchScale(coin.rectTransform, Vector3.one * 0.4f, 0.4f).SetUnscaled(); Sfx.Play("coin"); });
            var stats = UIKit.Label("Stats", b, $"Chuỗi dài nhất: x{k.bestChain}   ·   Lượt chạm: {k.moves}", 30, Pal.Hex("8A6B53"));
            ((RectTransform)stats.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -675), new Vector2(800, 60));
            UIKit.Fit(stats, 30, 22);
            bool last = def.number >= GameData.Levels.Count;
            Btn(h.panel, last ? "Chơi lại" : "Màn tiếp", "btn_green", new Vector2(0, 280), new Vector2(600, 144), () => Leave(last ? onRetry : onNext), last ? "retry" : "play", 2.2f);
            Btn(h.panel, "Về sảnh", "btn_yellow", new Vector2(0, 112), new Vector2(600, 144), () => Leave(onHome), "home", 2.4f);
        }

        // ---------------------------------------------------------------- lose
        public static void Lose(LevelDef def, string reason, Kitchen k, Action onRetry, Action onHome, Action onRevive = null)
        {
            AudioManager.I.DuckMusic(true);
            Sfx.Play("lose");
            string kind = k.loseKind ?? "stuck";
            string title = kind == "vip" ? "Khách VIP bỏ về!" : kind == "empty" ? "Hết nguyên liệu!" : "Bàn đầy đĩa rồi!";
            string tip = kind == "vip" ? "Khách VIP chờ có hạn – phục vụ họ trước!"
                : kind == "empty" ? "Thử lại và chọn đúng nguyên liệu nhé!" : "Đừng đặt nguyên liệu chưa ai gọi lên đĩa nhé!";
            Color header = Pal.Hex(kind == "vip" ? "D9A441" : kind == "stuck" ? "8E4A6B" : "C9734F");
            var h = Open(title, 960, 1280, true, header);
            bool closing = false;
            void Close(Action action)
            {
                if (closing) return;
                closing = true;
                AudioManager.I.DuckMusic(false);
                h.Close(action);
            }
            var sprite = Art.FX(kind == "vip" ? "lose_left" : "lose_plates");
            var face = UIKit.Img("Illustration", h.panel, sprite ? sprite : Art.Char("bear_head"));
            var frt = face.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -310), new Vector2(380, 380));
            if (!sprite)
            {
                UIKit.Img("Eyes", frt, Art.Char("bear_eyes_sad")).rectTransform.Stretch();
                UIKit.Img("Mouth", frt, Art.Char("bear_mouth_sad")).rectTransform.Stretch();
            }
            frt.anchoredPosition += Vector2.up * 150;
            Tween.Move(frt, new Vector2(0, -310), 0.55f).SetDelay(0.25f).SetEase(Ease.OutBack).SetUnscaled().OnComplete(() =>
            {
                if (!frt || closing) return;
                Idle.Add(face, sway: 3, speed: 0.65f).unscaled = true;
                VFX.Burst(frt.TransformPoint(new Vector3(0, -150, 0)), "circle", 7,
                    new[] { new Color(0.5f, 0.46f, 0.42f, 0.45f) }, 35, 90, 16, 34, 0.6f);
                VFX.ScreenShake(4, 0.15f);
            });
            var msg = UIKit.Label("Tip", h.panel, tip, 34, Pal.TextBrown);
            msg.rectTransform.Anchor(new Vector2(0.5f, 1), new Vector2(0, -562), new Vector2(864, 44));
            UIKit.Fit(msg, 34, 24);
            var row = UIKit.Node("Stats", h.panel).Anchor(new Vector2(0.5f, 1), new Vector2(0, -680), new Vector2(840, 104));
            void Pill(float x, float width, string icon, string text, Color color)
            {
                var pill = UIKit.Sliced("Stat", row, Art.FX("pill"), Pal.Hex("FDEBD0"));
                pill.rectTransform.Center(x, 0, width, 104);
                var ic = UIKit.Img("Icon", pill.transform, Art.FX(icon));
                ic.rectTransform.Anchor(new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(48, 48));
                var label = UIKit.Label("Value", pill.transform, text, 34, color);
                label.rectTransform.Stretch(88, 24, 24, 24);
                UIKit.Fit(label, 34, 20);
            }
            Pill(-170, 480, "heart", $"Đã phục vụ {k.served}/{k.TotalServedTarget}", Pal.TextBrown);
            Pill(260, 300, "coin", $"+{k.coins}", Pal.Hex("C98A1A"));
            bool revive = k.CanRevive && onRevive != null;
            if (revive)
                Btn(h.panel, $"Trả 1 đĩa & chơi tiếp ({k.returnsLeft})", "btn_green", new Vector2(0, 400), new Vector2(800, 136), () => Close(onRevive), null, 0.55f);
            Btn(h.panel, "Thử lại", revive ? "btn_yellow" : "btn_green", new Vector2(0, revive ? 240 : 320), new Vector2(800, 136), () => Close(onRetry), "retry", 0.65f);
            var home = Btn(h.panel, "Về sảnh", "btn_yellow", new Vector2(0, revive ? 80 : 160), new Vector2(800, 136), () => Close(onHome), "home", 0.75f);
            UIKit.Fit(home.label, 50, 26);
        }
    }

    public class Spin : MonoBehaviour
    {
        public float speed = 30f;
        void Update() { if (!GameView.MotionPaused(this)) transform.Rotate(0, 0, -speed * Time.unscaledDeltaTime); }
    }
}
