using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// =========================================================
// PILOTAGE EXTERNE DE L'EDITEUR (setups, scenes, Play mode)
// Un outil externe (script, session Claude) depose
// Library/BlockforgeRun.request avec une commande par ligne
// (lignes vides et "#" ignorees) :
//   Blockforge/Setup Map Test Scene   -> execute ce menu
//   open:Assets/_Project/Scenes/X.unity -> ouvre la scene
//   play                               -> entre en Play mode
//                                          (fin de la requete)
//   stop                               -> quitte le Play mode
// L'editeur ouvert le detecte (toutes les 2 s, hors
// compilation), execute les commandes dans l'ordre, sauvegarde
// les assets et ecrit le compte rendu dans
// Library/BlockforgeRun.result. En Play mode, seule une requete
// "stop" est acceptee, les autres attendent. Le fichier de
// requete est consomme avant l'execution : une seule tentative,
// meme en cas d'exception. Rien ne se passe sans fichier.
// Menu : Blockforge > Run Batch Request File (forcage manuel).
// =========================================================

[InitializeOnLoad]
public static class BlockforgeBatch
{
    private const string RequestPath = "Library/BlockforgeRun.request";
    private const string ResultPath = "Library/BlockforgeRun.result";
    private const double PollIntervalSeconds = 2.0;

    private static double nextPoll;

    static BlockforgeBatch()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll)
            return;
        nextPoll = EditorApplication.timeSinceStartup + PollIntervalSeconds;

        if (File.Exists(RequestPath))
            Run();
    }

    [MenuItem("Blockforge/Run Batch Request File")]
    public static void Run()
    {
        if (!File.Exists(RequestPath))
        {
            Debug.Log($"[BlockforgeBatch] Aucun fichier de requete ({RequestPath}).");
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        string[] commands = File.ReadAllLines(RequestPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#"))
            .ToArray();

        // En Play mode : seule une requete "stop" passe, le reste attend la sortie
        bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
        if (playing && commands.Any(c => c != "stop"))
            return;

        File.Delete(RequestPath);

        var report = new System.Text.StringBuilder();
        report.AppendLine($"# {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        foreach (var command in commands)
        {
            try
            {
                Debug.Log($"[BlockforgeBatch] > {command}");
                if (command == "play")
                {
                    EditorApplication.EnterPlaymode();
                    report.AppendLine("OK   play (la suite de la requete est ignoree : rechargement de domaine)");
                    break;
                }
                if (command == "stop")
                {
                    EditorApplication.ExitPlaymode();
                    report.AppendLine("OK   stop");
                    continue;
                }
                if (command.StartsWith("open:"))
                {
                    bool opened = BlockforgeScenes.OpenForSetup(command.Substring(5).Trim(), out _);
                    report.AppendLine($"{(opened ? "OK  " : "FAIL")} {command}");
                    continue;
                }

                bool ok = EditorApplication.ExecuteMenuItem(command);
                report.AppendLine($"{(ok ? "OK  " : "FAIL")} {command}");
                if (!ok)
                    Debug.LogWarning($"[BlockforgeBatch] Menu introuvable ou refuse : {command}");
            }
            catch (System.Exception e)
            {
                report.AppendLine($"EXC  {command} : {e.GetType().Name} {e.Message}");
                Debug.LogException(e);
            }
        }

        if (!playing)
            AssetDatabase.SaveAssets();
        File.WriteAllText(ResultPath, report.ToString());
        Debug.Log($"[BlockforgeBatch] Termine, compte rendu : {ResultPath}\n{report}");
    }
}
