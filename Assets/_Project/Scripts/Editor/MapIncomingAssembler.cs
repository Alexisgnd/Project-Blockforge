using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// =========================================================
// RECONSTITUTION D'UNE MAP ENVOYEE EN MORCEAUX
// Un fichier trop gros pour etre copie d'un bloc (outil externe,
// session Claude) arrive dans MapIncoming/ a la racine du projet
// (hors Assets : rien n'est importe) en morceaux
// <nom>.part0..N et un manifeste <nom>.manifest (nombre de
// morceaux, taille, SHA-256). Toutes les 3 s hors Play mode,
// quand tous les morceaux sont la : ils sont recolles, la taille
// et le SHA-256 sont verifies, le fichier est pose a la racine du
// projet (ou MapMarsSceneSetup le prend : deplacement dans
// Art/Models, import, mise a jour de Map_MARS) et MapIncoming/
// est vide. Echec de verification : morceaux conserves, erreur
// dans la console.
// =========================================================

[InitializeOnLoad]
public static class MapIncomingAssembler
{
    private const string Folder = "MapIncoming";
    private static double nextPoll;

    static MapIncomingAssembler()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll)
            return;
        nextPoll = EditorApplication.timeSinceStartup + 3.0;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (!Directory.Exists(Folder))
            return;
        foreach (var manifest in Directory.GetFiles(Folder, "*.manifest"))
            TryAssemble(manifest);
    }

    private static void TryAssemble(string manifestPath)
    {
        string name = Path.GetFileNameWithoutExtension(manifestPath);   // Map_Mars_TDM.glb
        string[] lines = File.ReadAllLines(manifestPath);
        if (lines.Length < 3 || !int.TryParse(lines[0].Trim(), out int count) || !long.TryParse(lines[1].Trim(), out long size))
            return;
        string sha = lines[2].Trim().ToLowerInvariant();
        for (int i = 0; i < count; i++)
        {
            if (!File.Exists(Path.Combine(Folder, $"{name}.part{i}")))
                return;   // un morceau manque encore : on attend
        }

        string temp = Path.Combine(Folder, name + ".tmp");
        try
        {
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < count; i++)
                {
                    using var input = File.OpenRead(Path.Combine(Folder, $"{name}.part{i}"));
                    input.CopyTo(output);
                }
            }
            long actual = new FileInfo(temp).Length;
            string hash;
            using (var stream = File.OpenRead(temp))
            using (var sha256 = SHA256.Create())
                hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (actual != size || hash != sha)
            {
                File.Delete(temp);
                Debug.LogError($"[MapIncoming] {name} : verification echouee ({actual} octets, attendu {size} ; SHA-256 {(hash == sha ? "ok" : "different")}). Morceaux conserves.");
                return;
            }
            if (File.Exists(name))
                File.Delete(name);
            File.Move(temp, name);
            for (int i = 0; i < count; i++)
                File.Delete(Path.Combine(Folder, $"{name}.part{i}"));
            File.Delete(manifestPath);
            if (Directory.GetFileSystemEntries(Folder).Length == 0)
                Directory.Delete(Folder);
            Debug.Log($"[MapIncoming] {name} reconstitue ({size} octets, SHA-256 verifie) et pose a la racine du projet.");
        }
        catch (Exception e)
        {
            if (File.Exists(temp))
                File.Delete(temp);
            Debug.LogError($"[MapIncoming] {name} : {e.Message}");
        }
    }
}
