using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// =========================================================
// APERCU D'UN ROBOT DANS UNE TEXTURE (ecran Build, killfeed,
// rendus de presets)
// Assemble le blueprint (RobotAssembler, peinture comprise),
// le photographie avec une camera dediee puis le detruit, le
// tout dans la meme image : il n'apparait jamais a l'ecran.
// Studio photo isole de la scene :
// - Calque reserve (Layer) : la camera d'apercu ne voit que le
//   robot, et ignore les volumes et les sondes de reflexion de
//   la scene (volumeLayerMask / probeLayerMask sur ce calque).
// - Volume propre, sur ce calque : exposition FIXE (ExposureEV),
//   pas de tonemapping (le fond garde exactement la couleur du
//   design system), ciel degrade neutre pour les reflets des
//   metaux.
// - Eclairage trois points en lux (key 3/4 haut-gauche, fill
//   sur le flanc droit, rim de dos), allume le temps du rendu
//   seulement, + ambiance uniforme fournie aux blocs (harmoniques
//   spheriques) : aucune face visible n'est noire, les couleurs
//   de peinture restent fideles. Les lumieres directionnelles de
//   la scene (soleil de Mars...) sont eteintes le temps du rendu ;
//   les lumieres locales n'atteignent pas une scene photo eloignee
//   (DefaultStage).
// - Fond uni surface-inset du design system (pas de ciel).
// - Cadrage de 3/4 avant : robot recentre dans l'image, distance
//   calculee pour que sa boite occupe Fill du cadre.
// =========================================================

public static class RobotThumbnailRenderer
{
    // Calque utilisateur libre, reserve aux apercus
    public const int Layer = 30;

    // Scene photo par defaut : loin sous les cartes, hors de portee des lumieres locales
    public static readonly Vector3 DefaultStage = new Vector3(0f, -6000f, 0f);

    private const float FieldOfView = 26f;
    private const float Fill = 0.9f; // part du cadre occupee par la boite du robot (axe limitant)
    private static readonly Vector3 ViewDirection = new Vector3(0.62f, 0.32f, 1f).normalized; // 3/4 avant, un peu au-dessus (comme l'Accueil)

    // ---------- Studio (unites physiques HDRP) ----------
    // Exposition fixe EV100 = 10 : multiplicateur 1 / (1.2 * 2^10). Avec les
    // reglages ci-dessous, un blanc (albedo 0.8) sort a ~0.75 lineaire sur le
    // dessus, ~0.6 de face, ~0.45 de flanc : trois valeurs nettes, sans cramer.
    private const float ExposureEV = 10f;
    // Directions VERS la lumiere (repere monde ; la camera regarde depuis +X +Z)
    private static readonly Vector3 KeyDirection = new Vector3(-0.25f, 0.75f, 0.6f).normalized;  // haut-gauche : dessus + face avant
    private static readonly Vector3 FillDirection = new Vector3(0.9f, 0.15f, 0.4f).normalized;   // flanc droit, plus bas
    private static readonly Vector3 RimDirection = new Vector3(0.3f, 0.6f, -0.75f).normalized;   // de dos : detache la silhouette
    private const float KeyLux = 3000f;
    private const float FillLux = 1300f;
    private const float RimLux = 1000f;
    private static readonly Color KeyColor = new Color(1f, 0.97f, 0.93f);
    private static readonly Color FillColor = new Color(0.88f, 0.93f, 1f);
    private static readonly Color RimColor = new Color(0.92f, 0.96f, 1f);
    // Ambiance uniforme (meme echelle que lux / pi) : ~20 % du key sur toutes les faces
    private const float AmbientLevel = 200f;
    // Ciel degrade (reflets des metaux, et ambiance si la scene utilise les APV)
    private const float SkyMultiplier = 240f;

    private static Camera camera;
    private static Light[] rigLights;
    private static MaterialPropertyBlock block;
    private static readonly SphericalHarmonicsL2[] ambient = new SphericalHarmonicsL2[1];
    private static readonly List<Light> sceneLights = new List<Light>();

    // Rend 'blueprint' dans 'target' (cree ou recree si la taille change). Renvoie
    // la texture, ou null si le robot est vide ou si aucun bloc n'est connu.
    // stagePosition : de preference DefaultStage (ou un point loin de toute lumiere locale).
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
        UseStudioAmbient(robot);

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
        Frame(cam, bounds);

        cam.targetTexture = target;
        RenderLit(cam);
        cam.targetTexture = null;

        Discard(robot);
        return target;
    }

    // =========================================================
    // CADRAGE
    // =========================================================

    private static void Frame(Camera cam, Bounds bounds)
    {
        float tanV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * cam.aspect;
        var look = Quaternion.LookRotation(-ViewDirection, Vector3.up);
        Vector3 right = look * Vector3.right, up = look * Vector3.up, forward = look * Vector3.forward;

        // 1) Distance ou les 8 coins de la boite tiennent a coup sur (borne
        //    symetrique autour du centre)
        float distance = 0.5f;
        for (int i = 0; i < 8; i++)
        {
            var p = Vector3.Scale(Corner(i), bounds.extents);
            float depthOffset = Vector3.Dot(p, forward);
            distance = Mathf.Max(distance,
                                 Mathf.Abs(Vector3.Dot(p, right)) / tanH - depthOffset,
                                 Mathf.Abs(Vector3.Dot(p, up)) / tanV - depthOffset);
        }
        Vector3 eye = bounds.center + ViewDirection * distance;

        // 2) Recentrage + zoom : vue de 3/4 et en perspective, la projection de la
        //    boite n'est pas centree sur son centre. Quelques passes convergent.
        for (int pass = 0; pass < 3; pass++)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 q = bounds.center + Vector3.Scale(Corner(i), bounds.extents) - eye;
                float z = Mathf.Max(0.05f, Vector3.Dot(q, forward));
                float x = Vector3.Dot(q, right) / (z * tanH);
                float y = Vector3.Dot(q, up) / (z * tanV);
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            float depth = Mathf.Max(0.05f, Vector3.Dot(bounds.center - eye, forward));
            float half = Mathf.Max(0.02f, Mathf.Max(maxX - minX, maxY - minY) * 0.5f);
            // Centre de l'image sur le centre de la projection (decalage a la profondeur du robot)
            eye += right * ((minX + maxX) * 0.5f * tanH * depth) + up * ((minY + maxY) * 0.5f * tanV * depth);
            // Avance ou recule pour que la boite occupe Fill du cadre
            eye += forward * (depth - depth * half / Fill);
        }

        float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        float centerDepth = Vector3.Dot(bounds.center - eye, forward);
        cam.transform.SetPositionAndRotation(eye, look);
        cam.nearClipPlane = Mathf.Max(0.05f, centerDepth - radius * 1.2f);
        cam.farClipPlane = centerDepth + radius * 1.5f;
    }

    private static Vector3 Corner(int i)
    {
        return new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
    }

    // =========================================================
    // ECLAIRAGE DU STUDIO
    // =========================================================

    // Rend avec les lumieres du studio seules : elles sont allumees, et les
    // directionnelles de la scene eteintes, le temps de ce seul rendu
    private static void RenderLit(Camera cam)
    {
        sceneLights.Clear();
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (light != null && light.enabled && light.type == LightType.Directional &&
                System.Array.IndexOf(rigLights, light) < 0)
            {
                sceneLights.Add(light);
            }
        }

        try
        {
            foreach (var light in sceneLights)
                light.enabled = false;
            Aim(rigLights[0], KeyDirection);
            Aim(rigLights[1], FillDirection);
            Aim(rigLights[2], RimDirection);
            foreach (var light in rigLights)
            {
                if (light != null)
                    light.enabled = true;
            }
            cam.Render();
        }
        finally
        {
            foreach (var light in rigLights)
            {
                if (light != null)
                    light.enabled = false;
            }
            foreach (var light in sceneLights)
            {
                if (light != null)
                    light.enabled = true;
            }
            sceneLights.Clear();
        }
    }

    private static void Aim(Light light, Vector3 towardLight)
    {
        if (light != null)
            light.transform.rotation = Quaternion.LookRotation(-towardLight, Vector3.up);
    }

    // Ambiance uniforme donnee directement aux blocs : ni les sondes de la
    // scene (hangar sombre, carte) ni la sonde d'ambiance du ciel (calculee
    // en differe sur le GPU, pas prete au premier rendu) n'entrent en jeu
    private static void UseStudioAmbient(GameObject robot)
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(new Color(1f, 1f, 1.03f) * AmbientLevel);
            ambient[0] = sh;
        }

        foreach (var renderer in robot.GetComponentsInChildren<Renderer>(true))
        {
            renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
            renderer.GetPropertyBlock(block);
            block.CopySHCoefficientArraysFrom(ambient);
            renderer.SetPropertyBlock(block);
        }
    }

    // =========================================================
    // CAMERA, LUMIERES ET VOLUME DU STUDIO (crees une fois)
    // =========================================================

    private static Camera GetCamera()
    {
        if (camera != null && rigLights != null)
            return camera;
        if (camera != null)
            DestroyRig(camera.gameObject);

        var go = new GameObject("RobotThumbnailCamera") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
        camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.cullingMask = 1 << Layer;
        camera.fieldOfView = FieldOfView;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BlockforgeTheme.SurfaceInset;

        var hd = go.AddComponent<HDAdditionalCameraData>();
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        // Couleur ecrite telle quelle (ni exposition ni tonemapping) : fond = token exact
        hd.backgroundColorHDR = BlockforgeTheme.SurfaceInset.linear;
        hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        // Isolee de la scene : seul le volume du studio (meme calque) s'applique,
        // et aucune sonde de reflexion de la scene (reflets = ciel du studio)
        hd.volumeLayerMask = 1 << Layer;
        hd.probeLayerMask = 1 << Layer;

        // Image nette : la camera "saute" d'un robot a l'autre entre deux rendus,
        // HDRP y verrait un mouvement enorme (flou de mouvement) ; le bloom et la
        // profondeur de champ baveraient aussi sur une petite vignette.
        // Pas de brouillard ni de nuages volumetriques : en match (killfeed), la
        // vignette est rendue loin au-dessus de la carte.
        hd.customRenderingSettings = true;
        foreach (var field in new[]
                 {
                     FrameSettingsField.MotionBlur, FrameSettingsField.MotionVectors, FrameSettingsField.ObjectMotionVectors,
                     FrameSettingsField.DepthOfField, FrameSettingsField.Bloom, FrameSettingsField.ChromaticAberration,
                     FrameSettingsField.FilmGrain, FrameSettingsField.LensDistortion, FrameSettingsField.Vignette,
                     FrameSettingsField.AtmosphericScattering, FrameSettingsField.Volumetrics,
                     FrameSettingsField.VolumetricClouds,
                 })
        {
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
            hd.renderingPathCustomFrameSettings.SetEnabled(field, false);
        }

        rigLights = new[]
        {
            CreateLight(go.transform, "Key", KeyColor, KeyLux),
            CreateLight(go.transform, "Fill", FillColor, FillLux),
            CreateLight(go.transform, "Rim", RimColor, RimLux),
        };
        CreateVolume(go.transform);
        return camera;
    }

    // Directionnelle HDRP : intensite en lux. Eteinte hors des rendus d'apercu.
    private static Light CreateLight(Transform parent, string name, Color color, float lux)
    {
        var go = new GameObject("RobotThumbnail" + name) { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
        go.transform.SetParent(parent, false);
        var light = go.AddComponent<Light>();
        light.enabled = false;
        light.type = LightType.Directional;
        if (!go.TryGetComponent<HDAdditionalLightData>(out var hd))
            hd = go.AddComponent<HDAdditionalLightData>();
        hd.affectsVolumetric = false;
        light.shadows = LightShadows.None;
        light.useColorTemperature = false;
        light.color = color;
        light.intensity = lux;
        return light;
    }

    // Volume global sur le calque des apercus : seule la camera d'apercu le voit
    private static void CreateVolume(Transform parent)
    {
        var go = new GameObject("RobotThumbnailVolume") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
        go.transform.SetParent(parent, false);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "RobotThumbnailProfile";
        profile.hideFlags = HideFlags.HideAndDontSave;

        var exposure = profile.Add<Exposure>();
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(ExposureEV);
        exposure.compensation.Override(0f);

        var tonemapping = profile.Add<Tonemapping>();
        tonemapping.mode.Override(TonemappingMode.None);

        var environment = profile.Add<VisualEnvironment>();
        environment.skyType.Override((int)SkyType.Gradient);
        environment.cloudType.Override(0);
        environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

        var sky = profile.Add<GradientSky>();
        sky.top.Override(new Color(1f, 1f, 1.04f));
        sky.middle.Override(new Color(0.82f, 0.84f, 0.88f));
        sky.bottom.Override(new Color(0.42f, 0.43f, 0.46f));
        sky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);
        sky.multiplier.Override(SkyMultiplier);

        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    private static void DestroyRig(GameObject root)
    {
        if (Application.isPlaying)
            Object.Destroy(root);
        else
            Object.DestroyImmediate(root);
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
