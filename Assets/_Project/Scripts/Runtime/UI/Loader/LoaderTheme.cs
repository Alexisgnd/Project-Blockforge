using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

// =========================================================
// THEME DU FORGE LOADER (ScriptableObject)
// Resources/Loader/LoaderTheme.asset, cree par
// Blockforge > Loader > Setup Forge Loader. Deux moities :
// - ce fichier : textes (etapes, astuces, build), courbe
//   ease-out, profil du chargement simule, cinematique de
//   matchmaking, PanelSettings du loader ;
// - Resources/Loader/LoaderTheme.uss : TOUTES les couleurs et
//   mesures en px (variables USS). Aucun ecran du loader n'ecrit
//   une couleur ou un pixel en dur.
// Sans asset (setup pas encore lance), Load() renvoie les memes
// valeurs par defaut.
// =========================================================

[Serializable]
public class LoadPlan
{
    [Tooltip("Nom de la scene de destination (MainMenu, Garage, Map_Test…)")]
    public string sceneName;
    [Tooltip("Etapes affichees pendant le chargement ; la derniere est l'etat final (« Ready »)")]
    public string[] steps;

    public LoadPlan() { }

    public LoadPlan(string sceneName, params string[] steps)
    {
        this.sceneName = sceneName;
        this.steps = steps;
    }
}

[Serializable]
public class MockLoadProfile
{
    [Tooltip("Rapide jusqu'a fastTo")]
    [Range(0f, 1f)] public float fastTo = 0.45f;
    public float fastSeconds = 0.6f;
    [Tooltip("Lent jusqu'a slowTo")]
    [Range(0f, 1f)] public float slowTo = 0.6f;
    public float slowSeconds = 1.6f;
    [Tooltip("Arret net")]
    public float stallSeconds = 0.5f;
    [Tooltip("Puis vite jusqu'a 100 %")]
    public float quickSeconds = 0.45f;

    public float TotalSeconds => fastSeconds + slowSeconds + stallSeconds + quickSeconds;
}

[CreateAssetMenu(menuName = "Blockforge/Loader Theme", fileName = "LoaderTheme")]
public class LoaderTheme : ScriptableObject
{
    public const string ResourcePath = "Loader/LoaderTheme";
    public const string SplashTemplate = "Loader/ForgeSplash";
    public const string ScreenLoadTemplate = "Loader/ForgeScreenLoad";
    public const string MatchmakingTemplate = "Loader/ForgeMatchmaking";

    [Header("Rendu")]
    [Tooltip("PanelSettings du loader : 1920 x 1080, mise a l'echelle sur la hauteur, au-dessus des menus")]
    public PanelSettings panelSettings;
    [Tooltip("Replique AnimationCurve de cubic-bezier(.2, .8, .2, 1)")]
    public AnimationCurve ease = ForgeEase.CreateCurve();

    [Header("Splash (lancement)")]
    public string[] splashSteps = { "Loading parts", "Building the arena", "Waking the robots", "Syncing the match", "Ready" };
    [Tooltip("{0} = Application.version, {1} = dev / release")]
    public string buildFormat = "Build {0} · {1}";
    [TextArea(2, 4)]
    public string[] tips =
    {
        "A robot loses the blocks it is hit on. Put your weapons where the armour is.",
        "Keys 1, 2 and 3 switch between your weapon and module types.",
        "Press R to self-right a robot that landed on its back.",
        "Heavier builds take more hits, lighter builds get there first.",
    };

    [Header("Chargement d'ecran")]
    [Tooltip("Etapes par scene de destination")]
    public LoadPlan[] screenPlans =
    {
        new("Garage", "Entering the garage", "Loading your bays", "Placing the robot", "Ready"),
        new("MainMenu", "Entering the hangar", "Loading your bays", "Placing the robot", "Ready"),
        new("Map_Test", "Entering the test range", "Building the arena", "Placing the robot", "Ready"),
        new("Map_MARS", "Entering the arena", "Building the arena", "Placing the robot", "Ready"),
        new("Boot", "Back to sign-in", "Ready"),
    };
    [Tooltip("Scene sans plan dedie")]
    public string[] defaultScreenSteps = { "Loading", "Ready" };

    [Header("Matchmaking")]
    [Tooltip("Cinematique plein ecran pendant la recherche (optionnelle : sans clip, le bandeau se pose sur l'ecran courant)")]
    public VideoClip matchmakingCinematic;
    public string defaultRegion = "EU West";
    [Range(2, 16)] public int seats = 10;
    [Tooltip("Attente estimee affichee par defaut (s)")]
    public float estimatedSeconds = 90f;

    [Header("Apercu / mock")]
    public MockLoadProfile mockLoad = new();

    private static LoaderTheme cached;

    public static LoaderTheme Load()
    {
        if (cached != null)
            return cached;
        cached = Resources.Load<LoaderTheme>(ResourcePath);
        if (cached == null)
        {
            Debug.LogWarning("[ForgeLoader] Resources/Loader/LoaderTheme.asset absent : valeurs par defaut. " +
                             "Lance Blockforge > Loader > Setup Forge Loader.");
            cached = CreateInstance<LoaderTheme>();
            cached.hideFlags = HideFlags.DontSave;
        }
        return cached;
    }

    public string[] StepsFor(string sceneName)
    {
        foreach (var plan in screenPlans)
        {
            if (plan != null && plan.steps != null && plan.steps.Length > 0 &&
                string.Equals(plan.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                return plan.steps;
        }
        return defaultScreenSteps;
    }

    public string RandomTip() => tips == null || tips.Length == 0 ? "" : tips[UnityEngine.Random.Range(0, tips.Length)];

    public string BuildString() => string.Format(buildFormat, Application.version, Debug.isDebugBuild ? "dev" : "release");

    public float Ease(float t) => ease != null && ease.length >= 2 ? ease.Evaluate(Mathf.Clamp01(t)) : ForgeEase.Evaluate(t);
}
