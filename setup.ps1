[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $ValheimPath,

    [Parameter(Position = 1)]
    [string] $BepInExPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$librariesPath = Join-Path $projectRoot 'libs'

function Add-UniquePath {
    param(
        [System.Collections.Generic.List[string]] $Paths,
        [string] $Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $fullPath = [System.IO.Path]::GetFullPath(
        [Environment]::ExpandEnvironmentVariables($Path)
    )

    if (-not $Paths.Contains($fullPath)) {
        [void] $Paths.Add($fullPath)
    }
}

function Test-ValheimInstall {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $assemblyPath = Join-Path $Path 'valheim_Data\Managed\assembly_valheim.dll'
    return Test-Path -LiteralPath $assemblyPath -PathType Leaf
}

function Find-ValheimInstall {
    param([string] $ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $resolvedPath = [System.IO.Path]::GetFullPath(
            [Environment]::ExpandEnvironmentVariables($ExplicitPath)
        )

        if (Test-ValheimInstall $resolvedPath) {
            return $resolvedPath
        }

        throw "No Valheim installation found at '$resolvedPath'. Pass the directory containing valheim.exe."
    }

    $candidates = [System.Collections.Generic.List[string]]::new()

    $uninstallKeys = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 892970',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 892970'
    )

    foreach ($uninstallKey in $uninstallKeys) {
        try {
            $installLocation = (Get-ItemProperty -LiteralPath $uninstallKey).InstallLocation
            Add-UniquePath $candidates $installLocation
        }
        catch {
        }
    }

    $steamRoots = [System.Collections.Generic.List[string]]::new()

    try {
        $steamPath = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam').SteamPath
        Add-UniquePath $steamRoots $steamPath
    }
    catch {
    }

    $programFilesX86 = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::ProgramFilesX86
    )
    Add-UniquePath $steamRoots (Join-Path $programFilesX86 'Steam')

    foreach ($steamRoot in @($steamRoots)) {
        Add-UniquePath $candidates (Join-Path $steamRoot 'steamapps\common\Valheim')

        $libraryFile = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $libraryFile -PathType Leaf)) {
            continue
        }

        foreach ($line in Get-Content -LiteralPath $libraryFile) {
            if ($line -match '^\s*"path"\s+"(?<LibraryPath>[^"]+)"') {
                $libraryRoot = $Matches.LibraryPath -replace '\\\\', '\'
                Add-UniquePath $candidates (Join-Path $libraryRoot 'steamapps\common\Valheim')
            }
        }
    }

    foreach ($candidate in $candidates) {
        if (Test-ValheimInstall $candidate) {
            return $candidate
        }
    }

    throw 'Valheim was not found automatically. Run setup.ps1 with -ValheimPath.'
}

function Test-BepInExCore {
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    return Test-Path -LiteralPath (Join-Path $Path 'BepInEx.dll') -PathType Leaf
}

function Find-BepInExCore {
    param(
        [string] $ExplicitPath,
        [string] $ValheimInstall
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $resolvedPath = [System.IO.Path]::GetFullPath(
            [Environment]::ExpandEnvironmentVariables($ExplicitPath)
        )

        $explicitCandidates = @(
            $resolvedPath,
            (Join-Path $resolvedPath 'core'),
            (Join-Path $resolvedPath 'BepInEx\core')
        )

        foreach ($candidate in $explicitCandidates) {
            if (Test-BepInExCore $candidate) {
                return $candidate
            }
        }

        throw "No BepInEx.dll found below '$resolvedPath'. Pass the BepInEx core, BepInEx, profile, or Valheim directory."
    }

    $gameCorePath = Join-Path $ValheimInstall 'BepInEx\core'
    if (Test-BepInExCore $gameCorePath) {
        return $gameCorePath
    }

    $applicationData = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::ApplicationData
    )
    $profileRoots = @(
        (Join-Path $applicationData 'r2modmanPlus-local\Valheim\profiles'),
        (Join-Path $applicationData 'Thunderstore Mod Manager\DataFolder\Valheim\profiles')
    )

    foreach ($profileRoot in $profileRoots) {
        if (-not (Test-Path -LiteralPath $profileRoot -PathType Container)) {
            continue
        }

        $profiles = Get-ChildItem -LiteralPath $profileRoot -Directory |
            Sort-Object LastWriteTime -Descending

        foreach ($profile in $profiles) {
            $profileCorePath = Join-Path $profile.FullName 'BepInEx\core'
            if (Test-BepInExCore $profileCorePath) {
                return $profileCorePath
            }
        }
    }

    throw 'BepInEx was not found automatically. Run setup.ps1 with -BepInExPath.'
}

$valheimInstall = Find-ValheimInstall $ValheimPath
$bepInExCore = Find-BepInExCore $BepInExPath $valheimInstall
$managedPath = Join-Path $valheimInstall 'valheim_Data\Managed'

$assemblySources = [ordered] @{
    'BepInEx.dll' = Join-Path $bepInExCore 'BepInEx.dll'
    'assembly_valheim.dll' = Join-Path $managedPath 'assembly_valheim.dll'
    'assembly_utils.dll' = Join-Path $managedPath 'assembly_utils.dll'
    'UnityEngine.dll' = Join-Path $managedPath 'UnityEngine.dll'
    'UnityEngine.CoreModule.dll' = Join-Path $managedPath 'UnityEngine.CoreModule.dll'
    'UnityEngine.UI.dll' = Join-Path $managedPath 'UnityEngine.UI.dll'
    'Unity.TextMeshPro.dll' = Join-Path $managedPath 'Unity.TextMeshPro.dll'
}

$missingAssemblies = @(
    foreach ($assembly in $assemblySources.GetEnumerator()) {
        if (-not (Test-Path -LiteralPath $assembly.Value -PathType Leaf)) {
            $assembly.Value
        }
    }
)

if ($missingAssemblies.Count -gt 0) {
    throw "Required assemblies are missing:`n$($missingAssemblies -join "`n")"
}

New-Item -ItemType Directory -Path $librariesPath -Force | Out-Null
Get-ChildItem -LiteralPath $librariesPath -Filter '*.dll' -File |
    Remove-Item -Force

foreach ($assembly in $assemblySources.GetEnumerator()) {
    Copy-Item -LiteralPath $assembly.Value -Destination (Join-Path $librariesPath $assembly.Key) -Force
}

Write-Host "Copied $($assemblySources.Count) build dependencies to '$librariesPath'."
Write-Host 'Run: dotnet build -c Release'
