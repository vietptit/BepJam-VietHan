// Compile the actual model/data/report code outside Unity. No gameplay rules are stubbed.
using System;
using System.IO;
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class Sprite : Object { }
    public class Font : Object { }
    public struct Color { }
    public static class ColorUtility { public static bool TryParseHtmlString(string text, out Color color) { color = new Color(); return true; } }
    public static class Resources { public static T Load<T>(string path) where T : class => null; }
    public static class Debug
    {
        public static void Log(object value) => Console.WriteLine(value);
        public static void LogWarning(object value) => Console.Error.WriteLine(value);
    }
    public static class Application { public static string dataPath => Path.GetFullPath("Assets"); }
    public static class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp01(float f) => Math.Max(0, Math.Min(1, f));
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute { public MenuItem(string path) { } }
}
