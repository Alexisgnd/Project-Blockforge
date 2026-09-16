# =========================================================
# DONNE LE FOCUS A L'EDITEUR UNITY OUVERT ET ATTEND LA COMPILATION
# Unity ne rafraichit ses assets (et ne recompile) que quand il
# reprend le focus : ce script le met au premier plan (detour
# par une autre fenetre s'il y est deja), attend que le log
# Logs/Editor.log se calme, puis liste les erreurs C# et les
# lignes utiles. A lancer depuis la racine du projet apres
# avoir modifie des scripts, avant run_batch.ps1.
# =========================================================
param([int]$MinWaitSeconds = 30, [int]$QuietSeconds = 20, [int]$MaxWaitSeconds = 480)

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class Win32Focus {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@
Add-Type -AssemblyName System.Windows.Forms

function Focus-Window($handle) {
    # SetForegroundWindow echoue sans une touche simulee juste avant
    [System.Windows.Forms.SendKeys]::SendWait("%")
    [Win32Focus]::SetForegroundWindow($handle) | Out-Null
}

$log = "Logs\Editor.log"
if (-not (Test-Path $log)) { Write-Error "Logs\Editor.log introuvable : lancer depuis la racine du projet, editeur ouvert."; exit 1 }
$startLength = (Get-Item $log).Length

$unity = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $unity) { Write-Error "Fenetre principale Unity introuvable."; exit 1 }

if ([Win32Focus]::GetForegroundWindow() -eq $unity.MainWindowHandle) {
    $other = Get-Process blender, explorer -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($other) { Focus-Window $other.MainWindowHandle; Start-Sleep -Milliseconds 900 }
}
Focus-Window $unity.MainWindowHandle
Start-Sleep -Milliseconds 700
Write-Output ("focus Unity : " + ([Win32Focus]::GetForegroundWindow() -eq $unity.MainWindowHandle))

$t0 = Get-Date
$lastLength = (Get-Item $log).Length
$lastChange = Get-Date
while ($true) {
    Start-Sleep -Seconds 5
    $len = (Get-Item $log).Length
    if ($len -ne $lastLength) { $lastLength = $len; $lastChange = Get-Date }
    $elapsed = ((Get-Date) - $t0).TotalSeconds
    $quiet = ((Get-Date) - $lastChange).TotalSeconds
    if ($elapsed -ge $MinWaitSeconds -and $quiet -ge $QuietSeconds) { break }
    if ($elapsed -ge $MaxWaitSeconds) { Write-Output "timeout"; break }
}

$content = Get-Content $log -Raw
$new = if ($content.Length -gt $startLength) { $content.Substring($startLength) } else { "" }
Write-Output ("log : " + $new.Length + " nouveaux octets en " + [int]((Get-Date) - $t0).TotalSeconds + " s")
$errors = [regex]::Matches($new, "[^\r\n]*error CS\d+[^\r\n]*") | ForEach-Object { $_.Value } | Select-Object -Unique
Write-Output ("erreurs C# : " + $errors.Count)
$errors | Select-Object -First 30
Write-Output "--- lignes utiles :"
[regex]::Matches($new, "[^\r\n]*(\[Blockforge|Finished compiling|Script compilation|Domain Reload Profiling|Exception|Importing '.*\.glb)[^\r\n]*") |
    ForEach-Object { $_.Value } | Select-Object -Unique -First 40
