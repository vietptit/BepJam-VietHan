using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BepJam
{
    /// <summary>QA helper: launch the player with "-autoshot &lt;folder&gt;" to auto-play a few levels and save screenshots.
    /// Also usable in the editor via the BepJam menu (plays itself). Never active in normal play.</summary>
    public class AutoPilot : MonoBehaviour
    {
        public static bool Requested => Environment.GetCommandLineArgs().Contains("-autoshot") || ForceInEditor || PlayerPrefs.GetInt("bj_qa", 0) == 1;
        public static bool ForceInEditor;
        string dir;

        void OnEnable() => Application.logMessageReceived += RecordError;
        void OnDisable() => Application.logMessageReceived -= RecordError;
        void RecordError(string message, string stack, LogType type)
        {
            if (dir == null || (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)) return;
            File.AppendAllText(Path.Combine(dir, "qa-errors.txt"), message + "\n" + stack + "\n");
        }

        IEnumerator Start()
        {
            // Keep QA mode stable after consuming the one-shot PlayerPrefs flag.
            ForceInEditor = true;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-autoshot");
            bool editorQa = PlayerPrefs.GetInt("bj_qa", 0) == 1;
            PlayerPrefs.SetInt("bj_qa", 0); PlayerPrefs.Save(); if (editorQa) ForceInEditor = true;
            dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.dataPath, "../Tools/qa");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "qa-errors.txt"), "");
            File.WriteAllText(Path.Combine(dir, "capture-manifest.txt"), "Bếp Jam UI QA\n");
            int startLevel = PlayerPrefs.GetInt("bj_qa_level", 5);
            int li = Array.IndexOf(args, "-level");
            if (li >= 0 && li + 1 < args.Length) int.TryParse(args[li + 1], out startLevel);

            var boot = GameBoot.I;
            boot.ShowHome();
            yield return new WaitForSeconds(2.6f);
            yield return Shot("01_home");
            Popups.Settings(null);
            yield return new WaitForSeconds(1.6f);
            yield return Shot("01b_settings");
            boot.ShowHome();
            Popups.Notebook(null);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("01c_notebook");
            var notebookScroll = GameBoot.I.popupLayer.GetComponentInChildren<ScrollRect>();
            notebookScroll.verticalNormalizedPosition = 0;
            yield return null;
            yield return Shot("01c_notebook_bottom");
            boot.ShowHome();
            Popups.Story(null);
            yield return new WaitForSeconds(4.5f);
            yield return Shot("01d_story");
            boot.ShowHome();
            Popups.LevelIntro(GameData.Levels[7], null, null);
            yield return new WaitForSeconds(3.2f);
            yield return Shot("01e_new_recipe_intro");

            boot.PlayLevel(startLevel);
            yield return new WaitForSeconds(2.2f);
            yield return Shot("02_intro");

            boot.PlayLevel(startLevel, true);
            yield return new WaitForSeconds(2.4f);
            yield return Shot("03_game_start");
            var playingKitchen = boot.CurrentGame.kitchen;
            var policy = new AutoPlayPolicy(playingKitchen, Time.realtimeSinceStartup);
            float playStarted = Time.realtimeSinceStartup;
            int shots = 0;
            while (true)
            {
                var g = boot.CurrentGame;
                if (g == null || g.kitchen != playingKitchen) throw QaFailure("game changed during playback", playingKitchen);
                var k = g.kitchen;
                string failure = policy.Failure(Time.realtimeSinceStartup);
                if (failure != null)
                {
                    Debug.LogError(QaFailure(failure, k).Message);
                    yield return new WaitForSecondsRealtime(0.8f);
                    yield return Shot(k.ended ? "06_lose" : "06_stalled");
                    throw QaFailure(failure, k);
                }
                if (k.ended)
                {
                    // Win opens directly after the kitchen finishes.
                    string winPopup = k.level.number >= GameData.Levels.Count ? "Popup_Bếp trưởng!" : "Popup_Tuyệt vời!";
                    yield return ReachWin(winPopup);
                    yield return new WaitForSecondsRealtime(2.8f);
                    yield return Shot("06_win", winPopup);
                    break;
                }
                policy.Step();
                yield return new WaitForSecondsRealtime(AutoPlayPolicy.TapInterval);
                float t = Time.realtimeSinceStartup - playStarted;
                if (shots == 0 && t > 2.2f) { yield return Shot("04_game_mid"); shots++; }
                else if (shots == 1 && k.stations.Values.Any(s => s.state == StationState.Cooking)) { yield return Shot("05_game_cooking"); shots++; }
            }

            boot.PlayLevel(GameData.Levels.First(l => l.vip.Take(3).Any(v => v)).number, true);
            yield return new WaitForSeconds(1.5f);
            var waitingVip = boot.CurrentGame.kitchen.seats.FirstOrDefault(c => c != null && c.vip);
            if (waitingVip == null) throw new InvalidOperationException("QA: expected a seated VIP for loss capture");
            waitingVip.patience = 0.3f;
            yield return new WaitForSeconds(3f);
            if (!boot.CurrentGame.kitchen.ended || boot.CurrentGame.kitchen.loseKind != "vip")
                throw new InvalidOperationException("QA: expected a VIP patience loss");
            yield return Shot("07_lose", "Popup_Khách VIP bỏ về!");

            boot.PlayLevel(1, true);
            yield return new WaitForSeconds(3.3f);
            yield return Shot("08_tutorial");
            yield return CalmTutorialChecks();

            // The preview only adds in-memory unlocks and is restored even if capture throws.
            using (Album.QaPreview(5, 10))
            {
                boot.ShowHome();
                yield return new WaitForSecondsRealtime(1.4f);
                PressHomeAlbum();
                yield return WaitForPopup("Popup_Album");
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("09_album", "Popup_Album");
                boot.ShowHome();
                // Clear destroys old screens at end of frame. Never open the next sheet in that same frame.
                yield return null;
                Popups.AlbumReward(Album.ForLevel(10), null);
                yield return WaitForPopup("Popup_AlbumReward_p10");
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot("10_album_reward", "Popup_AlbumReward_p10");
                boot.ShowHome();
                yield return null;
                Popups.AlbumReward(Album.ForLevel(5), null);
                yield return WaitForPopup("Popup_AlbumReward_p05");
                yield return new WaitForSecondsRealtime(2.5f);
                var reward = Popup("Popup_AlbumReward_p05");
                var rewardPanel = reward.Find("Panel") as RectTransform;
                var header = rewardPanel.Find("NextDestinationHeader")?.GetComponent<Text>();
                var destination = rewardPanel.Find("NextDestination")?.GetComponent<Text>();
                if (!header || header.text != "Điểm đến tiếp theo!" || !destination || destination.text != Chapters.ForLevel(6).Header)
                    throw QaFailure("p05 reward is missing the next-destination header");
                Inside(rewardPanel, header.rectTransform);
                Inside(rewardPanel, destination.rectTransform);
                yield return Shot("10b_album_reward_p05", "Popup_AlbumReward_p05");
                boot.ShowHome();
            }

            boot.PlayLevel(49);
            yield return WaitForPopup("Popup_LevelIntro_49");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("11_intro_l49", "Popup_LevelIntro_49");
            boot.PlayLevel(49, true);
            yield return new WaitForSecondsRealtime(2.4f);
            // Exercise the same book button used by the player.
            var book = boot.CurrentGame.GetComponentsInChildren<JuicyButton>().FirstOrDefault(b => b.name == "Công thức");
            if (!book || !book.interactable) throw new InvalidOperationException("QA: recipe book button is unavailable");
            Click(book.transform);
            yield return WaitForPopup("Popup_Recipes");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("11_intro_l49_book", "Popup_Recipes");

            // Stress the complete menu and prove the last four-part recipe remains reachable.
            boot.ShowHome(); yield return null;
            Popups.Recipes(GameData.Dishes.Keys.ToList(), null);
            yield return WaitForPopup("Popup_Recipes");
            yield return new WaitForSecondsRealtime(1f);
            var allRecipes = Popup("Popup_Recipes").GetComponentInChildren<ScrollRect>();
            allRecipes.verticalNormalizedPosition = 0;
            yield return null;
            yield return Shot("11b_all_recipes_bottom", "Popup_Recipes");

            boot.PlayLevel(50, true);
            yield return new WaitForSecondsRealtime(2.4f);
            float bossTime = 0;
            var bossPolicy = new AutoPlayPolicy(boot.CurrentGame.kitchen, Time.realtimeSinceStartup);
            while (bossTime < 9f)
            {
                var g = boot.CurrentGame;
                if (!g || g.kitchen.ended) throw new InvalidOperationException("QA: boss ended before mid-game capture");
                bossPolicy.Step();
                yield return new WaitForSecondsRealtime(AutoPlayPolicy.TapInterval);
                bossTime += AutoPlayPolicy.TapInterval;
            }
            if (!boot.CurrentGame || boot.CurrentGame.def.number != 50 || boot.CurrentGame.kitchen.ended)
                throw new InvalidOperationException("QA: expected active boss level 50");
            yield return Shot("12_boss_mid");
            yield return ChapterShots();
            Debug.Log("[AutoPilot] done → " + dir);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        RectTransform[] CoachStrips() => GameBoot.I.gameplayLayer.GetComponentsInChildren<RectTransform>()
            .Where(r => r.name.StartsWith("CoachStrip_") && r.gameObject.activeInHierarchy).ToArray();

        IEnumerator CalmTutorialChecks()
        {
            var boot = GameBoot.I;
            var dayOne = boot.CurrentGame;
            foreach (var tile in dayOne.GetComponentsInChildren<TileView>().ToArray())
            {
                tile.onTap(tile);
                yield return new WaitForSeconds(0.1f);
            }
            yield return new WaitForSeconds(8f);
            if (!dayOne.kitchen.won || boot.gameplayLayer.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Hand" || r.name == "Coach"))
                throw new InvalidOperationException("QA: day-one hand must end after the first dish");

            boot.PlayLevel(2, true);
            yield return new WaitForSeconds(2.5f);
            var g = boot.CurrentGame;
            var strips = CoachStrips();
            if (g.Paused || strips.Length != 1 || !strips[0].name.EndsWith("booster_book"))
                throw new InvalidOperationException("QA: book introduction must be a single non-pausing strip");
            var group = strips[0].GetComponent<CanvasGroup>();
            if (!group || group.blocksRaycasts || group.interactable)
                throw new InvalidOperationException("QA: coach strip blocks input");
            yield return Shot("08b_coach_strip");
            var tileToTap = g.GetComponentsInChildren<TileView>().First(t => t.crate.id == g.kitchen.Hint(false));
            var pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(boot.cam, tileToTap.rt.position) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<TileView>() != tileToTap)
                throw new InvalidOperationException("QA: tutorial covers a pantry tap");
            int moves = g.kitchen.moves;
            Click(tileToTap.transform, pointer);
            if (g.kitchen.moves != moves + 1 || g.Paused || CoachStrips().Length != 0)
                throw new InvalidOperationException("QA: next tap must both dismiss the strip and take an ingredient");
            yield return new WaitForSeconds(5.7f);
            if (CoachStrips().Length != 0) throw new InvalidOperationException("QA: queued coach did not wait six seconds");
            yield return new WaitForSeconds(0.6f);
            strips = CoachStrips();
            if (strips.Length != 1 || !strips[0].name.EndsWith("plates"))
                throw new InvalidOperationException("QA: next coach was not queued");
            yield return Shot("08c_queued_coach");
            yield return new WaitForSeconds(3.2f);
            if (CoachStrips().Length != 0) throw new InvalidOperationException("QA: coach did not auto-hide after three seconds");
            yield return new WaitForSeconds(6.2f);
            if (CoachStrips().Length != 0) throw new InvalidOperationException("QA: an already displayed coach repeated");
            File.AppendAllText(Path.Combine(dir, "capture-manifest.txt"), "PASS: day-one completion; tap-through coach; six-second queue gap; three-second auto-hide; no repeat.\n");
        }

        static Transform Popup(string name)
        {
            var layer = GameBoot.I.popupLayer;
            for (int n = layer.childCount - 1; n >= 0; n--)
            {
                var child = layer.GetChild(n);
                if (child.name == name && child.gameObject.activeInHierarchy) return child;
            }
            return null;
        }

        static IEnumerator WaitForPopup(string name)
        {
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var root = Popup(name);
                var panel = root ? root.Find("Panel") as RectTransform : null;
                if (panel && panel.anchoredPosition.sqrMagnitude < 4 && root.GetComponent<CanvasGroup>().alpha >= 0.99f)
                {
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForEndOfFrame();
                    yield break;
                }
                yield return null;
            }
            throw QaFailure("popup never opened/settled: " + name);
        }

        static IEnumerator ReachWin(string name)
        {
            yield return WaitForPopup(name);
        }

        static void PressHomeAlbum()
        {
            var home = GameBoot.I.CurrentHome;
            var button = home ? home.transform.Find("Album") : null;
            if (!button) throw new InvalidOperationException("QA: Home Album button missing");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(GameBoot.I.cam, button.position) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            var hit = hits.FirstOrDefault();
            if (hit.gameObject != button.gameObject)
                throw new InvalidOperationException("QA: Home Album button is covered by " + (hit.gameObject ? hit.gameObject.name : "no raycast hit"));
            Click(button, pointer);
        }

        // JuicyButton arms taps on pointer-down and checks eligibleForClick on click.
        // A click event alone is deliberately ignored by the real input handler.
        static void Click(Transform target, PointerEventData pointer = null)
        {
            Canvas.ForceUpdateCanvases();
            if (pointer == null) pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(GameBoot.I.cam, target.position) };
            pointer.button = PointerEventData.InputButton.Left;
            pointer.eligibleForClick = true;
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        static InvalidOperationException QaFailure(string reason, Kitchen kitchen = null)
        {
            var boot = GameBoot.I;
            if (kitchen == null && boot && boot.CurrentGame) kitchen = boot.CurrentGame.kitchen;
            string popups = boot ? string.Join(",", boot.popupLayer.Cast<Transform>()
                .Where(p => p.gameObject.activeInHierarchy).Select(p => p.name)) : "no boot";
            return new InvalidOperationException("QA: " + reason + " | " + AutoPlayPolicy.Describe(kitchen) + " | popups=[" + popups + "]");
        }

        IEnumerator ChapterShots()
        {
            var boot = GameBoot.I;
            foreach (int level in new[] { 6, 11, 16, 21, 26, 31, 36, 41, 46, 9, 37 })
            {
                boot.PlayLevel(level, true);
                yield return new WaitForSecondsRealtime(2.4f);
                var g = boot.CurrentGame;
                if (!g || g.def.number != level || !g.kitchen.started || g.Paused || g.kitchen.ended)
                    throw QaFailure("chapter capture expected active level " + level);
                var expected = Chapters.WindowBackground(g.def);
                var background = g.GetComponentsInChildren<Image>().FirstOrDefault(img => img.name == "ChapterBackground");
                if (expected && (!background || background.sprite != expected))
                    throw QaFailure("chapter background mismatch: " + Chapters.BackgroundPath(g.def));
                yield return Shot("20_ch_L" + level);
                File.AppendAllText(Path.Combine(dir, "capture-manifest.txt"), "L" + level + " → " + Chapters.BackgroundPath(g.def) +
                    "; rendered=" + (expected ? expected.name : "UI/bg_shop (fallback)") + "\n");
            }
        }

        IEnumerator Shot(string name, string expectedPopup = null)
        {
            yield return new WaitForEndOfFrame();
            if (expectedPopup != null && !Popup(expectedPopup))
                throw new InvalidOperationException("QA: refusing false capture " + name + "; missing " + expectedPopup);
            ValidateLayout();
            var tex = ScreenCapture.CaptureScreenshotAsTexture(1);
            try { File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG()); }
            finally { Destroy(tex); }
            File.AppendAllText(Path.Combine(dir, "capture-manifest.txt"), name + " → " + (expectedPopup ?? "screen") + "\n");
            Debug.Log("[AutoPilot] shot " + name);
        }

        static Rect Bounds(RectTransform rt, Transform relative)
        {
            var corners = new Vector3[4]; rt.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            foreach (var corner in corners)
            {
                Vector2 p = relative.InverseTransformPoint(corner); min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static void Inside(RectTransform outer, RectTransform inner)
        {
            var bounds = Bounds(inner, outer); var rect = outer.rect;
            if (bounds.xMin < rect.xMin - 1 || bounds.xMax > rect.xMax + 1 || bounds.yMin < rect.yMin - 1 || bounds.yMax > rect.yMax + 1)
                throw new InvalidOperationException("QA: " + inner.name + " leaves " + outer.name);
        }

        static void ValidateLayout()
        {
            Canvas.ForceUpdateCanvases();
            var boot = GameBoot.I; var g = boot.CurrentGame;
            if (g)
            {
                if (boot.gameplayLayer.GetSiblingIndex() <= boot.fxLayer.GetSiblingIndex())
                    throw new InvalidOperationException("QA: gameplay messages are under FX");
                var protectedRects = new List<RectTransform>();
                protectedRects.AddRange(g.GetComponentsInChildren<StationView>().Select(s => s.rt));
                protectedRects.AddRange(g.GetComponentsInChildren<PlateView>().Select(p => p.rt));
                protectedRects.AddRange(g.GetComponentsInChildren<TileView>().Select(t => t.rt));
                protectedRects.AddRange(g.GetComponentsInChildren<CustomerView>().Select(c => c.bubble));
                var hud = g.transform.Find("TopBar") as RectTransform;
                if (hud) protectedRects.Add(hud);
                var seated = g.GetComponentsInChildren<CustomerView>().Where(c => !c.customer.finished).ToArray();
                var plateBounds = g.GetComponentsInChildren<PlateView>().Select(p => Bounds(p.rt, boot.phone)).ToList();
                foreach (var c in seated)
                {
                    var order = VisibleBounds(c.bubble, boot.phone);
                    var localOrder = Bounds(c.bubble, c.rt);
                    if (localOrder.xMin < c.rt.rect.xMin - 1 || localOrder.xMax > c.rt.rect.xMax + 1)
                        throw new InvalidOperationException("QA: order bubble exceeds its seat width");
                    foreach (var slot in c.orderSlots) Inside(c.bubble, slot);
                    foreach (var other in seated)
                        if (c != other && Overlap(order, VisibleBounds(other.bubble, boot.phone)))
                            throw new InvalidOperationException("QA: neighbouring order bubbles overlap");
                    foreach (string ornament in new[] { "Bubble", "Bubble/Patience", "VipHalo", "Crown", "VIP" })
                    {
                        var rt = c.rt.Find(ornament) as RectTransform; if (!rt) continue;
                        var rect = VisibleBounds(rt, boot.phone);
                        if (hud && Overlap(rect, Bounds(hud, boot.phone)) || plateBounds.Any(p => Overlap(rect, p)))
                            throw new InvalidOperationException("QA: " + ornament + " overlaps HUD/plates");
                        foreach (var other in seated)
                            if (c != other && Overlap(rect, VisibleBounds(other.bubble, boot.phone)))
                                throw new InvalidOperationException("QA: VIP decoration overlaps neighbouring order");
                    }
                }
                foreach (Transform screen in boot.gameplayLayer)
                foreach (RectTransform card in screen)
                {
                    if (!card.gameObject.activeInHierarchy || card.name == "Hand") continue;
                    var group = card.GetComponent<CanvasGroup>(); if (group && group.alpha < 0.01f) continue;
                    var message = Bounds(card, boot.phone);
                    foreach (var protectedRect in protectedRects)
                    {
                        var other = Bounds(protectedRect, boot.phone);
                        other.xMin -= 24; other.yMin -= 24; other.xMax += 24; other.yMax += 24;
                        if (message.Overlaps(other)) throw new InvalidOperationException("QA: message overlaps " + protectedRect.name + " padding");
                    }
                    foreach (var text in card.GetComponentsInChildren<Text>())
                        if (text.cachedTextGenerator.lineCount > 2) throw new InvalidOperationException("QA: message exceeds two lines");
                }
            }
            foreach (var row in boot.popupLayer.GetComponentsInChildren<RectTransform>())
            {
                if (!row.name.StartsWith("Row_")) continue;
                foreach (RectTransform child in row) Inside(row, child);
                var equation = row.Find("Equation") as RectTransform;
                if (equation) foreach (var label in equation.GetComponentsInChildren<Text>()) Inside(equation, label.rectTransform);
            }
        }

        static bool Overlap(Rect a, Rect b) => a.width > 0 && a.height > 0 && b.width > 0 && b.height > 0 && a.Overlaps(b);
        static Rect VisibleBounds(RectTransform rt, Transform relative)
        {
            var rect = Bounds(rt, relative);
            for (var parent = rt.parent; parent; parent = parent.parent)
            {
                var mask = parent.GetComponent<RectMask2D>();
                if (!mask || !mask.enabled) continue;
                var clip = Bounds((RectTransform)parent, relative);
                float left = Mathf.Max(rect.xMin, clip.xMin), right = Mathf.Min(rect.xMax, clip.xMax);
                float bottom = Mathf.Max(rect.yMin, clip.yMin), top = Mathf.Min(rect.yMax, clip.yMax);
                if (right <= left || top <= bottom) return new Rect();
                rect = Rect.MinMaxRect(left, bottom, right, top);
            }
            return rect;
        }
    }
}
