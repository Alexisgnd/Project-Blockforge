using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// AVATARS DE LA KILLFEED (vignettes des robots)
// En match contre les bots, l'identite d'un participant est son
// robot : chaque participant a une vignette de son blueprint
// (RobotThumbnailRenderer), rendue une fois pendant le compte a
// rebours, un robot par image, loin au-dessus de la carte. La
// vignette est relue dans deux Texture2D : couleur, et niveaux
// de gris a victimBrightness (victime). Le joueur local peut
// garder sa photo Steam (CombatFeedbackTheme.localUsesProfilePicture).
// Pas d'image (robot vide, rendu rate) : la killfeed affiche le
// repli du design (fond colore deterministe + picto user).
// =========================================================

public static class KillfeedAvatars
{
    private struct Entry
    {
        public Texture2D color, gray;
    }

    private static readonly Dictionary<TdmParticipant, Entry> cache = new();
    // Loin au-dessus de la carte : la camera d'apercu ne voit que le calque des vignettes
    private static readonly Vector3 Stage = new(0f, 3000f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache.Clear();

    public static Texture2D Get(TdmParticipant p, bool gray)
    {
        if (p == null || !cache.TryGetValue(p, out var e))
            return null;
        return gray ? e.gray : e.color;
    }

    // Rend les vignettes manquantes, une par image (pendant le compte a rebours)
    public static IEnumerator Prepare(TdmMatch match, CombatFeedbackTheme theme)
    {
        if (match == null)
            yield break;
        var todo = new List<TdmParticipant>(match.Participants);
        foreach (var p in todo)
        {
            if (p == null || cache.ContainsKey(p))
                continue;
            yield return null;
            cache[p] = Render(p, match, theme);
        }
    }

    private static Entry Render(TdmParticipant p, TdmMatch match, CombatFeedbackTheme theme)
    {
        var entry = new Entry();
        if (p.Blueprint == null)
            return entry;
        int size = Mathf.Max(64, theme.avatarRenderSize);
        RenderTexture rt = null;
        try
        {
            rt = RobotThumbnailRenderer.Render(p.Blueprint, match.BlockDefs, Stage, size, size);
            if (rt == null)
                return entry;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var color = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false)
            {
                name = "KillfeedAvatar_" + p.Name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            color.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
            color.Apply();
            RenderTexture.active = previous;

            var pixels = color.GetPixels32();
            float k = Mathf.Clamp01(theme.victimBrightness);
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * k), 0, 255);
                pixels[i] = new Color32(g, g, g, c.a);
            }
            var gray = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false)
            {
                name = "KillfeedAvatarGray_" + p.Name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            gray.SetPixels32(pixels);
            gray.Apply();
            entry.color = color;
            entry.gray = gray;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Killfeed] Vignette de {p.Name} impossible : {ex.Message}");
        }
        finally
        {
            if (rt != null)
            {
                rt.Release();
                Object.Destroy(rt);
            }
        }
        return entry;
    }

    // Fin du match : on libere les textures
    public static void Clear()
    {
        foreach (var e in cache.Values)
        {
            if (e.color != null) Object.Destroy(e.color);
            if (e.gray != null) Object.Destroy(e.gray);
        }
        cache.Clear();
    }

    // Repli du design : couleur deterministe tiree de l'identifiant
    public static Color FallbackColor(TdmParticipant p)
    {
        int id = p != null ? p.Id : 0;
        uint h = (uint)(id * 2654435761u);
        float hue = (h % 360u) / 360f;
        return Color.HSVToRGB(hue, 0.45f, 0.55f);
    }
}
