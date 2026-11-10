using UnityEngine;
using UnityEngine.UI;

namespace BepJam
{
    public static class Progress
    {
        const string TipsKey = "bj_tips_seen";
        public static bool TipSeen(string key) => !AutoPilot.Requested && PlayerPrefs.GetString(TipsKey, "|").Contains("|" + key + "|");
        public static void MarkTipSeen(string key)
        {
            if (AutoPilot.Requested || TipSeen(key)) return;
            PlayerPrefs.SetString(TipsKey, PlayerPrefs.GetString(TipsKey, "|") + key + "|");
            PlayerPrefs.Save();
        }
        public static void ResetTips() => PlayerPrefs.DeleteKey(TipsKey);

        public static int Stars(int level) => PlayerPrefs.GetInt("bj_stars_" + level, 0);
        public static int TotalStars { get { int s = 0; for (int i = 1; i <= GameData.Levels.Count; i++) s += Stars(i); return s; } }
        public static int Coins { get => PlayerPrefs.GetInt("bj_coins", 0); set { if (AutoPilot.Requested) return; PlayerPrefs.SetInt("bj_coins", value); PlayerPrefs.Save(); } }
        /// <summary>Playtest switch: every level is open. Set false to go back to star-gated progression.</summary>
        public const bool UnlockAll = false;
        public static bool Unlocked(int level) => UnlockAll || level <= 1 || Stars(level - 1) > 0;
        public static int NextLevel { get { for (int i = 1; i <= GameData.Levels.Count; i++) if (Stars(i) == 0) return i; return GameData.Levels.Count; } }
        public static void Complete(int level, int stars)
        {
            if (AutoPilot.Requested) return;   // QA runs never touch the player's save
            Album.Collect(level);
            if (stars > Stars(level)) PlayerPrefs.SetInt("bj_stars_" + level, stars);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Entry point: builds the canvas, the phone-shaped root and switches between Home and Game.</summary>
    public class GameBoot : MonoBehaviour
    {
        public static GameBoot I;
        public const float PhoneW = 1080f, PhoneH = 2336f;

        public Canvas canvas;
        public RectTransform phone;      // 1080 x 2336 design space, everything lives here
        public RectTransform content;    // screens (shaken by VFX.ScreenShake)
        public RectTransform popupLayer;
        public RectTransform fxLayer;
        public RectTransform gameplayLayer; // messages stay above every gameplay effect, below modal sheets
        public RectTransform popupFxLayer;
        public RectTransform transitionLayer;   // paper-wipe transitions live here (survive screen clears)
        bool transitioning;
        public Camera cam;

        HomeView home;
        GameView game;

        void Awake()
        {
            I = this;
            if (Application.targetFrameRate <= 0) Application.targetFrameRate = 60;
            if (Application.isMobilePlatform) QualitySettings.vSyncCount = 0;
            Input.multiTouchEnabled = false;
            AudioManager.Ensure();
            BuildCanvas();
            VFX.Init(fxLayer);
            VFX.Font = Art.Font;
            VFX.ShakeRoot = content;
        }

        void Start()
        {
            if (AutoPilot.Requested) gameObject.AddComponent<AutoPilot>();
            else ShowHome();
        }

        void BuildCanvas()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera", typeof(Camera));
                cgo.tag = "MainCamera"; cam = cgo.GetComponent<Camera>();
                cam.orthographic = true; cam.transform.position = new Vector3(0, 0, -10);
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Hex("3E2A22");
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5;
            canvas.pixelPerfect = false;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(PhoneW, PhoneH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // backdrop outside the phone area (desktop / tablets): blurred-looking lake + dark vignette
            var root = (RectTransform)go.transform;
            var back = UIKit.Img("Backdrop", root, Art.UI("bg_lake"), false, false);
            ((RectTransform)back.transform).Stretch(-200, -200, -200, -200);
            back.color = new Color(0.55f, 0.5f, 0.5f);
            var aspect = back.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = 941f / 1672f;

            phone = UIKit.Node("Phone", root);
            phone.Center(0, 0, PhoneW, PhoneH);
            var mask = phone.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            content = UIKit.Node("Content", phone); content.Stretch();
            fxLayer = UIKit.Node("FX", phone); fxLayer.Stretch();
            gameplayLayer = UIKit.Node("GameplayMessages", phone); gameplayLayer.Stretch();
            popupLayer = UIKit.Node("Popups", phone); popupLayer.Stretch();
            popupFxLayer = UIKit.Node("PopupFX", phone); popupFxLayer.Stretch();
            transitionLayer = UIKit.Node("Transition", phone); transitionLayer.Stretch();
        }

        void Clear()
        {
            Tween.KillAllTweens();
            VFX.ClearAll();
            // Disable synchronously: Destroy is deferred, and old Update/coroutines must not run on the new screen.
            foreach (var layer in new[] { content, gameplayLayer, popupLayer })
                for (int i = layer.childCount - 1; i >= 0; i--)
                {
                    var child = layer.GetChild(i).gameObject;
                    child.SetActive(false); Destroy(child);
                }
            content.localPosition = Vector3.zero;
            home = null; game = null;
        }

        public void ShowHome()
        {
            if (home == null && game == null || AutoPilot.Requested || transitioning) { ShowHomeNow(); return; }
            StartCoroutine(PaperWipe("Bếp Jam", ShowHomeNow));
        }

        void ShowHomeNow()
        {
            Clear();
            home = HomeView.Create(content);
            AudioManager.I.PlayMusic("bgm_home");
        }

        public void PlayLevel(int number, bool skipIntro = false)
        {
            number = Mathf.Clamp(number, 1, GameData.Levels.Count);
            if (AutoPilot.Requested || transitioning) { PlayLevelNow(number, skipIntro); return; }
            StartCoroutine(PaperWipe("Ngày " + number, () => PlayLevelNow(number, skipIntro)));
        }

        void PlayLevelNow(int number, bool skipIntro)
        {
            Clear();
            var def = GameData.Levels[number - 1];
            game = GameView.Create(content, def, skipIntro);
            AudioManager.I.PlayMusic("bgm_game");
        }

        /// <summary>A big torn paper sheet sweeps across the phone (page-flip sound), the screen swaps underneath,
        /// then the sheet slides away. Uses a coroutine so Clear()/KillAllTweens can't interrupt it.</summary>
        System.Collections.IEnumerator PaperWipe(string caption, System.Action mid)
        {
            transitioning = true;
            var sheet = UIKit.Sliced("Wipe", transitionLayer, Art.FX("paper_panel"));
            sheet.raycastTarget = true;
            var rt = (RectTransform)sheet.transform;
            rt.Center(0, 0, 1500, 2900);
            rt.localEulerAngles = new Vector3(0, 0, -5f);
            var stamp = UIKit.Img("Stamp", rt, Art.FX("ribbon_stamp"));
            ((RectTransform)stamp.transform).Center(0, 40, 420, 420);
            var cap = UIKit.Label("Cap", rt, caption, 84, Pal.Hex("D9414F"));
            ((RectTransform)cap.transform).Center(0, 40, 380, 140);
            UIKit.Fit(cap, 84, 40);
            Sfx.Play("page", 0.9f);
            yield return Slide(rt, new Vector2(1600, 0), Vector2.zero, 0.34f, true);
            Sfx.Play("stamp", 0.5f);
            stamp.rectTransform.localScale = Vector3.one * 1.6f;
            for (float t = 0; t < 0.12f; t += Time.unscaledDeltaTime) { stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, t / 0.12f); yield return null; }
            stamp.rectTransform.localScale = Vector3.one;
            mid?.Invoke();
            yield return new WaitForSecondsRealtime(0.18f);
            Sfx.Play("paper", 0.6f);
            yield return Slide(rt, Vector2.zero, new Vector2(-1700, -120), 0.38f, false);
            Destroy(sheet.gameObject);
            transitioning = false;
        }

        static System.Collections.IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to, float dur, bool easeIn)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur; k = easeIn ? k * k * k : 1 - Mathf.Pow(1 - k, 3);
                if (rt == null) yield break;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                yield return null;
            }
            if (rt) rt.anchoredPosition = to;
        }

        public GameView CurrentGame => game;
        public HomeView CurrentHome => home;
    }
}
