# =========================================================
# PILOTE L'EDITEUR UNITY OUVERT VIA BlockforgeBatch
# Depose Library/BlockforgeRun.request (une commande par ligne :
# chemin de menu, open:<scene>, play, stop), attend le compte
# rendu Library/BlockforgeRun.result et affiche les lignes du
# log Logs/Editor.log ecrites pendant l'execution.
# L'editeur doit tourner, hors compilation ; en Play mode seule
# une requete "stop" est acceptee.
# Exemples (depuis la racine du projet) :
#   .\Tools\Unity\run_batch.ps1 -Commands "Blockforge/Setup Map Test Scene"
#   .\Tools\Unity\run_batch.ps1 -Commands "open:Assets/_Project/Scenes/Map_Test.unity","play"
#   .\Tools\Unity\run_batch.ps1 -Commands "stop" -LogPattern "RobotTest|Exception"
# =========================================================
param(
    [Parameter(Mandatory = $true)][string[]]$Commands,
    [int]$TimeoutSeconds = 300,
    [string]$LogPattern = "\[Blockforge|\[MapTestSceneSetup\]|\[GarageSceneSetup\]|\[GarageInventorySetup\]|\[GarageUISetup\]|\[RobotTest|\[GarageBuild|Exception|error CS|Assertion"
)

$req = "Library\BlockforgeRun.request"
$res = "Library\BlockforgeRun.result"
$log = "Logs\Editor.log"

if (-not (Test-Path "ProjectSettings\ProjectVersion.txt")) { Write-Error "A lancer depuis la racine du projet Unity."; exit 1 }
if (Test-Path $res) { [System.IO.File]::Delete((Resolve-Path $res).Path) }
$logStart = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }

$Commands | Set-Content -Encoding ASCII $req

$t0 = Get-Date
while (-not (Test-Path $res) -and ((Get-Date) - $t0).TotalSeconds -lt $TimeoutSeconds) { Start-Sleep -Seconds 3 }
if (Test-Path $res) {
    Get-Content $res
    [System.IO.File]::Delete((Resolve-Path $res).Path)
} else {
    Write-Output "Pas de compte rendu apres $TimeoutSeconds s (editeur ferme, en compilation, ou boite de dialogue ouverte ?)"
    exit 2
}

Start-Sleep -Seconds 3
if (Test-Path $log) {
    $content = Get-Content $log -Raw
    $new = if ($content.Length -gt $logStart) { $content.Substring($logStart) } else { "" }
    Write-Output "--- log :"
    [regex]::Matches($new, "[^\r\n]*($LogPattern)[^\r\n]*") | ForEach-Object { $_.Value } |
        Where-Object { $_ -notmatch "UnityEngine\.(Debug|Logger|DebugLogHandler|StackTraceUtility)|^\s+at " } |
        Select-Object -Unique -First 80
}
