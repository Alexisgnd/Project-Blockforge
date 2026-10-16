using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

// =========================================================
// MINIMAP (HUD de match, en haut a droite)
// - La carte est photographiee une fois, vue de dessus, par une
//   camera orthographique HDRP (sans brouillard ni post-effets),
//   pendant une seconde pour laisser l'exposition se poser (au
//   moins 24 images : le temps de chaque image est plafonne a
//   1/20 s), puis la camera s'eteint : la carte est statique,
//   la texture reste
// - Cadrage : la zone jouable de la map (Map_TestGround,
//   Map_Canyon ou Map_Arena). Sont ignores le decor exterieur
//   du canyon (BACKGROUND_NON_PLAYABLE), les montagnes de fond
//   du terrain d'essai (Backdrop_*, anneau jusqu'a 6 km : sans
//   ce filtre la photo couvrait 12 km et la minimap restait
//   vide) et les rendus desactives (murs invisibles Bounds_*) ;
//   le robot (calque RobotTestSpawner.RobotLayer) n'est jamais
//   photographie
// - Affichage nord en haut, centre sur le robot (fenetre de
//   'range' metres) ; le robot est une fleche accent au centre
//   qui tourne avec son cap
// Plaque surface-hud, coin superieur droit coupe (hud-chamfer).
// =========================================================

public class MiniMap : MonoBehaviour
{
    public float range = 90f;          // demi-largeur de la fenetre affichee (m)
    public int resolution = 1024;
    public float settleSeconds = 1.2f;

    private static readonly string[] MapRoots = { "Map_TestGround", "Map_Canyon", "Map_Arena" };
    private const string NonPlayableGroup = "BACKGROUND_NON_PLAYABLE";
    private const string BackdropPrefix = "Backdrop";

    private Transform robot;
    private Camera mapCamera;
    private RenderTexture texture;
    private RawImage image;
    private RectTransform marker;
    private Vector3 center;
    private float halfSize = 100f;
    private float settle;

    public void Build(RectTransform hudRoot, Transform target)
    {
        robot = target;
        const float size = 232f, pad = 12f, header = 22f;

        var plate = RobotTestHUD.NewImage(hudRoot, "MiniMap", HudShapes.Plate(HudCorner.TopRight), BlockforgeTheme.Hud.SurfaceHud);
        plate.type = Image.Type.Sliced;
        RobotTestHUD.Place(plate.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(size, size + header), new Vector2(1f, 1f));

        var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        title.transform.SetParent(plate.transform, false);
        DSText.Apply(title, DSTextStyle.Label, BlockforgeTheme.Hud.InkSecondary);
        title.text = "Map";
        title.raycastTarget = false;
        title.alignment = TextAlignmentOptions.MidlineLeft;
        RobotTestHUD.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(pad, -pad + 2f), new Vector2(120f, 16f), new Vector2(0f, 1f));

        float inner = size - 2 * pad;
        var mask = new GameObject("View", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        mask.SetParent(plate.transform, false);
        RobotTestHUD.Place(mask, new Vector2(0f, 0f), new Vector2(pad, pad), new Vector2(inner, inner), new Vector2(0f, 0f));

        image = new GameObject("Terrain", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(mask, false);
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0.92f);
        RobotTestHUD.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(inner, inner));

        // Nord
        var north = new GameObject("North", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        north.transform.SetParent(mask, false);
        DSText.Apply(north, DSTextStyle.Label, BlockforgeTheme.Hud.InkPrimary);
        north.text = "N";
        north.alignment = TextAlignmentOptions.Top;
        north.raycastTarget = false;
        RobotTestHUD.Place(north.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(20f, 16f), new Vector2(0.5f, 1f));

        // Le robot : fleche accent (cap), avec ombre
        var shadow = RobotTestHUD.NewImage(mask, "SelfShadow", DSAssets.Icon("arrow-up"), new Color(0f, 0f, 0f, 0.85f));
        RobotTestHUD.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(22f, 22f));
        var arrow = RobotTestHUD.NewImage(mask, "Self", DSAssets.Icon("arrow-up"), BlockforgeTheme.Hud.Accent);
        RobotTestHUD.Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        marker = arrow.rectTransform;
        shadow.transform.SetParent(marker, true);

        Capture();
    }

    // ---------- Photo de la carte ----------

    private void Capture()
    {
        if (!TryGetMapBounds(out var bounds))
        {
            Debug.LogWarning("[MiniMap] Aucune carte trouvee (Map_TestGround / Map_Canyon / Map_Arena) : minimap vide.");
            return;
        }
        center = bounds.center;
        halfSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.02f;

        texture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
        {
            name = "MiniMap",
            useMipMap = true,
            autoGenerateMips = true,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.Create();
        image.texture = texture;

        var go = new GameObject("MiniMapCamera");
        go.transform.SetPositionAndRotation(new Vector3(center.x, bounds.max.y + 50f, center.z), Quaternion.Euler(90f, 0f, 0f));
        mapCamera = go.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = halfSize;
        mapCamera.aspect = 1f;
        mapCamera.nearClipPlane = 1f;
        mapCamera.farClipPlane = bounds.size.y + 120f;
        mapCamera.cullingMask = ~(1 << RobotTestSpawner.RobotLayer);
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = BlockforgeTheme.Hud.SurfaceInset;
        mapCamera.targetTexture = texture;
        mapCamera.depth = -10f;

        var hd = go.AddComponent<HDAdditionalCameraData>();
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = BlockforgeTheme.Hud.SurfaceInset.linear;
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

    private bool TryGetMapBounds(out Bounds bounds)
    {
        bounds = default;
        GameObject map = null;
        foreach (var name in MapRoots)
        {
            map = GameObject.Find(name);
            if (map != null)
                break;
        }
        Renderer[] renderers = map != null ? map.GetComponentsInChildren<Renderer>() : FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        bool any = false;
        foreach (var r in renderers)
        {
            if (!r.enabled || r.gameObject.layer == RobotTestSpawner.RobotLayer || InNonPlayable(r.transform))
                continue;
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return any;
    }

    private static bool InNonPlayable(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name == NonPlayableGroup || t.name.StartsWith(BackdropPrefix, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ---------- Suivi du robot ----------

    private void LateUpdate()
    {
        if (mapCamera != null && mapCamera.enabled)
        {
            // Pas de temps plafonne : au lancement direct de la scene, les
            // premieres images durent plusieurs secondes (chargement, shaders)
            // et la camera s'eteignait avant d'avoir rendu la carte
            settle -= Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            if (settle <= 0f)
                mapCamera.enabled = false; // la carte est statique : la derniere image reste dans la texture
        }

        if (robot == null || image == null || texture == null)
            return;
        float span = 2f * halfSize;
        float u = (robot.position.x - (center.x - halfSize)) / span;
        float v = (robot.position.z - (center.z - halfSize)) / span;
        float w = Mathf.Clamp(range * 2f / span, 0.02f, 1f);
        image.uvRect = new Rect(u - w * 0.5f, v - w * 0.5f, w, w);

        float yaw = robot.eulerAngles.y;
        marker.localRotation = Quaternion.Euler(0f, 0f, -yaw);
    }

    private void OnDestroy()
    {
        if (mapCamera != null)
            Destroy(mapCamera.gameObject);
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }
}
