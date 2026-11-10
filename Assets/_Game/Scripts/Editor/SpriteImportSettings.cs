using UnityEditor;
using UnityEngine;

namespace BepJam.EditorTools
{
    /// <summary>Every PNG under Resources is a crisp UI sprite (no mipmaps, no compression artefacts).
    /// 9-slice borders are assigned for the procedural UI panels.</summary>
    public class SpriteImportSettings : AssetPostprocessor
    {
        static readonly (string name, Vector4 border)[] Borders =
        {
            ("rounded", new Vector4(24, 24, 24, 24)),
            // Venba UI kit (L, B, R, T)
            ("panel", new Vector4(90, 90, 90, 90)), ("paper_panel", new Vector4(90, 90, 90, 90)),
            ("ribbon", new Vector4(90, 50, 90, 50)), ("paper_tag", new Vector4(90, 50, 90, 50)),
            ("pill", new Vector4(70, 34, 70, 34)), ("hud_bar", new Vector4(70, 34, 70, 34)),
            ("btn_green", new Vector4(56, 56, 56, 56)), ("btn_yellow", new Vector4(56, 56, 56, 56)),
            ("btn_red", new Vector4(56, 56, 56, 56)), ("btn_blue", new Vector4(56, 56, 56, 56)),
            ("btn_teal", new Vector4(56, 56, 56, 56)), ("btn_mustard", new Vector4(56, 56, 56, 56)), ("btn_plum", new Vector4(56, 56, 56, 56)),
            ("bubble", new Vector4(48, 48, 48, 48)), ("speech_cream", new Vector4(48, 48, 48, 48)), ("speech_pink", new Vector4(48, 48, 48, 48)),
            ("board", new Vector4(110, 110, 110, 110)),
        };

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/") && !assetPath.Contains("/ArtRef/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;
            ti.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(settings);
            // Android: ASTC keeps the APK small while illustrations stay crisp
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true; android.format = TextureImporterFormat.ASTC_6x6; android.maxTextureSize = 2048;
            ti.SetPlatformTextureSettings(android);
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            ti.spriteBorder = Vector4.zero;
            foreach (var (name, border) in Borders)
                if (file == name) ti.spriteBorder = border;
        }
    }
}
