using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

// =========================================================
// APERCU D'UN ROBOT DANS UNE TEXTURE (ecran Build)
// Assemble le blueprint (RobotAssembler), le photographie avec
// une camera dediee puis le detruit, le tout dans la meme
// image : il n'apparait jamais a l'ecran.
// - Le robot est pose sur la plateforme du hangar (memes
//   lumieres et meme exposition que la vitrine de l'Accueil) mais
//   sur un calque reserve (Layer) : la camera d'apercu ne voit
//   que lui, la camera du menu ne le voit jamais (desactive avant
//   la fin de l'image).
// - Fond uni surface-inset du design system (pas de ciel).
// - Cadrage de 3/4 avant, distance calculee pour que tout le
//   robot rentre dans l'image quelle que soit sa taille.
// =========================================================

public static class RobotThumbnailRenderer
{
    // Calque utilisateur libre, reserve aux apercus
    public const int Layer = 30;

    private const float FieldOfView = 26f;
    private const float Margin = 0.98f; // marge autour du robot dans le puits
    private static readonly Vector3 ViewDirection = new Vector3(0.62f, 0.32f, 1f).normalized; // 3/4 avant, un peu au-dessus (comme l'Accueil)

    private static Camera camera;

    // Rend 'blueprint' dans 'target' (cree ou recree si la taille change). Renvoie
    // la texture, ou null si le robot est vide ou si aucun bloc n'est connu.
    public static RenderTexture Render(RobotBlueprint blueprint, IReadOnlyDictionary<string, BlockDefinition> defs,
                                       Vector3 stagePosition, int width, int height, RenderTexture target = null)
    {
        if (blueprint == null || blueprint.blocks == null || blueprint.blocks.Count == 0 || defs == null)
            return null;

        var robot = RobotAssembler.Build(blueprint, defs, 1f, null, out int placed, out _);
        if (placed == 0)
        {
            Discard(robot);
            return null;
        }

        robot.name = "RobotThumbnail_" + blueprint.name;
        robot.transform.position = stagePosition;
        foreach (var collider in robot.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        SetLayer(robot.transform, Layer);

        if (!TryGetBounds(robot, out var bounds))
        {
            Discard(robot);
            return null;
        }

        width = Mathf.Clamp(width, 64, 2048);
        height = Mathf.Clamp(height, 64, 2048);
        if (target == null || target.width != width || target.height != height)
        {
            if (target != null)
                target.Release();
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "RobotThumbnail",
                antiAliasing = 1,
                // Rendue a 2x puis affichee plus petite : les mipmaps la reduisent proprement
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            target.Create();
        }

        var cam = GetCamera();
        cam.aspect = (float)width / height;

        // Distance minimale pour que les 8 coins de la boite rentrent dans le cadre
        // (plus serre qu'une sphere englobante : le robot remplit le puits)
        float tanV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * cam.aspect;
        var look = Quaternion.LookRotation(-ViewDirection, Vector3.up);
        Vector3 right = look * Vector3.right, up = look * Vector3.up, forward = look * Vector3.forward;
        float distance = 0.5f;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
            var p = Vector3.Scale(corner, bounds.extents);
            float depthOffset = Vector3.Dot(p, forward);
            distance = Mathf.Max(distance,
                                 Mathf.Abs(Vector3.Dot(p, right)) / tanH - depthOffset,
                                 Mathf.Abs(Vector3.Dot(p, up)) / tanV - depthOffset);
        }
        distance *= Margin;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);

        cam.transform.SetPositionAndRotation(bounds.center + ViewDirection * distance, look);
        cam.nearClipPlane = Mathf.Max(0.05f, distance - radius * 1.2f);
        cam.farClipPlane = distance + radius * 1.5f;
        cam.targetTexture = target;
        cam.Render();
        cam.targetTexture = null;

        Discard(robot);
        return target;
    }

    private static Camera GetCamera()
    {
        if (camera != null)
            return camera;

        var go = new GameObject("RobotThumbnailCamera") { hideFlags = HideFlags.HideAndDontSave };
        camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.cullingMask = 1 << Layer;
        camera.fieldOfView = FieldOfView;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BlockforgeTheme.SurfaceInset;

        var hd = go.AddComponent<HDAdditionalCameraData>();
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        hd.backgroundColorHDR = BlockforgeTheme.SurfaceInset.linear;
        hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        // Image nette : la camera "saute" d'un robot a l'autre entre deux rendus,
        // HDRP y verrait un mouvement enorme (flou de mouvement) ; le bloom et la
        // profondeur de champ du hangar baveraient aussi sur une petite vignette.
        // Pas de brouillard non plus : en match (killfeed), la vignette est rendue loin
        // au-dessus de la carte, et le brouillard de Mars la noierait.
        hd.customRenderingSettings = true;
        foreach (var field in new[]
                 {
                     FrameSettingsField.MotionBlur, FrameSettingsField.MotionVectors, FrameSettingsField.ObjectMotionVectors,
                     FrameSettingsField.DepthOfField, FrameSettingsField.Bloom, FrameSettingsField.ChromaticAberration,
                     FrameSettingsField.FilmGrain, FrameSettingsField.LensDistortion, FrameSettingsField.Vignette,
                     FrameSettingsField.AtmosphericScattering,
                 })
        {
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            hd.renderingPathCustomFrameSettings.SetEnabled(field, false);
        }
        return camera;
    }

    // Retire le robot de l'image en cours (la camera du menu ne doit pas le voir)
    private static void Discard(GameObject robot)
    {
        if (robot == null)
            return;
        robot.SetActive(false);
        if (Application.isPlaying)
            Object.Destroy(robot);
        else
            Object.DestroyImmediate(robot);   // rendus d'editeur (Blockforge > Debug > Bots > Render presets)
    }

    private static void SetLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayer(child, layer);
    }

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return any;
    }
}
