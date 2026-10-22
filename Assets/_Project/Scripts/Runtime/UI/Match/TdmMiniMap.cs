using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

// =========================================================
// MINIMAP DU MATCH (theme hud du DS, bord droit, a mi-hauteur)
// Carte entiere de la zone jouable, nord en haut, photographiee
// une fois vue de dessus par une camera orthographique HDRP sans
// brouillard ni post-effets (comme MiniMap du banc d'essai), puis
// statique. Cadrage : colliders de la carte hors robots et hors
// decor geant (> 3 km), le meme que le NavMesh des bots.
// Marques (silhouettes d'equipe du DS) :
// - allies : disque TeamAlly, toujours visibles ; le joueur :
//   fleche accent qui tourne avec son cap (match normal)
// - ennemis : losange TeamEnemy. Match normal : visibles 3 s
//   apres chaque tir (fondu), sinon caches ; scene spectateur
//   (TdmMatch.spectatorOnly) : toujours visibles.
// Les robots detruits disparaissent de la carte.
// Plaque surface-hud, coin superieur droit coupe (hud-chamfer).
// =========================================================

public class TdmMiniMap : MonoBehaviour
{
    private const float Edge = 16f, Size = 212f, Pad = 10f, Header = 20f;
    // Descendue sous la killfeed (TdmKillfeed : 176 -> 440 px a 1080 p) : 456 -> 688 px
    private const float DropBelowKillfeed = 32f;
    private const float RevealSeconds = 3f;
    public int resolution = 1024;
    public float settleSeconds = 1.2f;

    private TdmMatch match;
    private bool showEnemies;
    private readonly UIKit kit = UIKit.Hud;
    private RectTransform view;
    private CanvasGroup group;
    private RawImage image;
    private Camera mapCamera;
    private RenderTexture texture;
    private Vector3 center;
    private float halfSize = 100f, settle, inner;
    private readonly Dictionary<TdmParticipant, (RectTransform rect, Image img, float lastFire)> marks = new();

    public static TdmMiniMap Create(TdmMatch match)
    {
        var go = new GameObject("[TdmMiniMap]", typeof(RectTransform));
        var map = go.AddComponent<TdmMiniMap>();
        map.match = match;
        map.showEnemies = match.spectatorOnly;
        map.Build();
        return map;
    }

    private void Build()
    {
        UIKit.ScreenCanvas(gameObject, BlockforgeTheme.Order.Hud + 1);
        GetComponent<GraphicRaycaster>().enabled = false;
        var root = (RectTransform)transform;
        var P = kit.P;
        group = kit.Group(root);
        group.blocksRaycasts = false;
        group.alpha = match.Phase == TdmPhase.Live ? 1f : 0f;   // cachee pendant le compte a rebours, comme le HUD

        var plate = ChamferedPlate.Build(kit, root, "MiniMap", PlateCorners.TopRight, BlockforgeTheme.HudChamfer, P.SurfaceHud);
        UIKit.Place(plate.Rect, UIKit.MiddleRight, new Vector2(-Edge, -DropBelowKillfeed), new Vector2(Size, Size + Header));
        var c = plate.Content;

        var title = kit.Text(c, "Title", new TypeSpec(FontFamily.Sans, 11f, 11f, 600, 0.18f), P.InkSecondary,
                             showEnemies ? "Map · all robots" : "Map · allies", TextAlignmentOptions.MidlineLeft, true);
        UIKit.HudShadow(title);
        UIKit.Place(title.rectTransform, UIKit.TopLeft, new Vector2(Pad, -4f), new Vector2(Size - 2f * Pad, 14f));

        inner = Size - 2f * Pad;
        view = kit.Rect(c, "View");
        view.gameObject.AddComponent<RectMask2D>();
        UIKit.Place(view, UIKit.BottomCenter, new Vector2(0f, Pad), new Vector2(inner, inner));
        var back = kit.Image(view, "Back", P.SurfaceInset);
        UIKit.Place(back.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        image = kit.Rect(view, "Terrain").gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.92f);
        UIKit.Place(image.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var north = kit.Text(view, "North", new TypeSpec(FontFamily.Sans, 10f, 10f, 600, 0.1f), P.InkPrimary, "N", TextAlignmentOptions.Top, true);
        UIKit.HudShadow(north);
        UIKit.Place(north.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(20f, 14f));

        Capture();
        match.Spawned += OnSpawned;
        match.PhaseChanged += OnPhase;
        foreach (var p in match.Participants)
            if (p.Robot != null) OnSpawned(p);
    }

    private void OnDestroy()
    {
        if (match != null)
        {
            match.Spawned -= OnSpawned;
            match.PhaseChanged -= OnPhase;
        }
        if (mapCamera != null)
            Destroy(mapCamera.gameObject);
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }

    private void OnPhase(TdmPhase phase) => group.alpha = phase == TdmPhase.Live ? 1f : 0f;

    // ---------- Marques ----------

    private void OnSpawned(TdmParticipant p)
    {
        if (marks.ContainsKey(p))
            return;
        bool ally = match.IsAlly(p);
        Image img;
        if (p.IsLocal)
        {
            img = kit.Icon(view, "Self", "arrow-up", 16f, kit.P.Accent);
            var shadow = kit.Icon(img.transform, "Shadow", "arrow-up", 16f, new Color(0f, 0f, 0f, 0.85f));
            UIKit.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(16f, 16f));
            shadow.transform.SetAsFirstSibling();
        }
        else
        {
            img = kit.TeamMark(view, ally ? Blockforge.Meta.Team.Ally : Blockforge.Meta.Team.Enemy, 10f);
            img.name = "Mark_" + p.Name;
        }
        img.raycastTarget = false;
        UIKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, img.rectTransform.sizeDelta);
        marks[p] = (img.rectTransform, img, -100f);
    }

    private void LateUpdate()
    {
        if (mapCamera != null && mapCamera.enabled)
        {
            settle -= Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            if (settle <= 0f)
                mapCamera.enabled = false;
        }
        if (view == null)
            return;
        float span = 2f * halfSize;
        float now = Time.time;
        var keys = new List<TdmParticipant>(marks.Keys);
        foreach (var p in keys)
        {
            var (rect, img, lastFire) = marks[p];
            bool ally = match.IsAlly(p);
            bool alive = p.Alive && p.Robot != null;
            float alpha = alive ? 1f : 0f;
            if (alive && !ally && !showEnemies)
            {
                // Ennemi : visible le temps d'un tir, puis fondu
                var fire = p.Robot.GetComponent<WeaponFireController>();
                if (fire != null && fire.FireHeld)
                    lastFire = now;
                float since = now - lastFire;
                alpha = since < RevealSeconds ? Mathf.Clamp01((RevealSeconds - since) / 1f) : 0f;
                marks[p] = (rect, img, lastFire);
            }
            if (alpha <= 0f)
            {
                if (img.enabled) img.enabled = false;
                continue;
            }
            if (!img.enabled) img.enabled = true;
            var col = img.color; col.a = alpha; img.color = col;
            Vector3 pos = p.Position;
            float u = (pos.x - (center.x - halfSize)) / span - 0.5f;
            float v = (pos.z - (center.z - halfSize)) / span - 0.5f;
            rect.anchoredPosition = new Vector2(Mathf.Clamp(u, -0.5f, 0.5f) * inner, Mathf.Clamp(v, -0.5f, 0.5f) * inner);
            if (p.IsLocal)
                rect.localRotation = Quaternion.Euler(0f, 0f, -p.Robot.transform.eulerAngles.y);
        }
    }

    // ---------- Photo de la carte ----------

    private void Capture()
    {
        if (!TryMapBounds(out var bounds))
        {
            Debug.LogWarning("[TdmMiniMap] Aucun collider de carte : minimap vide.");
            return;
        }
        center = bounds.center;
        halfSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.02f;

        texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
        {
            name = "TdmMiniMap", useMipMap = true, autoGenerateMips = true, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp,
        };
        texture.Create();
        image.texture = texture;

        var go = new GameObject("TdmMiniMapCamera");
        go.transform.SetPositionAndRotation(new Vector3(center.x, bounds.max.y + 50f, center.z), Quaternion.Euler(90f, 0f, 0f));
        mapCamera = go.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = halfSize;
        mapCamera.aspect = 1f;
        mapCamera.nearClipPlane = 1f;
        mapCamera.farClipPlane = bounds.size.y + 120f;
        mapCamera.cullingMask = ~(1 << RobotTestSpawner.RobotLayer);
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = kit.P.SurfaceInset;
        mapCamera.targetTexture = texture;
        mapCamera.depth = -10f;
        var hd = go.AddComponent<HDAdditionalCameraData>();
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = kit.P.SurfaceInset.linear;
        hd.customRenderingSettings = true;
        foreach (var field in new[]
                 {
                     FrameSettingsField.AtmosphericScattering, FrameSettingsField.Volumetrics, FrameSettingsField.MotionBlur,
                     FrameSettingsField.MotionVectors, FrameSettingsField.DepthOfField, FrameSettingsField.Bloom,
                     FrameSettingsField.ChromaticAberration, FrameSettingsField.FilmGrain, FrameSettingsField.Vignette,
                     FrameSettingsField.LensDistortion,
                 })
        {
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            hd.renderingPathCustomFrameSettings.SetEnabled(field, false);
        }
        settle = settleSeconds;
    }

    // Meme zone que MatchNavMesh : colliders du decor, hors robots et hors decor geant
    private static bool TryMapBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
        {
            if (col.isTrigger || col.gameObject.layer == RobotTestSpawner.RobotLayer || col.attachedRigidbody != null)
                continue;
            var b = col.bounds;
            if (b.size.x > 3000f || b.size.z > 3000f)
                continue;
            if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
        }
        return any;
    }
}
