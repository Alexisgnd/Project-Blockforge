using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// =========================================================
// REGLAGES D'IMPORT DES ASSETS DU DESIGN SYSTEM
// Tout PNG sous .../Resources/DesignSystem/ devient un sprite :
// - Shapes : 9-slice, bordure lue dans le nom (_b<px>), pixels
//   par unite = 100 x facteur de sur-echantillonnage (_k<n>) pour
//   qu'1 px du canvas de reference = n texels (net en 4K) ;
// - Icons  : 96 px pour 24 px affiches (PPU 400) ;
// - Logo   : PPU 100.
// Mipmaps + trilineaire : les formes restent lisses en reduction.
// Automatique a l'import, rien a faire a la main.
// =========================================================

public class DesignSystemAssetImporter : AssetPostprocessor
{
    private const string Marker = "/Resources/DesignSystem/";
    private static readonly Regex Factor = new(@"_k(\d+)");
    private static readonly Regex Border = new(@"_b(\d+)");

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains(Marker) || !path.EndsWith(".png"))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // obligatoire pour le 9-slice et le Tiled
        settings.spriteGenerateFallbackPhysicsShape = false;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);

        string file = System.IO.Path.GetFileNameWithoutExtension(path);
        if (path.Contains(Marker + "Shapes/"))
        {
            int k = Factor.Match(file) is { Success: true } f ? int.Parse(f.Groups[1].Value) : 1;
            int b = Border.Match(file) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0;
            importer.spritePixelsPerUnit = 100f * k;
            importer.spriteBorder = new Vector4(b, b, b, b);
        }
        else if (path.Contains(Marker + "Icons/"))
        {
            importer.spritePixelsPerUnit = 400f; // 96 px -> 24 unites
            importer.spriteBorder = Vector4.zero;
        }
        else
        {
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = Vector4.zero;
        }
    }
}
