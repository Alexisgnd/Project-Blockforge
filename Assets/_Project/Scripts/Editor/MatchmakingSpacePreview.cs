using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// APERCU DE LA SCENE MATCHMAKING_SPACE (rendu en PNG)
// Menu : Blockforge > Debug > Render Matchmaking Space Preview
// Ouvre la scene en additif si besoin (sans toucher a la scene
// courante), rend la camera MatchmakingCamera en 1600 x 900 et
// une vue de profil, ecrit Logs/MatchmakingSpace_Camera.png et
// Logs/MatchmakingSpace_Side.png (hors Assets : rien n'est
// importe), puis referme la scene sans la sauvegarder.
// Aussi declenche par un fichier
// Logs/render-matchmaking-preview.request (supprime apres rendu),
// pour verifier le cadrage sans passer par l'editeur.
// =========================================================

[InitializeOnLoad]
public static class MatchmakingSpacePreview
{
    private const string RequestPath = "Logs/render-matchmaking-preview.request";
    private const int Width = 1600, Height = 900;
    private static double nextCheck;

    static MatchmakingSpacePreview()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck)
            return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;
        File.Delete(RequestPath);
        Render();
    }

    [MenuItem("Blockforge/Debug/Render Matchmaking Space Preview")]
    public static void Render()
    {
        string path = BlockforgeScenes.MatchmakingSpace;
        if (!File.Exists(path))
        {
            Debug.LogError("[MatchmakingSpacePreview] Scene absente : lance Blockforge > Setup Matchmaking Space Scene.");
            return;
        }

        var scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        try
        {
            Camera camera = null;
            Transform ship = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "MatchmakingCamera")
                    camera = root.GetComponent<Camera>();
                else if (root.name == "SuperDestroyer")
                    ship = root.transform;
            }
            if (camera == null)
            {
                Debug.LogError("[MatchmakingSpacePreview] MatchmakingCamera introuvable dans la scene.");
                return;
            }

            Directory.CreateDirectory("Logs");
            RenderTo(camera, "Logs/MatchmakingSpace_Camera.png");

            // Vue de profil (cote +X, legerement au-dessus), camera remise en place ensuite
            var pose = (camera.transform.position, camera.transform.rotation);
            Vector3 center = ship != null ? ship.position : Vector3.zero;
            float distance = Vector3.Distance(pose.position, center);
            camera.transform.position = center + new Vector3(1f, 0.15f, 0f).normalized * distance;
            camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, Vector3.up);
            RenderTo(camera, "Logs/MatchmakingSpace_Side.png");
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);

            Debug.Log("[MatchmakingSpacePreview] Apercus ecrits : Logs/MatchmakingSpace_Camera.png, Logs/MatchmakingSpace_Side.png");
        }
        finally
        {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void RenderTo(Camera camera, string file)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        camera.targetTexture = rt;
        for (int i = 0; i < 4; i++) // historiques HDRP (TAA, volumetrie) stabilises
            camera.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());

        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }
}
