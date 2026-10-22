using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

// =========================================================
// COMPTE RENDU DE COMPILATION ET DE CONSOLE (pilotage externe)
// Complement de BlockforgeBatch pour un outil externe (session
// Claude) qui ne voit pas la console de l'editeur :
// - Library/BlockforgeCompile.log : chaque compilation, assembly
//   par assembly, avec ses erreurs (fichier, ligne, message).
//   Le domaine deja charge ecrit le rapport : meme quand la
//   nouvelle compilation echoue, le fichier dit pourquoi.
// - Library/BlockforgeConsole.log : erreurs, exceptions et
//   avertissements de la console (editeur et Play mode), plus
//   les lignes [Tdm] / [Bot] du match contre les bots. Vide a
//   chaque entree en Play mode, limite a ~400 Ko.
// - Library/BlockforgeRun.background (fichier temoin pose par
//   l'outil externe avant un "play") : le jeu tourne sans le focus
//   de l'editeur pour ce Play mode seulement (temoin consomme).
// Aucun effet sur le jeu : code editeur uniquement.
// =========================================================

[InitializeOnLoad]
public static class BlockforgeCompileReporter
{
    private const string CompileLog = "Library/BlockforgeCompile.log";
    private const string ConsoleLog = "Library/BlockforgeConsole.log";
    private const long ConsoleMaxBytes = 400_000;
    private const string BackgroundFlag = "Library/BlockforgeRun.background";

    private static readonly object fileLock = new();

    static BlockforgeCompileReporter()
    {
        CompilationPipeline.compilationStarted += _ => Write(CompileLog, $"# compilation demarree {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", false);
        CompilationPipeline.assemblyCompilationFinished += OnAssembly;
        CompilationPipeline.compilationFinished += _ => Write(CompileLog, $"# compilation terminee {DateTime.Now:HH:mm:ss}\n", true);

        Application.logMessageReceivedThreaded -= OnLog;
        Application.logMessageReceivedThreaded += OnLog;
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void OnAssembly(string assembly, CompilerMessage[] messages)
    {
        var sb = new StringBuilder();
        int errors = 0;
        foreach (var m in messages)
        {
            if (m.type != CompilerMessageType.Error)
                continue;
            errors++;
            sb.AppendLine($"ERROR {m.file}({m.line},{m.column}): {m.message}");
        }
        Write(CompileLog, $"## {Path.GetFileName(assembly)} : {(errors == 0 ? "OK" : errors + " erreur(s)")}\n{sb}", true);
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            Write(ConsoleLog, $"# Play mode {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", false);
            // Test pilote de l'exterieur (fichier temoin) : le jeu tourne meme si l'editeur
            // n'a pas le focus. Reglage de la session de jeu seulement, rien n'est sauvegarde.
            if (File.Exists(BackgroundFlag))
            {
                File.Delete(BackgroundFlag);
                Application.runInBackground = true;
                Write(ConsoleLog, "# runInBackground force pour ce test\n", true);
            }
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
            Write(ConsoleLog, $"# sortie du Play mode {DateTime.Now:HH:mm:ss}\n", true);
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        bool tagged = condition != null && (condition.StartsWith("[Tdm]") || condition.StartsWith("[Bot]") || condition.StartsWith("[MatchNav]"));
        if (type == LogType.Log && !tagged)
            return;
        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ').Append(type).Append(' ').AppendLine(condition);
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            // Les 6 premieres lignes de la pile suffisent a situer l'erreur
            var lines = (stackTrace ?? "").Split('\n');
            for (int i = 0; i < Mathf.Min(6, lines.Length); i++)
            {
                if (lines[i].Trim().Length > 0)
                    sb.Append("    ").AppendLine(lines[i].Trim());
            }
        }
        Write(ConsoleLog, sb.ToString(), true);
    }

    private static void Write(string path, string text, bool append)
    {
        try
        {
            lock (fileLock)
            {
                if (append && File.Exists(path) && new FileInfo(path).Length > ConsoleMaxBytes)
                    return;
                if (append)
                    File.AppendAllText(path, text);
                else
                    File.WriteAllText(path, text);
            }
        }
        catch (Exception)
        {
            // Rapport facultatif : jamais d'erreur en retour dans la console
        }
    }
}
