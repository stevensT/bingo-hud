# Inspects this machine after a Bingo install, upgrade, or uninstall, and reports each thing the
# installer is supposed to have done as one PASS or FAIL line. Exits 1 if anything failed.
#
#   installer\check-install.ps1 -Expect Installed -Version 0.3.0
#   installer\check-install.ps1 -Expect Installed -Version 0.3.0 -StartsAtSignIn
#   installer\check-install.ps1 -Expect Uninstalled
#
# This is the installer's test. It was written before the installer and run against a machine with
# nothing installed, where every Installed check has to fail; a check that cannot fail proves
# nothing. It reads the machine and changes nothing.
#
# The Uninstalled checks look wider than the Installed ones. An entry or shortcut that is still
# there under a slightly wrong name is still an install that was not removed.
#
# Uninstalled also expects the settings folder to exist, because uninstalling must leave it. On a
# machine where Bingo has never run, that check fails for the honest reason that there was never a
# folder to keep.
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Installed', 'Uninstalled')]
    [string]$Expect,

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$StartsAtSignIn
)

$ErrorActionPreference = 'Stop'

if ($Expect -eq 'Installed' -and -not $Version) {
    Write-Error '-Expect Installed needs -Version, the version Settings > Apps should show.'
}
if ($Expect -eq 'Uninstalled' -and ($Version -or $StartsAtSignIn)) {
    Write-Error '-Version and -StartsAtSignIn only apply to -Expect Installed.'
}

# These names have to match installer\bingo.iss and the winget manifest exactly. winget finds the
# installed app by its Settings > Apps name, publisher, and version.
$appName = 'Bingo'
$publisher = 'stevensT'
$exeName = 'BingoHud.App.exe'
$shortcutName = 'Bingo.lnk'

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\Bingo'
$exePath = Join-Path $installDir $exeName
$settingsDir = Join-Path $env:LOCALAPPDATA 'Bingo'
$programsDir = [Environment]::GetFolderPath('Programs')
$startupDir = [Environment]::GetFolderPath('Startup')
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'

$script:failures = 0

function Report([bool]$passed, [string]$what) {
    if ($passed) {
        Write-Host "PASS  $what"
    } else {
        Write-Host "FAIL  $what" -ForegroundColor Red
        $script:failures++
    }
}

function Get-ShortcutTarget([string]$path) {
    $shell = New-Object -ComObject WScript.Shell
    return $shell.CreateShortcut($path).TargetPath
}

function Test-SamePath([string]$a, [string]$b) {
    if (-not $a -or -not $b) { return $false }
    return $a.TrimEnd('\') -eq $b.TrimEnd('\')
}

# Each Get- function below is called as @(Get-...). PowerShell sends a one-item result back as a
# single object, and in Windows PowerShell 5.1 a single registry entry has no .Count, so without
# the @() a count of one reads as blank and the checks that depend on it never run.

# Start menu shortcuts named Bingo, wherever under Programs the installer put them. The Startup
# folder sits inside Programs, so it is left out here and checked on its own.
function Get-StartMenuShortcuts {
    if (-not (Test-Path $programsDir)) { return }
    return Get-ChildItem -Path $programsDir -Filter $shortcutName -Recurse -File |
        Where-Object { -not $_.FullName.StartsWith($startupDir + '\', [StringComparison]::OrdinalIgnoreCase) }
}

# Startup shortcuts that start Bingo: by name, or by pointing at the install folder under any name.
function Get-StartupShortcuts {
    if (-not (Test-Path $startupDir)) { return }
    return Get-ChildItem -Path $startupDir -Filter '*.lnk' -File | Where-Object {
        $_.Name -eq $shortcutName -or
        (Get-ShortcutTarget $_.FullName).StartsWith($installDir + '\', [StringComparison]::OrdinalIgnoreCase)
    }
}

# Settings > Apps entries that could be Bingo's. Inno Setup's default name is "Bingo version X.Y.Z",
# so a wrongly named entry is caught here rather than missed.
function Get-UninstallEntries {
    return Get-ChildItem -Path $uninstallKey | Get-ItemProperty | Where-Object {
        $_.DisplayName -eq $appName -or
        $_.DisplayName -like "$appName *" -or
        (Test-SamePath $_.InstallLocation $installDir)
    }
}

function Get-InstalledBingoProcesses {
    return Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($exeName)) -ErrorAction SilentlyContinue |
        Where-Object { Test-SamePath $_.Path $exePath }
}

Write-Host "Expecting Bingo $Expect$(if ($Version) { " at $Version" })$(if ($StartsAtSignIn) { ', starting at sign-in' })"
Write-Host ''

if ($Expect -eq 'Installed') {
    Report (Test-Path $exePath -PathType Leaf) "The executable is at $exePath"

    $menu = @(Get-StartMenuShortcuts)
    Report ($menu.Count -eq 1) "Exactly one Start menu shortcut named Bingo (found $($menu.Count))"
    if ($menu.Count -ge 1) {
        $target = Get-ShortcutTarget $menu[0].FullName
        Report (Test-SamePath $target $exePath) "The Start menu shortcut points at the executable (it points at '$target')"
    }

    $entries = @(Get-UninstallEntries)
    Report ($entries.Count -eq 1) "Exactly one Settings > Apps entry for Bingo (found $($entries.Count))"
    if ($entries.Count -ge 1) {
        $entry = $entries[0]
        Report ($entry.DisplayName -eq $appName) "The entry is named '$appName' (it is '$($entry.DisplayName)')"
        Report ($entry.Publisher -eq $publisher) "The entry's publisher is '$publisher' (it is '$($entry.Publisher)')"
        Report ($entry.DisplayVersion -eq $Version) "The entry's version is $Version (it is '$($entry.DisplayVersion)')"
    }

    $startup = @(Get-StartupShortcuts)
    if ($StartsAtSignIn) {
        Report ($startup.Count -eq 1) "Exactly one sign-in shortcut in the Startup folder (found $($startup.Count))"
        if ($startup.Count -ge 1) {
            $target = Get-ShortcutTarget $startup[0].FullName
            Report (Test-SamePath $target $exePath) "The sign-in shortcut points at the executable (it points at '$target')"
        }
    } else {
        Report ($startup.Count -eq 0) "No sign-in shortcut in the Startup folder (found $($startup.Count))"
    }
} else {
    Report (-not (Test-Path $exePath)) "No executable at $exePath"

    $menu = @(Get-StartMenuShortcuts)
    Report ($menu.Count -eq 0) "No Start menu shortcut named Bingo (found $($menu.Count))"

    $entries = @(Get-UninstallEntries)
    $names = ($entries | ForEach-Object { "'$($_.DisplayName)'" }) -join ', '
    Report ($entries.Count -eq 0) "No Settings > Apps entry for Bingo (found $($entries.Count)$(if ($names) { ": $names" }))"

    $startup = @(Get-StartupShortcuts)
    Report ($startup.Count -eq 0) "No sign-in shortcut in the Startup folder (found $($startup.Count))"

    Report (Test-Path $settingsDir -PathType Container) "The settings folder is kept at $settingsDir"

    $running = @(Get-InstalledBingoProcesses)
    Report ($running.Count -eq 0) "No Bingo running from the install folder (found $($running.Count))"
}

Write-Host ''
if ($script:failures -gt 0) {
    Write-Host "$($script:failures) check(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host 'All checks passed.'
exit 0
