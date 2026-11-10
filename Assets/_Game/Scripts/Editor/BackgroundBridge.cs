using System.IO;
using UnityEditor;
using UnityEngine;

namespace BepJam.EditorTools
{
    /// <summary>Lets external tools drive the editor without stealing window focus: write a command into
    /// Library/bj_cmd.txt ("refresh", "qa", "play", "stop"). Polled ~once per second from EditorApplication.update,
    /// which keeps ticking while the editor is in the background.</summary>
    [InitializeOnLoad]
    public static class BackgroundBridge
    {
        const string Cmd = "Library/bj_cmd.txt";
        static double next;

        static BackgroundBridge() { EditorApplication.update += Tick; }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(Cmd)) return;
            string c = File.ReadAllText(Cmd).Trim().ToLowerInvariant();
            File.Delete(Cmd);
            Debug.Log("[BepJam] bridge command: " + c);
            switch (c)
            {
                case "refresh": AssetDatabase.Refresh(); break;
                case "apk": if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; File.WriteAllText(Cmd, "apk"); break; } ProjectSetup.BuildAndroid(); break;
                case "stop": if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; break;
                case "play": if (!EditorApplication.isPlaying) AutoPlayOnce.PlayPortrait(); break;
                case "qa":
                    if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; File.WriteAllText(Cmd, "qa"); break; }
                    PlayerPrefs.SetInt("bj_qa", 1); PlayerPrefs.SetInt("bj_qa_level", File.Exists(Cmd + ".level") ? int.Parse(File.ReadAllText(Cmd + ".level").Trim()) : 5); PlayerPrefs.Save();
                    AutoPlayOnce.PlayPortrait();
                    break;
            }
        }
    }
}
