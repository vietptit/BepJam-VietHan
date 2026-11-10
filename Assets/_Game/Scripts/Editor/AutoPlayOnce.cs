using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BepJam.EditorTools
{
    /// <summary>If "Library/bj_autoplay_once" exists: open Main scene, set a portrait Game view and press Play (one time).
    /// Also available any time from the menu BepJam/Play (portrait).</summary>
    [InitializeOnLoad]
    public static class AutoPlayOnce
    {
        const string Flag = "Library/bj_autoplay_once";

        static AutoPlayOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists("Library/bj_qa_once"))
            {
                File.Delete("Library/bj_qa_once");
                PlayerPrefs.SetInt("bj_qa", 1); PlayerPrefs.Save();
                EditorApplication.delayCall += PlayPortrait;
                return;
            }
            if (!File.Exists(Flag)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Flag)) return;
                File.Delete(Flag);
                PlayPortrait();
            };
        }

        [MenuItem("BepJam/Play (portrait 1080x2340)")]
        public static void PlayPortrait()
        {
            if (EditorSceneManager.GetActiveScene().path != ProjectSetup.ScenePath)
                EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            SetPortraitGameView();
            EditorApplication.EnterPlaymode();
        }

        static void SetPortraitGameView()
        {
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = single.GetProperty("instance").GetValue(null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
                var gType = group.GetType();
                int total = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);
                int idx = -1;
                for (int i = 0; i < total; i++)
                {
                    var s = gType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    if ((string)s.GetType().GetProperty("baseText").GetValue(s) == "BepJam Portrait") { idx = i; break; }
                }
                if (idx < 0)
                {
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var enumType = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeType.GetConstructor(new[] { enumType, typeof(int), typeof(int), typeof(string) });
                    var size = ctor.Invoke(new object[] { Enum.ToObject(enumType, 1), 1080, 2340, "BepJam Portrait" });
                    gType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    idx = (int)gType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }
                var gvType = asm.GetType("UnityEditor.GameView");
                var gv = EditorWindow.GetWindow(gvType);
                var m = gvType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                m?.Invoke(gv, new object[] { idx, null });
            }
            catch (Exception e) { Debug.LogWarning("[BepJam] Could not set portrait Game view: " + e.Message); }
        }
    }
}
// qa 1794048140
// qa2
// qa3
// qa4
// qa5
// qa6
