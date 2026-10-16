using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// =========================================================
// IMPORT DU TERRAIN D'ESSAI (Art/Models/Environment/Map_TestGround.fbx)
// Modele genere par Tools/Maps/TestGround/generate_testground.py
// (Blender). Ne touche a aucun autre modele.
// - OnPreprocessModel : echelle du fichier, pas de camera, de
//   lumiere ni d'animation (pas d'Animator inutile sur 2 000
//   objets), pas de collider auto (MapTestSceneSetup pose les
//   MeshCollider en excluant les marquages au sol), materiaux
//   via MaterialDescription (textures grille de
//   Art/Textures/Map/TestGround)
// - OnPostprocessGameObjectWithUserProperties : la racine de
//   chaque cible mobile porte des proprietes FBX bf_* ecrites par
//   Blender (bf_move = linear | rotate | popup, bf_speed,
//   bf_pingpong, bf_angle) -> MovingTarget configure directement
//   dans le modele importe
// - EnsureMaterials (appele par le setup avant d'instancier) :
//   l'import par MaterialDescription perd la texture grille des
//   materiaux M_TG_<Nom> (ils arrivent blancs). Comme pour les
//   blocs (<Famille>MaterialSetup), on cree des materiaux
//   HDRP/Lit dans Art/Materials/Map/TestGround (_BaseColorMap =
//   Art/Textures/Map/TestGround/T_TG_<Nom>.png) et on remappe le
//   FBX dessus ; les materiaux a couleur unie restent ceux du FBX
// - ApplyLighting (appele apres le setup) : profil de volume
//   propre au terrain d'essai (copie du profil Sky and Fog) avec
//   lumiere ambiante DYNAMIQUE (le mode statique du profil partage
//   attend un eclairage precalcule qui n'existe pas : toute face
//   tournee dos au soleil sortait noire), brouillard allege pour
//   600 m, lumiere indirecte x2.5 (ombres lisibles) ; soleil
//   haut en diagonale avec ombres douces + lumiere de remplissage
//   opposee sans ombre (faces et falaises dos au soleil) ; les
//   remparts et les montagnes lointaines (Backdrop_Mountains, 6 km)
//   ne projettent pas d'ombre ; camera vue jusqu'a 8 km
// - PrepareInstance (appele par MapTestSceneSetup sur la map
//   instanciee) : murs invisibles Bounds_* = BoxCollider sans
//   rendu (un MeshCollider de 760 m declenche l'avertissement
//   « triangles > 500 unites ») et ajoute MovingTarget, regle
//   d'apres le nom, aux cibles mobiles dont l'import n'a pas
//   transmis les proprietes
// =========================================================

public class MapTestGroundImporter : AssetPostprocessor
{
    public const string ModelPath = "Assets/_Project/Art/Models/Environment/Map_TestGround.fbx";

    private static readonly string[] MoverPrefixes = { "MoveLinear_", "MoveRotor_", "MoveOrbit_", "MovePopup_" };

    // A incrementer quand les regles d'import changent (force la reimportation)
    public override uint GetVersion() => 1;

    private static bool IsTestGround(string path)
    {
        return path.Replace('\\', '/').EndsWith("/Map_TestGround.fbx", System.StringComparison.OrdinalIgnoreCase);
    }

    private void OnPreprocessModel()
    {
        if (!IsTestGround(assetPath))
            return;

        var importer = (ModelImporter)assetImporter;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.importBlendShapes = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.addCollider = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    }

    private void OnPostprocessGameObjectWithUserProperties(GameObject go, string[] propNames, object[] values)
    {
        if (!IsTestGround(assetPath))
            return;

        string move = null;
        float? speed = null, angle = null;
        bool? pingPong = null;
        for (int i = 0; i < propNames.Length; i++)
        {
            switch (propNames[i])
            {
                case "bf_move": move = values[i]?.ToString(); break;
                case "bf_speed": speed = ToFloat(values[i]); break;
                case "bf_angle": angle = ToFloat(values[i]); break;
                case "bf_pingpong": pingPong = ToFloat(values[i]) is float p && p != 0f; break;
            }
        }
        if (string.IsNullOrEmpty(move))
            return;

        if (!go.TryGetComponent<MovingTarget>(out var target))
            target = go.AddComponent<MovingTarget>();
        target.mode = move.Trim().ToLowerInvariant() switch
        {
            "rotate" => MovingTarget.Mode.Rotate,
            "popup" => MovingTarget.Mode.Popup,
            _ => MovingTarget.Mode.Linear,
        };
        if (speed.HasValue) target.speed = speed.Value;
        if (angle.HasValue) target.angle = angle.Value;
        if (pingPong.HasValue) target.pingPong = pingPong.Value;
    }

    private static float? ToFloat(object value)
    {
        try
        {
            return value == null ? (float?)null : System.Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        catch (System.Exception)
        {
            return float.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : (float?)null;
        }
    }

    private const string TextureDir = "Assets/_Project/Art/Textures/Map/TestGround";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Map/TestGround";

    // Materiaux grille HDRP/Lit + remap du FBX. Retourne le nombre de materiaux remappes.
    public static int EnsureMaterials()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            return 0;

        if (!AssetDatabase.IsValidFolder(MaterialDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Materials/Map"))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Map");
            AssetDatabase.CreateFolder("Assets/_Project/Art/Materials/Map", "TestGround");
        }

        var shader = Shader.Find("HDRP/Lit");
        var existingRemaps = importer.GetExternalObjectMap();
        bool changed = false;
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D T_TG_", new[] { TextureDir }))
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(guid);
            string suffix = System.IO.Path.GetFileNameWithoutExtension(texturePath).Substring("T_TG_".Length);
            string materialName = "M_TG_" + suffix;
            string materialPath = $"{MaterialDir}/{materialName}.mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);

            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName);
            if (!existingRemaps.TryGetValue(id, out var current) || current != material)
            {
                importer.AddRemap(id, material);
                changed = true;
            }
            count++;
        }

        AssetDatabase.SaveAssets();
        if (changed)
            importer.SaveAndReimport();
        return count;
    }

    private const string TemplateProfilePath = "Assets/Settings/SkyandFogSettingsProfile.asset";
    private const string TestGroundProfilePath = "Assets/_Project/Art/MapTest/TestGroundVolumeProfile.asset";

    private const string FillLightName = "Fill Light (TestGround)";

    // Lumiere du terrain d'essai sur la scene ouverte (Map_Test), puis sauvegarde
    public static void ApplyLighting()
    {
        // 1. Profil de volume dedie (le profil partage sert aussi a Map_MARS)
        var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(TestGroundProfilePath);
        if (profile == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/MapTest"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "MapTest");
            if (AssetDatabase.CopyAsset(TemplateProfilePath, TestGroundProfilePath))
                profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(TestGroundProfilePath);
        }
        if (profile != null)
        {
            if (profile.TryGet(out UnityEngine.Rendering.HighDefinition.VisualEnvironment env))
                env.skyAmbientMode.Override(UnityEngine.Rendering.HighDefinition.SkyAmbientMode.Dynamic);
            // Lumiere indirecte renforcee : les ombres et les faces dos au soleil restent lisibles
            if (!profile.TryGet(out UnityEngine.Rendering.HighDefinition.IndirectLightingController indirect))
            {
                indirect = profile.Add<UnityEngine.Rendering.HighDefinition.IndirectLightingController>(true);
                AssetDatabase.AddObjectToAsset(indirect, profile);
            }
            indirect.indirectDiffuseLightingMultiplier.Override(2.5f);

            if (profile.TryGet(out UnityEngine.Rendering.HighDefinition.Fog fog))
            {
                fog.meanFreePath.Override(1500f);   // 400 m : le fond de la map etait noye
                fog.anisotropy.Override(0.2f);      // 0.65 : gros halo face au soleil
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            foreach (var volume in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include))
            {
                if (!volume.isGlobal)
                    continue;
                volume.sharedProfile = profile;
                EditorUtility.SetDirty(volume);
            }
        }

        // 2. Soleil : haut (55 deg) et en diagonale, pour que les faces tournees
        //    vers le hub (complexe, panneaux) soient eclairees ; ombres douces
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.type != LightType.Directional || light.name == FillLightName)
                continue;
            light.transform.rotation = Quaternion.Euler(55f, 45f, 0f);
            light.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(light.transform);
        }

        // 3. Remplissage : lumiere opposee au soleil, sans ombre ni brouillard,
        //    a 40 % : eclaire les faces et les falaises tournees dos au soleil
        var fillGo = GameObject.Find(FillLightName);
        if (fillGo == null)
            fillGo = new GameObject(FillLightName);
        fillGo.transform.rotation = Quaternion.Euler(35f, 225f, 0f);
        if (!fillGo.TryGetComponent<Light>(out var fill))
            fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.shadows = LightShadows.None;
        if (!fillGo.TryGetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>(out var fillData))
            fillData = fillGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
#pragma warning disable 618 // meme API que MapTestSceneSetup.EnsureSun
        fillData.SetIntensity(40000f, LightUnit.Lux);
#pragma warning restore 618
        fillData.volumetricDimmer = 0f;
        EditorUtility.SetDirty(fillGo);

        // 4. Les remparts (falaises de 50 m) et les montagnes lointaines ne font pas
        //    d'ombre sur la zone de jeu
        foreach (var name in new[] { "Ramparts", "Backdrop_Mountains" })
        {
            var go = GameObject.Find(name);
            if (go != null && go.TryGetComponent<MeshRenderer>(out var shadowless))
            {
                shadowless.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                EditorUtility.SetDirty(shadowless);
            }
        }

        // 5. Camera : les montagnes vont jusqu'a 6 km (2 km avant : vide visible en vol)
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (cam.farClipPlane < 8000f)
            {
                cam.farClipPlane = 8000f;
                EditorUtility.SetDirty(cam);
            }
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MapTestGroundImporter] Lumiere du terrain d'essai : ambiance dynamique x2.5, brouillard allege, soleil 55/45 avec ombres, remplissage 40 % sans ombre, remparts sans ombre portee.");
    }

    // Retourne (cibles mobiles pretes, murs invisibles). Sans effet sur une map
    // qui n'a ni Bounds_* ni Move* : appele pour toutes les maps de test.
    public static (int movers, int bounds) PrepareInstance(GameObject root)
    {
        int movers = 0, bounds = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Bounds_"))
            {
                if (t.TryGetComponent<MeshRenderer>(out var renderer))
                {
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
                if (t.TryGetComponent<MeshCollider>(out var meshCollider))
                    Object.DestroyImmediate(meshCollider);
                if (!t.TryGetComponent<BoxCollider>(out _) && t.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                {
                    var box = t.gameObject.AddComponent<BoxCollider>();
                    box.center = filter.sharedMesh.bounds.center;
                    box.size = filter.sharedMesh.bounds.size;
                }
                bounds++;
                continue;
            }

            // Racine d'une cible mobile : nom prefixe, parent non prefixe
            // (ses enfants _Carriage, _Rotor, _Hinge... portent le meme prefixe)
            if (!HasMoverPrefix(t.name) || (t.parent != null && HasMoverPrefix(t.parent.name)))
                continue;

            if (!t.TryGetComponent<MovingTarget>(out _))
            {
                var target = t.gameObject.AddComponent<MovingTarget>();
                ApplyDefaultsFromName(target, t.name);
            }
            movers++;
        }
        return (movers, bounds);
    }

    private static bool HasMoverPrefix(string name)
    {
        foreach (var prefix in MoverPrefixes)
        {
            if (name.StartsWith(prefix))
                return true;
        }
        return false;
    }

    // Repli quand l'import n'a pas transmis bf_* : valeurs moyennes du generateur
    private static void ApplyDefaultsFromName(MovingTarget target, string name)
    {
        if (name.StartsWith("MoveLinear_"))
        {
            target.mode = MovingTarget.Mode.Linear;
            target.speed = 5f;
        }
        else if (name.StartsWith("MoveRotor_"))
        {
            target.mode = MovingTarget.Mode.Rotate;
            target.speed = 35f;
        }
        else if (name.StartsWith("MoveOrbit_"))
        {
            target.mode = MovingTarget.Mode.Rotate;
            target.speed = 18f;
        }
        else
        {
            target.mode = MovingTarget.Mode.Popup;
            target.angle = 90f;
        }
        EditorUtility.SetDirty(target);
    }
}
