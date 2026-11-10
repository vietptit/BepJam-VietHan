using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BepJam
{
    /// <summary>Venba-style culture & settings popups with intro animation + sound:
    /// Settings, "Chuyện bên mâm" (food story), the recipe notebook and a confirm dialog.</summary>
    public static partial class Popups
    {
        static readonly Color CInk = Pal.Hex("3B2A3A"), CCream = Pal.Hex("FBF1DE"), CRed = Pal.Hex("D9414F"), CMute = Pal.Hex("8C7680");
        const float BottomPad = 120f;   // every button keeps at least this much paper below it

        static Text Txt(RectTransform parent, string s, int size, Color c, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align = TextAnchor.MiddleCenter, int min = 18)
        {
            var t = UIKit.Label("T", parent, s, size, c, align);
            ((RectTransform)t.transform).Anchor(anchor, pos, box);
            return UIKit.Fit(t, size, min);
        }

        static JuicyButton PaperBtn(RectTransform parent, string text, string sprite, float yFromBottom, float w, float h, Action click, string icon = null, float delay = 0.5f)
        {
            var b = UIKit.Button(text, parent, BepJam.Art.FX(sprite), text, 48, click);
            var rt = ((RectTransform)b.transform).Anchor(new Vector2(0.5f, 0f), new Vector2(0, yFromBottom + h / 2f), new Vector2(w, h));
            if (icon != null)
            {
                var plate = UIKit.Img("IconPlate", rt, BepJam.Art.FX("btn_round_cream"));
                ((RectTransform)plate.transform).Anchor(new Vector2(0, 0.5f), new Vector2(64, 6), new Vector2(h * 0.72f, h * 0.72f));
                var ic = UIKit.Img("Icon", plate.transform, BepJam.Art.FX(icon));
                ((RectTransform)ic.transform).Stretch(14, 12, 14, 18);
                b.label.rectTransform.offsetMin = new Vector2(64 + h * 0.5f + 24, b.label.rectTransform.offsetMin.y);
            }
            rt.localScale = Vector3.zero;
            Tween.Scale(rt, Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(delay).SetUnscaled();
            return b;
        }

        /// <summary>Reveal text letter by letter (pen or typewriter sound). Tapping the popup finishes it instantly.</summary>
        public class Typewriter : MonoBehaviour
        {
            public Text text; public string full; public float cps = 40f; public string sound = "type";
            float t; int shown = -1; public bool Done => shown >= full.Length;
            AudioSource loop;
            public void Begin(float delay) { if (!text) text = GetComponent<Text>(); full = full ?? ""; t = -delay; text.text = ""; shown = 0; }
            public void Finish() { if (!text) text = GetComponent<Text>(); shown = full.Length; text.text = full; StopLoop(); }
            void StopLoop() { if (loop != null && AudioManager.I != null) AudioManager.I.StopLoop(loop, 0.1f); loop = null; }
            void Update()
            {
                if (shown < 0 || Done) return;
                t += Time.unscaledDeltaTime;
                if (t < 0) return;
                if (sound == "pen" && loop == null) loop = AudioManager.I.StartLoop("pen", 0.45f);
                int target = Mathf.Min(full.Length, Mathf.FloorToInt(t * cps));
                if (target > shown)
                {
                    if (sound == "type" && full[Mathf.Min(target - 1, full.Length - 1)] != ' ') Sfx.Play("type", 0.35f, UnityEngine.Random.Range(0.9f, 1.15f));
                    shown = target; text.text = full.Substring(0, shown);
                }
                if (Done) StopLoop();
            }
            void OnDisable() => StopLoop();
            void OnDestroy() => StopLoop();
        }

        // ================================================================== SETTINGS
        public static void Settings(Action onClose)
        {
            const float W = 860, H = 1260;
            var h = Open("Cài đặt", W, H);
            var p = h.panel;
            var rows = new (string icon, string label, Func<bool> get, Action<bool> set)[]
            {
                ("music", "Nhạc nền", () => AudioManager.I.MusicOn, v => AudioManager.I.MusicOn = v),
                ("sound", "Âm thanh", () => AudioManager.I.SfxOn, v => AudioManager.I.SfxOn = v),
                ("heart", "Rung", () => Haptics.On, v => { Haptics.On = v; if (v) Haptics.Buzz(); }),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i]; float y = -190 - i * 150;
                var row = UIKit.Node("Row" + i, p).Anchor(new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(W - 160, 120));
                var ic = UIKit.Img("I", row, BepJam.Art.FX(r.icon)); ((RectTransform)ic.transform).Anchor(new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(80, 80));
                Txt(row, r.label, 46, CInk, new Vector2(0, 0.5f), new Vector2(260, 0), new Vector2(300, 80), TextAnchor.MiddleLeft);
                var tog = UIKit.Button("Toggle", row, BepJam.Art.FX(r.get() ? "btn_teal" : "btn_plum"), r.get() ? "BẬT" : "TẮT", 36, null);
                var trt = ((RectTransform)tog.transform).Anchor(new Vector2(1, 0.5f), new Vector2(-100, 0), new Vector2(190, 90));
                tog.sound = null;
                tog.onClick = () =>
                {
                    bool v = !r.get();
                    Sfx.Play("toggle", 0.8f);
                    // flip like a card, swap face at the middle
                    Tween.Scale(trt, new Vector3(0, 1, 1), 0.1f).SetUnscaled().OnComplete(() =>
                    {
                        r.set(v);
                        tog.GetComponent<Image>().sprite = BepJam.Art.FX(v ? "btn_teal" : "btn_plum");
                        tog.label.text = v ? "BẬT" : "TẮT";
                        Tween.Scale(trt, Vector3.one, 0.18f).SetEase(Ease.OutBack).SetUnscaled();
                    });
                };
                // rows slide in one after another
                row.anchoredPosition += new Vector2(-900, 0);
                Tween.Move(row, row.anchoredPosition + new Vector2(900, 0), 0.4f).SetEase(Ease.OutBack).SetDelay(0.35f + i * 0.08f).SetUnscaled();
            }
            PaperBtn(p, "Chơi lại từ đầu", "btn_red", BottomPad + 140 + 70, 560, 116, () => Confirm("Xoá toàn bộ tiến trình?", "Sao, xu, Album và các món đã mở sẽ mất.", () =>
            {
                BepJam.Album.Reset();
                Progress.ResetTips();
                for (int i = 1; i <= GameData.Levels.Count; i++) PlayerPrefs.DeleteKey("bj_stars_" + i);
                PlayerPrefs.DeleteKey("bj_coins"); PlayerPrefs.DeleteKey("bj_story_cursor"); PlayerPrefs.DeleteKey("bj_unlock_seen"); PlayerPrefs.Save();
                h.Close(onClose);
            }), "retry", 0.66f);
            Txt(p, "Bếp Jam: Việt – Hàn · v1.0", 30, CMute, new Vector2(0.5f, 0f), new Vector2(0, BottomPad + 130 + 40), new Vector2(700, 44));
            PaperBtn(p, "Đóng", "btn_green", BottomPad, 380, 120, () => h.Close(onClose), null, 0.72f);
        }

        public static void Confirm(string title, string message, Action onYes)
        {
            var h = Open("Xác nhận", 800, 720);
            Txt(h.panel, title, 46, CInk, new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(680, 70));
            Txt(h.panel, message, 34, CMute, new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(680, 110));
            var no = PaperBtn(h.panel, "Không", "btn_green", BottomPad, 300, 116, () => h.Close(), null, 0.4f);
            ((RectTransform)no.transform).anchoredPosition += new Vector2(-170, 0);
            var yes = PaperBtn(h.panel, "Xoá", "btn_red", BottomPad, 300, 116, () => h.Close(onYes), null, 0.45f);
            ((RectTransform)yes.transform).anchoredPosition += new Vector2(170, 0);
        }

        // ================================================================== BOOSTER INTRO
        public static void BoosterIntro(string id, Action onDone)
        {
            string icon = id == "hint" ? "bulb" : id == "return" ? "undo" : "book";
            string name = id == "hint" ? "Gợi ý" : id == "return" ? "Trả đĩa" : "Công thức";
            string desc = id == "hint" ? "Chỉ cho bạn khay nên chạm tiếp theo. Mỗi ngày có 3 lượt."
                        : id == "return" ? "Cất nguyên liệu đang chờ trên đĩa về lại kho, giải phóng 1 đĩa. Mỗi ngày có 3 lượt."
                        : "Xem lại công thức của mọi món khách gọi hôm nay – dùng thoải mái!";
            const float W = 860, H = 1180;
            var h = Open("Trợ giúp mới!", W, H);
            var p = h.panel;
            var stamp = UIKit.Img("Stamp", p, BepJam.Art.FX("ribbon_stamp"));
            var srt = ((RectTransform)stamp.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -360), new Vector2(330, 330));
            var disc = UIKit.Img("Disc", p, BepJam.Art.FX("btn_round_cream"));
            var drt = ((RectTransform)disc.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(0, -360), new Vector2(230, 230));
            var ic = UIKit.Img("Icon", drt, BepJam.Art.FX(icon)); ((RectTransform)ic.transform).Stretch(46, 40, 46, 52);
            srt.localScale = Vector3.one * 2.2f; UIKit.SetAlpha(stamp, 0); drt.localScale = Vector3.zero;
            Tween.Delay(0.55f, () => { if (srt == null) return; Tween.Scale(srt, Vector3.one, 0.16f).SetEase(Ease.InQuad).SetUnscaled(); Tween.Fade(stamp, 1, 0.1f).SetUnscaled(); }, true, p);
            Tween.Delay(0.7f, () =>
            {
                if (drt == null) return;
                Sfx.Play("stamp"); Sfx.Play("unlock", 0.8f); VFX.ScreenShake(8, 0.2f);
                Tween.Scale(drt, Vector3.one, 0.5f).SetEase(Ease.OutBack).SetUnscaled();
                VFX.Sparkle(drt.position, 16, 1.3f);
                Tween.PunchRotation(ic.rectTransform, 15, 0.6f).SetDelay(0.3f).SetUnscaled();
            }, true, p);
            var title = Txt(p, name, 62, CInk, new Vector2(0.5f, 1f), new Vector2(0, -620), new Vector2(W - 200, 84));
            UIKit.SetAlpha(title, 0); Tween.Fade(title, 1, 0.3f).SetDelay(0.95f).SetUnscaled();
            var body = Txt(p, "", 36, CInk, new Vector2(0.5f, 1f), new Vector2(0, -780), new Vector2(W - 180, 190));
            var tw = body.gameObject.AddComponent<Typewriter>(); tw.full = desc; tw.cps = 45; tw.Begin(1.1f);
            PaperBtn(p, "Thử ngay!", "btn_green", BottomPad, 440, 126, () => h.Close(onDone), "play", 1.4f);
            TapToFinish(h, tw);
        }

        // ================================================================== FOOD STORY
        public static void Story(Action onDone)
        {
            var (dishId, idx, count) = Culture.NextStory();
            var d = GameData.Dishes[dishId]; var c = Culture.Dish[dishId];
            const float W = 900, H = 1300;
            var h = Open("Chuyện bên mâm", W, H);
            var p = h.panel;
            // page-flip instead of the default fly-in
            Tween.KillAll(p); p.anchoredPosition = Vector2.zero; p.localEulerAngles = Vector3.zero;
            p.pivot = new Vector2(0f, 0.5f); p.anchoredPosition = new Vector2(-W / 2f, 0);
            p.localScale = new Vector3(0, 1, 1);
            Tween.Scale(p, Vector3.one, 0.45f).SetEase(Ease.OutCubic).SetUnscaled();
            Sfx.Play("page", 0.9f);

            var food = UIKit.Img("Dish", p, d.Icon);
            var frt = ((RectTransform)food.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(-180, -330), new Vector2(300, 250));
            frt.anchoredPosition += new Vector2(-600, 0);
            Tween.Move(frt, frt.anchoredPosition + new Vector2(600, 0), 0.5f).SetEase(Ease.OutBack).SetDelay(0.35f).SetUnscaled()
                 .OnComplete(() => { if (frt) Tween.PunchRotation(frt, 10, 0.4f).SetUnscaled(); });
            var nameBox = UIKit.Sliced("Name", p, BepJam.Art.FX("speech_pink"));
            var nrt = ((RectTransform)nameBox.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(190, -290), new Vector2(380, 100));
            Txt(nrt, d.name.Split('(')[0].Trim(), 34, CCream, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(320, 70));
            var tag = UIKit.Sliced("Country", p, BepJam.Art.FX(d.country == Country.VN ? "btn_red" : "btn_blue"));
            var tgrt = ((RectTransform)tag.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(190, -390), new Vector2(260, 70));
            Txt(tgrt, d.country == Country.VN ? "VIỆT NAM" : "HÀN QUỐC", 28, CCream, new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(210, 46));
            foreach (var rt in new[] { nrt, tgrt }) { rt.localScale = Vector3.zero; Tween.Scale(rt, Vector3.one, 0.35f).SetEase(Ease.OutBack).SetDelay(0.6f).SetUnscaled(); }

            // "Bạn có biết?" stamps down, bulb blinks
            var q = Txt(p, "Bạn có biết?", 52, CRed, new Vector2(0.5f, 1f), new Vector2(0, -540), new Vector2(560, 72));
            q.rectTransform.localScale = Vector3.one * 2f; UIKit.SetAlpha(q, 0);
            Tween.Delay(0.8f, () => { if (q == null) return; Tween.Scale(q.rectTransform, Vector3.one, 0.16f).SetEase(Ease.InQuad).SetUnscaled(); Tween.Fade(q, 1, 0.1f).SetUnscaled(); Sfx.Play("stamp", 0.7f); }, true, p);
            var bulb = UIKit.Img("Bulb", p, BepJam.Art.FX("bulb"));
            var brt = ((RectTransform)bulb.transform).Anchor(new Vector2(0.5f, 1f), new Vector2(-250, -540), new Vector2(64, 64));
            Tween.Fade(bulb, 0.35f, 0.5f).SetLoops(-1, true).SetEase(Ease.InOutSine).SetUnscaled();

            var body = Txt(p, "", 38, CInk, new Vector2(0.5f, 1f), new Vector2(0, -760), new Vector2(W - 170, 300));
            var tw = body.gameObject.AddComponent<Typewriter>(); tw.full = c.stories[idx]; tw.cps = 45; tw.sound = "type"; tw.Begin(1.0f);
            Txt(p, $"Chuyện {idx + 1} / {count}", 30, CMute, new Vector2(0.5f, 0f), new Vector2(0, BottomPad + 130 + 50), new Vector2(400, 44));
            PaperBtn(p, "Tiếp tục", "btn_green", BottomPad, 420, 126, () => h.Close(onDone), null, 1.2f);
            TapToFinish(h, tw);
        }

        static void TapToFinish(Handle h, Typewriter tw)
        {
            var dim = h.root.GetComponentInChildren<Image>();
            var relay = (dim ? dim.gameObject : h.root.gameObject).AddComponent<ClickRelay>();
            relay.onClick = () => { if (tw && !tw.Done) tw.Finish(); };
        }

        // ================================================================== NOTEBOOK
        public static void Notebook(Action onClose)
        {
            const float W = 940, H = 1900;
            var h = Open("Sổ tay ẩm thực", W, H);
            var p = h.panel;
            var unlocked = Culture.UnlockedDishes();
            ScrollList(p, "NotebookScroll", 150, BottomPad + 170, 70, out var content);
            float y = 0;
            foreach (var id in GameData.Dishes.Keys)
            {
                bool open = unlocked.Contains(id);
                var d = GameData.Dishes[id];
                if (!Culture.Dish.TryGetValue(id, out var c))
                    c = new Culture.Entry { native = d.ShortName, intro = d.story, stories = new[] { Culture.History(id) } };
                float rowH = open ? 640 + c.stories.Length * 132 : 190;
                var card = UIKit.Sliced("Card", content, BepJam.Art.FX(open ? "speech_cream" : "hud_bar"));
                var crt = (RectTransform)card.transform;
                crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1f);
                crt.sizeDelta = new Vector2(-30, rowH - 30); crt.anchoredPosition = new Vector2(0, -y);
                var ic = UIKit.Img("I", crt, d.Icon); ((RectTransform)ic.transform).Anchor(new Vector2(0, 1), new Vector2(110, -100), new Vector2(160, 140));
                if (!open) { ic.color = new Color(0.25f, 0.2f, 0.22f, 0.35f); Txt(crt, "???  ·  Chơi tiếp để mở khoá", 34, CMute, new Vector2(0, 1), new Vector2(485, -85), new Vector2(500, 60), TextAnchor.MiddleLeft); }
                else
                {
                    Txt(crt, d.name.Split('(')[0].Trim(), 42, CInk, new Vector2(0, 1), new Vector2(485, -60), new Vector2(500, 56), TextAnchor.MiddleLeft);
                    Txt(crt, c.native + "  ·  " + (d.country == Country.VN ? "Việt Nam" : "Hàn Quốc"), 30, CMute, new Vector2(0, 1), new Vector2(485, -134), new Vector2(500, 44), TextAnchor.MiddleLeft);
                    Txt(crt, c.intro, 30, CInk, new Vector2(0, 1), new Vector2(385, -230), new Vector2(700, 96), TextAnchor.UpperLeft);
                    RecipeRow(crt, d, -300, 722, 94, false, 0);
                    for (int i = 0; i < c.stories.Length; i++)
                        Txt(crt, "• " + c.stories[i], 28, Pal.Hex("6B4F5B"), new Vector2(0, 1), new Vector2(385, -640 - i * 132), new Vector2(700, 108), TextAnchor.UpperLeft);
                }
                y += rowH;
            }
            content.sizeDelta = new Vector2(0, y);
            PaperBtn(p, "Đóng", "btn_green", BottomPad, 380, 120, () => h.Close(onClose), null, 0.5f);
        }
    }

    /// <summary>Emits steam puffs above a target while it exists.</summary>
    public class SteamEmitter : MonoBehaviour
    {
        public RectTransform target; public float delay; float t;
        void Update()
        {
            if (target == null) return;
            delay -= Time.unscaledDeltaTime; if (delay > 0) return;
            t -= Time.unscaledDeltaTime; if (t > 0) return;
            t = 0.35f;
            VFX.Steam(target.position + Vector3.up * target.rect.height * target.lossyScale.y * 0.35f, 1.1f);
        }
    }

    /// <summary>Vibration preference + helper (no-op on desktop).</summary>
    public static class Haptics
    {
        public static bool On { get => PlayerPrefs.GetInt("bj_vibe", 1) == 1; set { PlayerPrefs.SetInt("bj_vibe", value ? 1 : 0); PlayerPrefs.Save(); } }
        public static void Buzz()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (On) Handheld.Vibrate();
#endif
        }
    }
}
