using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// =========================================================
// POLICES DU DESIGN SYSTEM -> FONT ASSETS TEXTMESHPRO
// Menu : Blockforge > Design System > Build Font Assets
// (lance aussi automatiquement par Apply Design System).
// Barlow (400/500/600/700), Barlow Condensed (600/700) et
// JetBrains Mono (500) sont dans Assets/_Project/UI/Fonts
// (licence OFL a cote). Les font assets sont DYNAMIQUES
// (glyphes ajoutes a la volee), ranges dans
// Resources/DesignSystem/Fonts pour DSAssets.Font(), avec la
// police TMP par defaut en secours (glyphes manquants).
// =========================================================

public static class DesignSystemFonts
{
    public const string SourceFolder = "Assets/_Project/UI/Fonts";
    public const string TargetFolder = "Assets/_Project/UI/DesignSystem/Resources/DesignSystem/Fonts";

    [MenuItem("Blockforge/Design System/Build Font Assets")]
    public static void BuildMenu() => Build(true);

    public static bool AllPresent()
    {
        foreach (var assetName in DSAssets.FontAssetNames)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{TargetFolder}/{assetName}.asset") == null)
                return false;
        }
        return true;
    }

    public static void Build(bool rebuild)
    {
        if (!AssetDatabase.IsValidFolder(TargetFolder))
        {
            Directory.CreateDirectory(TargetFolder);
            AssetDatabase.Refresh();
        }

        int created = 0;
        foreach (var assetName in DSAssets.FontAssetNames)
        {
            string fontName = assetName.Replace(" SDF", "");
            string target = $"{TargetFolder}/{assetName}.asset";
            if (!rebuild && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target) != null)
                continue;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{SourceFolder}/{fontName}.ttf");
            if (font == null)
            {
                Debug.LogError($"[DesignSystem] Police manquante : {SourceFolder}/{fontName}.ttf");
                continue;
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target) != null)
                AssetDatabase.DeleteAsset(target);

            var asset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048,
                                                      AtlasPopulationMode.Dynamic, false);
            if (asset == null)
            {
                Debug.LogError($"[DesignSystem] Creation du font asset impossible pour {fontName}.");
                continue;
            }
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, target);

            var atlas = asset.atlasTextures[0];
            atlas.name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            var material = asset.material;
            material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(material, asset);

            // Secours pour les glyphes absents (fleches, symboles)
            var fallback = TMP_Settings.defaultFontAsset;
            if (fallback != null && fallback != asset)
                asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };

            // Pre-remplit l'atlas avec l'ASCII et le Latin-1 (moins de travail en jeu)
            asset.TryAddCharacters(PrintableLatin(), out _);
            EditorUtility.SetDirty(asset);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (created > 0)
            Debug.Log($"[DesignSystem] {created} font asset(s) TextMeshPro generes dans {TargetFolder}.");
    }

    private static string PrintableLatin()
    {
        var sb = new System.Text.StringBuilder();
        for (int c = 0x20; c < 0x7F; c++) sb.Append((char)c);
        for (int c = 0xA0; c <= 0xFF; c++) sb.Append((char)c);
        sb.Append("–—‘’“”…·×");
        return sb.ToString();
    }
}
