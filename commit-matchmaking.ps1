# Prepare les commits de la session « matchmaking space » (a lancer a la racine du projet, Unity peut rester ouvert).
# Usage : powershell -ExecutionPolicy Bypass -File .\commit-matchmaking.ps1
# Le script ne pousse rien : verifie avec `git log --stat -5` puis `git push`.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
if (-not (Test-Path ".git")) { throw "A lancer a la racine du depot (dossier .git introuvable)." }


$P = "Assets/_Project"

function Stage([string[]]$paths) {
    foreach ($p in $paths) {
        foreach ($f in @($p, "$p.meta")) {
            if (Test-Path $f) { git add -- $f }
        }
    }
}
function Commit([string]$title, [string]$body) {
    if ([string]::IsNullOrWhiteSpace((git diff --cached --name-only))) {
        Write-Host "  (rien a commiter pour : $title)" -ForegroundColor DarkGray
        return
    }
    git commit -q -m ($title + "`n`n" + $body)
    Write-Host "  OK  $title" -ForegroundColor Green
}

Write-Host "`n1/5 Scene d'attente Matchmaking_Space" -ForegroundColor Cyan
Stage @(
    "$P/Art/Models/SuperDestroyer.glb",
    "$P/Scenes/Matchmaking_Space.unity",
    "$P/Scripts/Runtime/UI/Loader/SceneFade.cs",
    "$P/Scripts/Runtime/UI/Loader/MatchmakingSpaceScene.cs",
    "$P/Scripts/Editor/MatchmakingSpaceSetup.cs",
    "$P/Scripts/Editor/BlockforgeScenes.cs",
    "$P/Scripts/Runtime/UI/MainMenu/MainMenuController.cs",
    "$P/Scripts/Runtime/UI/MainMenu/PlayMenuController.cs",
    "ProjectSettings/EditorBuildSettings.asset"
)
Commit "feat(matchmaking): scene d'attente Matchmaking_Space avec le Super Destroyer" @"
- Import du Super Destroyer (SuperDestroyer.glb, glTFast) et scene Matchmaking_Space
  generee par Blockforge > Setup Matchmaking Space Scene (camera, soleil, dome
  d'etoiles, exposition fixe), ajoutee au build.
- SceneFade : petit fondu au noir UI Toolkit entre scenes (sous le Forge Loader).
- « Find a match » lance la recherche puis fondu vers la scene d'attente ; la
  recherche annulee revient au menu, qui rouvre l'ecran Play (ReopenOnReturn).
- Le match trouve passe par la meme transition Versus que « Play with bots »
  (PlayMenuController.StartMatch statique, mode et robot captures au clic).
"@

Write-Host "`n2/5 Ecran Play : Find a match en action principale" -ForegroundColor Cyan
Stage @("$P/Scripts/Editor/GameModesUISetup.cs")
Commit "feat(ui): « Find a match » active et action principale de l'ecran Play" @"
- Find a match : ButtonPrimary control-lg, place en premier, touche Entree
  (KeyHint) ; Play with bots passe en ButtonSecondary control-md.
- Plus de cadenas « bientot disponible » ; les deux actions restent
  indisponibles sans robot (raison au survol).
- A appliquer dans l'editeur : Blockforge > Apply Design System.
"@

Write-Host "`n3/5 Service de matchmaking : blocage a 1 joueur et porte de transition" -ForegroundColor Cyan
Stage @(
    "$P/Scripts/Runtime/UI/Loader/MatchmakingService.cs",
    "$P/Scripts/Editor/LoaderDebugMenu.cs"
)
Commit "feat(matchmaking): blocage a 1 joueur (debug) et porte de transition" @"
- HoldAtOnePlayer (PlayerPrefs, actif par defaut) + HoldMatchmakingSource :
  la recherche reste a 1/N tant que le service en ligne n'existe pas ;
  bascule dans Blockforge > Debug > Loader > Hold matchmaking at 1 player.
- TransitionGate : le match trouve n'enchaine sur le Versus que lorsque la
  cinematique a fini son depart (completionPending).
"@

Write-Host "`n4/5 Cinematique d'attente : arrivee, largage, flamme, propulseurs" -ForegroundColor Cyan
Stage @(
    "$P/Scripts/Runtime/UI/Loader/MatchmakingCinematic.cs",
    "$P/Scripts/Editor/MatchmakingCinematicSetup.cs",
    "$P/Scripts/Runtime/Space/ReentryFlameFX.cs",
    "$P/Art/Space/ReentryCap.mat",
    "$P/Art/Space/ReentryRibbon.mat",
    "$P/Art/Space/ReentryStreak.mat",
    "$P/Art/Space/ReentryEmber.mat",
    "$P/Art/Space/SpeedStreak.mat",
    "$P/Art/Space/MatchmakingSpaceProfile.asset",
    "$P/Art/Textures/ReentryCap.png",
    "$P/Art/Textures/ReentryRibbon.png",
    "$P/Art/Textures/SoftDot.png"
)
Commit "feat(matchmaking): cinematique d'attente — arrivee, largage du ship garage, flamme de rentree" @"
- MatchmakingCinematic : le Super Destroyer arrive de face depuis le lointain
  (camera devant-dessous cote baie, 3/4 avant), la camera glisse en arc vers
  la baie du joueur (SelectedBay), portes, sortie du ship garage (portes du
  garage fermees, orientation vers l'avant), camera en orbite autour de la
  baie, puis chase avec flamme de rentree ; a 10/10 joueurs, acceleration
  hors cadre puis transition Versus.
- ReentryFlameFX : calotte de plasma au ras de l'avant du ship garage +
  rubans de flammes contigus (textures procedurales ReentryCap/ReentryRibbon),
  braises et filaments ; intensite et boost pilotes par la cinematique.
- Propulseurs animes : 5 tuyeres du Super Destroyer (plumes statiques du GLB
  masquees) et propulsion renforcee du ship garage ; Bloom dans le profil
  d'exposition de la scene.
- MatchmakingCinematicSetup (one-shot, marqueur Library v10) : reconstruit
  GarageShip, BowFlame, EngineFX et les materiaux additifs HDRP/Unlit.
"@

Write-Host "`n5/5 Nettoyage : outil de preview et assets de flamme obsoletes" -ForegroundColor Cyan
$obsolete = @(
    "$P/Scripts/Editor/MatchmakingSpacePreview.cs",
    "$P/Art/Space/ReentrySheath.mat",
    "$P/Art/Space/ReentryCore.mat",
    "$P/Art/Space/ReentryWisp.mat",
    "$P/Art/Space/ReentryGlow.mat",
    "$P/Art/Space/ReentryFlame.mat",
    "$P/Art/Textures/ReentryFlame.png",
    "$P/Art/Textures/ReentryCore.png",
    "$P/Art/Textures/ReentryWisp.png",
    "super-destroyer-carrier.glb"
)
foreach ($p in $obsolete) {
    foreach ($f in @($p, "$p.meta")) {
        if (Test-Path $f) {
            Remove-Item -Force $f
            git add -A -- $f 2>$null   # stage la suppression si le fichier etait suivi
        }
    }
}
if (Test-Path "Logs/render-matchmaking-preview.request") { Remove-Item -Force "Logs/render-matchmaking-preview.request" }
Commit "chore(matchmaking): suppression de l'outil de preview et des anciens assets de flamme" @"
- MatchmakingSpacePreview (rendu hors ecran, provoquait un crash GPU D3D12)
  et sa requete dans Logs.
- Anciennes textures/materiaux de la flamme (Sheath/Core/Wisp/Glow/Flame),
  remplaces par ReentryCap/ReentryRibbon ; copie du GLB a la racine.
"@

Write-Host "`nReste non commite (a verifier) :" -ForegroundColor Yellow
git status --short
Write-Host "`nVerifie avec : git log --stat -5   puis   git push" -ForegroundColor Cyan
