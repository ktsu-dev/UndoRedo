#Requires -Version 7.0
<#
.SYNOPSIS
    Updates winget manifest files with new version and SHA256 hashes from GitHub releases.

.DESCRIPTION
    This script automates the process of updating winget manifest files when a new version
    is released. It fetches the SHA256 hashes from the GitHub releases and updates the
    manifest files accordingly.

.PARAMETER Version
    The version to update the manifests for (e.g., "1.0.3")

.PARAMETER GitHubRepo
    The GitHub repository in the format "owner/repo" (default: "ktsu-dev/BlastMerge")

.EXAMPLE
    .\update-winget-manifests.ps1 -Version "1.0.3"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$GitHubRepo = "ktsu-dev/BlastMerge"
)

$ErrorActionPreference = "Stop"

# Configuration
$manifestDir = Join-Path $PSScriptRoot ".." "winget"
$packageId = "ktsu.BlastMerge"

# GitHub API configuration
$releaseUrl = "https://api.github.com/repos/$GitHubRepo/releases/tags/v$Version"
$downloadBaseUrl = "https://github.com/$GitHubRepo/releases/download/v$Version"

Write-Host "Updating winget manifests for version $Version..." -ForegroundColor Green

# Fetch release information from GitHub
try {
    Write-Host "Fetching release information from GitHub..." -ForegroundColor Yellow
    $release = Invoke-RestMethod -Uri $releaseUrl -Headers @{
        "User-Agent" = "BlastMerge-Winget-Updater"
        "Accept" = "application/vnd.github.v3+json"
    }

    Write-Host "Found release: $($release.name)" -ForegroundColor Green
    $releaseDate = [DateTime]::Parse($release.published_at).ToString("yyyy-MM-dd")
} catch {
    Write-Error "Failed to fetch release information: $_"
    exit 1
}

# Download and calculate SHA256 hashes for each architecture
$architectures = @("win-x64", "win-x86", "win-arm64")
$sha256Hashes = @{}

# First, try to read hashes from local file if available (from recent build)
$localHashesFile = Join-Path $PSScriptRoot ".." "staging" "hashes.txt"
$localHashes = @{}

if (Test-Path $localHashesFile) {
    Write-Host "Reading hashes from local build output..." -ForegroundColor Yellow
    Get-Content $localHashesFile | ForEach-Object {
        if ($_ -match '^(.+)=(.+)$') {
            $localHashes[$Matches[1]] = $Matches[2]
        }
    }
}

foreach ($arch in $architectures) {
    $fileName = "BlastMerge.ConsoleApp-$Version-$arch.zip"

    # Try to use local hash first
    if ($localHashes.ContainsKey($fileName)) {
        $sha256Hashes[$arch] = $localHashes[$fileName].ToUpper()
        Write-Host "  $arch`: $($sha256Hashes[$arch]) (from local build)" -ForegroundColor Cyan
        continue
    }

    # Fall back to downloading and calculating hash
    $downloadUrl = "$downloadBaseUrl/$fileName"
    $tempFile = Join-Path $env:TEMP $fileName

    try {
        Write-Host "Downloading $fileName to calculate SHA256..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $downloadUrl -OutFile $tempFile -UseBasicParsing

        $hash = Get-FileHash -Path $tempFile -Algorithm SHA256
        $sha256Hashes[$arch] = $hash.Hash.ToUpper()

        Write-Host "  $arch`: $($hash.Hash)" -ForegroundColor Cyan

        # Clean up temp file
        Remove-Item $tempFile -Force
    } catch {
        Write-Error "Failed to download or hash $fileName`: $_"
        exit 1
    }
}

# Update version manifest
$versionManifestPath = Join-Path $manifestDir "$packageId.yaml"
Write-Host "Updating version manifest: $versionManifestPath" -ForegroundColor Yellow

$versionContent = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.10.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.10.0
"@

Set-Content -Path $versionManifestPath -Value $versionContent -Encoding UTF8

# Update locale manifest
$localeManifestPath = Join-Path $manifestDir "$packageId.locale.en-US.yaml"
Write-Host "Updating locale manifest: $localeManifestPath" -ForegroundColor Yellow

$localeContent = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.10.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $Version
PackageLocale: en-US
Publisher: ktsu.dev
PublisherUrl: https://github.com/ktsu-dev
PublisherSupportUrl: https://github.com/$GitHubRepo/issues
# PrivacyUrl:
Author: ktsu.dev
PackageName: BlastMerge
PackageUrl: https://github.com/$GitHubRepo
License: MIT
LicenseUrl: https://github.com/$GitHubRepo/blob/main/LICENSE.md
Copyright: Copyright (c) ktsu.dev
# CopyrightUrl:
ShortDescription: Cross-repository file synchronization through intelligent iterative merging
Description: |-
  BlastMerge is a revolutionary file synchronization tool that uses intelligent iterative merging to unify multiple versions of files across repositories, directories, and codebases.
  Unlike traditional diff tools, BlastMerge progressively merges file versions by finding the most similar pairs and resolving conflicts interactively, ultimately synchronizing entire file ecosystems into a single, unified version.

  Key Features:
  - Smart Discovery: Automatically finds all versions of a file across directories/repositories
  - Hash-Based Grouping: Groups identical files and identifies unique versions
  - Similarity Analysis: Calculates similarity scores between all version pairs
  - Optimal Merge Order: Progressively merges the most similar versions first to minimize conflicts
  - Interactive Resolution: Visual TUI for resolving conflicts block-by-block
  - Cross-Repository Sync: Updates all file locations with the final merged result
Moniker: blastmerge
Tags:
- diff
- merge
- sync
- git
- files
- repository
- cli
- tui
- synchronization
- version-control
ReleaseNotes: |-
  Cross-repository file synchronization through intelligent iterative merging.

  See full changelog at: https://github.com/$GitHubRepo/blob/main/CHANGELOG.md
ReleaseNotesUrl: https://github.com/$GitHubRepo/releases/tag/v$Version
# PurchaseUrl:
# InstallationNotes:
Documentations:
- DocumentLabel: README
  DocumentUrl: https://github.com/$GitHubRepo/blob/main/README.md
ManifestType: defaultLocale
ManifestVersion: 1.10.0
"@

Set-Content -Path $localeManifestPath -Value $localeContent -Encoding UTF8

# Update installer manifest
$installerManifestPath = Join-Path $manifestDir "$packageId.installer.yaml"
Write-Host "Updating installer manifest: $installerManifestPath" -ForegroundColor Yellow

$installerContent = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.10.0.schema.json
PackageIdentifier: $packageId
PackageVersion: $Version
Platform:
- Windows.Desktop
MinimumOSVersion: 10.0.17763.0
InstallerType: zip
InstallModes:
- interactive
- silent
UpgradeBehavior: install
Commands:
- blastmerge
- BlastMerge.ConsoleApp
FileExtensions:
- txt
- md
- json
- yml
- yaml
- xml
- config
- cs
- js
- py
- java
- cpp
- c
- h
- hpp
ReleaseDate: $releaseDate
Dependencies:
  PackageDependencies:
    - PackageIdentifier: Microsoft.DotNet.DesktopRuntime.9
Installers:
- Architecture: x64
  InstallerUrl: $downloadBaseUrl/BlastMerge.ConsoleApp-$Version-win-x64.zip
  InstallerSha256: $($sha256Hashes['win-x64'])
  NestedInstallerType: portable
  NestedInstallerFiles:
  - RelativeFilePath: ktsu.BlastMerge.ConsoleApp.exe
    PortableCommandAlias: blastmerge
- Architecture: x86
  InstallerUrl: $downloadBaseUrl/BlastMerge.ConsoleApp-$Version-win-x86.zip
  InstallerSha256: $($sha256Hashes['win-x86'])
  NestedInstallerType: portable
  NestedInstallerFiles:
  - RelativeFilePath: ktsu.BlastMerge.ConsoleApp.exe
    PortableCommandAlias: blastmerge
- Architecture: arm64
  InstallerUrl: $downloadBaseUrl/BlastMerge.ConsoleApp-$Version-win-arm64.zip
  InstallerSha256: $($sha256Hashes['win-arm64'])
  NestedInstallerType: portable
  NestedInstallerFiles:
  - RelativeFilePath: ktsu.BlastMerge.ConsoleApp.exe
    PortableCommandAlias: blastmerge
ManifestType: installer
ManifestVersion: 1.10.0
"@

Set-Content -Path $installerManifestPath -Value $installerContent -Encoding UTF8

gh release upload v$Version $versionManifestPath $localeManifestPath $installerManifestPath --repo ktsu-dev/BlastMerge

Write-Host "`n✅ Winget manifests updated successfully!" -ForegroundColor Green
Write-Host "Files updated:" -ForegroundColor Yellow
Write-Host "  - $versionManifestPath" -ForegroundColor Cyan
Write-Host "  - $localeManifestPath" -ForegroundColor Cyan
Write-Host "  - $installerManifestPath" -ForegroundColor Cyan

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Review the updated manifest files" -ForegroundColor White
Write-Host "2. Test the manifests locally with: winget install --manifest $manifestDir" -ForegroundColor White
Write-Host "3. Submit to winget-pkgs repository: https://github.com/microsoft/winget-pkgs" -ForegroundColor White
Write-Host "4. Create a PR following the winget contribution guidelines" -ForegroundColor White
