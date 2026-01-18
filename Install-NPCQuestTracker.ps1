# NPCQuestTracker Installation Script
# This script installs SMAPI and the NPC Quest Tracker mod for Stardew Valley

param(
    [string]$StardewPath,
    [string]$ModZipUrl,
    [switch]$XboxGamePass
)

$ErrorActionPreference = "Stop"

# Script configuration
$MOD_NAME = "NPCQuestTracker"
$SMAPI_REPO = "Pathoschild/SMAPI"
$MOD_REPO = "smencer/NPCQuestTracker"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  NPC Quest Tracker - Installation Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Function to get latest mod release info
function Get-LatestModRelease {
    Write-Host "Fetching latest NPC Quest Tracker release..." -ForegroundColor Yellow

    try {
        $ProgressPreference = 'SilentlyContinue'
        $apiUrl = "https://api.github.com/repos/$MOD_REPO/releases/latest"
        $release = Invoke-RestMethod -Uri $apiUrl -UseBasicParsing -Headers @{ "User-Agent" = "NPCQuestTracker-Installer" }
        $ProgressPreference = 'Continue'

        # Find the mod ZIP asset
        $modAsset = $release.assets | Where-Object { $_.name -match "$MOD_NAME.*\.zip" } | Select-Object -First 1

        if (-not $modAsset) {
            throw "Could not find mod ZIP asset in latest release"
        }

        $version = $release.tag_name -replace '^v', ''

        return @{
            Version = $version
            DownloadUrl = $modAsset.browser_download_url
            ReleaseUrl = $release.html_url
        }
    } catch {
        Write-Host "    WARNING: Could not fetch latest mod release: $_" -ForegroundColor Yellow
        return $null
    }
}

# Function to get latest SMAPI release info
function Get-LatestSmapiRelease {
    Write-Host "Fetching latest SMAPI release information..." -ForegroundColor Yellow

    try {
        $ProgressPreference = 'SilentlyContinue'
        $apiUrl = "https://api.github.com/repos/$SMAPI_REPO/releases/latest"
        $release = Invoke-RestMethod -Uri $apiUrl -UseBasicParsing -Headers @{ "User-Agent" = "NPCQuestTracker-Installer" }
        $ProgressPreference = 'Continue'

        # Find the installer asset
        $installerAsset = $release.assets | Where-Object { $_.name -match "SMAPI-.*-installer\.zip" } | Select-Object -First 1

        if (-not $installerAsset) {
            throw "Could not find SMAPI installer asset in latest release"
        }

        $version = $release.tag_name -replace '^v', ''

        return @{
            Version = $version
            DownloadUrl = $installerAsset.browser_download_url
            ReleaseUrl = $release.html_url
        }
    } catch {
        Write-Host "    WARNING: Could not fetch latest SMAPI release: $_" -ForegroundColor Yellow
        Write-Host "    Falling back to SMAPI 4.4.0" -ForegroundColor Yellow

        return @{
            Version = "4.4.0"
            DownloadUrl = "https://github.com/Pathoschild/SMAPI/releases/download/4.4.0/SMAPI-4.4.0-installer.zip"
            ReleaseUrl = "https://github.com/Pathoschild/SMAPI/releases/tag/4.4.0"
        }
    }
}

# Function to find Stardew Valley installation path
function Find-StardewValleyPath {
    Write-Host "Searching for Stardew Valley installation..." -ForegroundColor Yellow

    # Common Steam installation paths
    $steamPaths = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley",
        "C:\Program Files\Steam\steamapps\common\Stardew Valley",
        "$env:ProgramFiles\Steam\steamapps\common\Stardew Valley",
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\Stardew Valley"
    )

    # Check for custom Steam library folders
    $steamConfigPath = "${env:ProgramFiles(x86)}\Steam\steamapps\libraryfolders.vdf"
    if (Test-Path $steamConfigPath) {
        $steamConfig = Get-Content $steamConfigPath -Raw
        $libraryPaths = [regex]::Matches($steamConfig, '"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
        foreach ($libPath in $libraryPaths) {
            $libPath = $libPath -replace '\\\\', '\'
            $steamPaths += Join-Path $libPath "steamapps\common\Stardew Valley"
        }
    }

    # Xbox Game Pass path
    $xboxPaths = @(
        "C:\Program Files\WindowsApps\ConcernedApe.StardewValleyPC_*",
        "$env:ProgramFiles\WindowsApps\ConcernedApe.StardewValleyPC_*"
    )

    # Check Steam paths
    foreach ($path in $steamPaths) {
        if (Test-Path (Join-Path $path "Stardew Valley.exe")) {
            Write-Host "Found Stardew Valley (Steam) at: $path" -ForegroundColor Green
            return @{ Path = $path; IsXbox = $false }
        }
    }

    # Check Xbox Game Pass paths
    foreach ($pattern in $xboxPaths) {
        $foundDirs = Get-ChildItem -Path (Split-Path $pattern -Parent) -Filter (Split-Path $pattern -Leaf) -Directory -ErrorAction SilentlyContinue
        if ($foundDirs) {
            $path = $foundDirs[0].FullName
            if (Test-Path (Join-Path $path "Stardew Valley.exe")) {
                Write-Host "Found Stardew Valley (Xbox Game Pass) at: $path" -ForegroundColor Green
                return @{ Path = $path; IsXbox = $true }
            }
        }
    }

    return $null
}

# Step 0: Find or validate Stardew Valley installation
if (-not $StardewPath) {
    $gameInfo = Find-StardewValleyPath

    if ($null -eq $gameInfo) {
        Write-Host "Could not automatically find Stardew Valley installation." -ForegroundColor Red
        Write-Host ""
        Write-Host "Please provide the installation path using the -StardewPath parameter." -ForegroundColor Yellow
        Write-Host "Example: .\Install-NPCQuestTracker.ps1 -StardewPath 'C:\Program Files\Steam\steamapps\common\Stardew Valley'" -ForegroundColor Yellow
        exit 1
    }

    $StardewPath = $gameInfo.Path
    if ($gameInfo.IsXbox) {
        $XboxGamePass = $true
        Write-Host "Detected Xbox Game Pass version." -ForegroundColor Cyan
    }
} else {
    if (-not (Test-Path $StardewPath)) {
        Write-Host "ERROR: The specified Stardew Valley path does not exist: $StardewPath" -ForegroundColor Red
        exit 1
    }

    if (-not (Test-Path (Join-Path $StardewPath "Stardew Valley.exe"))) {
        Write-Host "ERROR: Could not find 'Stardew Valley.exe' in the specified path." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Using Stardew Valley installation at: $StardewPath" -ForegroundColor Green
Write-Host ""

# Create working directory
# Note: Cannot use $env:TEMP because SMAPI installer rejects paths containing "TEMP"
$workDir = Join-Path $env:USERPROFILE ".npcquesttracker_install_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $workDir -Force | Out-Null
Write-Host "Created working directory: $workDir" -ForegroundColor Gray
Write-Host ""

try {
    # Step 1: Get latest SMAPI release and download
    Write-Host "[1/6] Getting latest SMAPI release..." -ForegroundColor Cyan
    $smapiRelease = Get-LatestSmapiRelease
    Write-Host "    Latest SMAPI version: $($smapiRelease.Version)" -ForegroundColor Green
    Write-Host ""

    Write-Host "    Downloading SMAPI $($smapiRelease.Version)..." -ForegroundColor Cyan
    $smapiZipPath = Join-Path $workDir "SMAPI-installer.zip"

    try {
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $smapiRelease.DownloadUrl -OutFile $smapiZipPath -UseBasicParsing
        $ProgressPreference = 'Continue'
        Write-Host "    Downloaded SMAPI installer successfully." -ForegroundColor Green
    } catch {
        Write-Host "    ERROR: Failed to download SMAPI: $_" -ForegroundColor Red
        exit 1
    }
    Write-Host ""

    # Step 2: Extract SMAPI
    Write-Host "[2/6] Extracting SMAPI installer..." -ForegroundColor Cyan
    $smapiExtractPath = Join-Path $workDir "SMAPI"
    try {
        # Remove destination if it exists to ensure clean extraction
        if (Test-Path $smapiExtractPath) {
            Remove-Item -Path $smapiExtractPath -Recurse -Force
        }

        Expand-Archive -Path $smapiZipPath -DestinationPath $smapiExtractPath -Force
        Write-Host "    Extracted SMAPI installer successfully." -ForegroundColor Green

        # Give the filesystem a moment to release file handles
        Start-Sleep -Milliseconds 500

        # The ZIP extracts to a subdirectory like "SMAPI X.X.X installer"
        # Find that subdirectory
        $smapiInstallerDir = Get-ChildItem -Path $smapiExtractPath -Directory | Where-Object { $_.Name -like "SMAPI*installer" } | Select-Object -First 1
        if ($smapiInstallerDir) {
            $smapiExtractPath = $smapiInstallerDir.FullName
            Write-Host "    Using installer directory: $($smapiInstallerDir.Name)" -ForegroundColor Gray
            Write-Host "    Full path: $smapiExtractPath" -ForegroundColor Gray
        }
    } catch {
        Write-Host "    ERROR: Failed to extract SMAPI: $_" -ForegroundColor Red
        exit 1
    }
    Write-Host ""

    # Step 3: Run SMAPI installer
    Write-Host "[3/6] Running SMAPI installer..." -ForegroundColor Cyan
    Write-Host "    NOTE: The installer is interactive and will require your input." -ForegroundColor Yellow
    Write-Host "    Please follow the on-screen instructions." -ForegroundColor Yellow
    Write-Host ""

    # Find the install.bat file dynamically - try multiple possible locations
    $installerPath = $null

    # Look for installer files in common locations
    $searchPatterns = @(
        "install on Windows.bat",
        "install.bat",
        "internal\windows\install.bat",
        "windows-install.bat"
    )

    foreach ($pattern in $searchPatterns) {
        $testPath = Get-ChildItem -Path $smapiExtractPath -Filter (Split-Path $pattern -Leaf) -Recurse -ErrorAction SilentlyContinue |
                    Select-Object -First 1
        if ($testPath) {
            $installerPath = $testPath.FullName
            break
        }
    }

    if (-not $installerPath) {
        Write-Host "    ERROR: Could not find SMAPI installer batch file." -ForegroundColor Red
        Write-Host "    Searching in: $smapiExtractPath" -ForegroundColor Yellow
        Write-Host "    Directory contents:" -ForegroundColor Yellow
        Get-ChildItem -Path $smapiExtractPath -Recurse -File | Select-Object -First 20 | ForEach-Object {
            Write-Host "      $($_.FullName)" -ForegroundColor Gray
        }
        exit 1
    }

    Write-Host "    Found installer at: $installerPath" -ForegroundColor Gray

    # Verify the file actually exists on disk (not in ZIP)
    if (-not (Test-Path $installerPath)) {
        Write-Host "    ERROR: Installer path exists but file is not accessible." -ForegroundColor Red
        Write-Host "    This might indicate the file is still locked or in a ZIP." -ForegroundColor Yellow
        exit 1
    }

    # Run the installer (quote the path to handle spaces in filename)
    $installerDir = Split-Path $installerPath -Parent
    $installerName = Split-Path $installerPath -Leaf

    Write-Host "    Starting installer..." -ForegroundColor Gray
    Write-Host "    Working directory: $installerDir" -ForegroundColor Gray

    # Change to the installer directory and run the batch file directly
    # This ensures the installer's checks pass (it looks for specific relative paths)
    Push-Location $installerDir

    # Run directly without cmd /c to avoid path confusion
    Start-Process -FilePath $installerPath -Wait -NoNewWindow

    Pop-Location

    Write-Host ""
    Write-Host "    SMAPI installation completed." -ForegroundColor Green
    Write-Host ""

    # Step 4: Download mod
    Write-Host "[4/6] Downloading NPC Quest Tracker mod..." -ForegroundColor Cyan

    # If no URL provided, try to fetch the latest release
    if (-not $ModZipUrl) {
        $modRelease = Get-LatestModRelease

        if ($modRelease) {
            $ModZipUrl = $modRelease.DownloadUrl
            Write-Host "    Latest mod version: $($modRelease.Version)" -ForegroundColor Green
            Write-Host "    Release URL: $($modRelease.ReleaseUrl)" -ForegroundColor Gray
        } else {
            Write-Host "    Could not fetch latest release automatically." -ForegroundColor Yellow
            Write-Host "    You can provide the release ZIP URL using -ModZipUrl parameter." -ForegroundColor Yellow
            Write-Host "    Example: -ModZipUrl 'https://github.com/smencer/NPCQuestTracker/releases/download/v2.0.0/NPCQuestTracker.2.0.0.zip'" -ForegroundColor Yellow
            $ModZipUrl = Read-Host "    Enter the mod release ZIP URL (or press Enter to skip)"

            if (-not $ModZipUrl) {
                Write-Host "    Skipping mod installation. You can manually copy the mod files later." -ForegroundColor Yellow
                $skipModInstall = $true
            }
        }
    } else {
        Write-Host "    Using provided mod URL: $ModZipUrl" -ForegroundColor Gray
    }

    if (-not $skipModInstall) {
        $modZipPath = Join-Path $workDir "NPCQuestTracker.zip"

        Write-Host "    Downloading mod from: $ModZipUrl" -ForegroundColor Cyan
        try {
            $ProgressPreference = 'SilentlyContinue'
            Invoke-WebRequest -Uri $ModZipUrl -OutFile $modZipPath -UseBasicParsing
            $ProgressPreference = 'Continue'
            Write-Host "    Downloaded mod successfully." -ForegroundColor Green
        } catch {
            Write-Host "    ERROR: Failed to download mod: $_" -ForegroundColor Red
            Write-Host "    You can manually install the mod later by copying files to the Mods folder." -ForegroundColor Yellow
            $skipModInstall = $true
        }
    }
    Write-Host ""

    # Step 5: Install mod
    if (-not $skipModInstall) {
        Write-Host "[5/6] Installing NPC Quest Tracker mod..." -ForegroundColor Cyan

        # Create Mods directory if it doesn't exist
        $modsPath = Join-Path $StardewPath "Mods"
        if (-not (Test-Path $modsPath)) {
            New-Item -ItemType Directory -Path $modsPath -Force | Out-Null
            Write-Host "    Created Mods directory: $modsPath" -ForegroundColor Gray
        }

        # Create mod folder
        $modInstallPath = Join-Path $modsPath $MOD_NAME
        if (Test-Path $modInstallPath) {
            Write-Host "    Mod folder already exists. Removing old version..." -ForegroundColor Yellow
            Remove-Item -Path $modInstallPath -Recurse -Force
        }
        New-Item -ItemType Directory -Path $modInstallPath -Force | Out-Null

        # Extract mod
        try {
            Expand-Archive -Path $modZipPath -DestinationPath $modInstallPath -Force

            # Verify required files
            $manifestPath = Join-Path $modInstallPath "manifest.json"
            $dllPath = Join-Path $modInstallPath "$MOD_NAME.dll"

            if ((Test-Path $manifestPath) -and (Test-Path $dllPath)) {
                Write-Host "    Mod installed successfully to: $modInstallPath" -ForegroundColor Green
            } else {
                Write-Host "    WARNING: Mod files may not be in the correct structure." -ForegroundColor Yellow
                Write-Host "    Please verify that manifest.json and $MOD_NAME.dll are in: $modInstallPath" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "    ERROR: Failed to install mod: $_" -ForegroundColor Red
            Write-Host "    You can manually extract the mod to: $modInstallPath" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[5/6] Skipped mod installation." -ForegroundColor Yellow
        Write-Host "    To manually install, extract your mod files to:" -ForegroundColor Yellow
        Write-Host "    $(Join-Path $StardewPath "Mods\$MOD_NAME")" -ForegroundColor Yellow
    }
    Write-Host ""

    # Step 6: Provide launch instructions
    Write-Host "[6/6] Launch Instructions" -ForegroundColor Cyan
    Write-Host ""

    $smapiExePath = Join-Path $StardewPath "StardewModdingAPI.exe"

    if ($XboxGamePass) {
        Write-Host "    Xbox Game Pass Version:" -ForegroundColor Yellow
        Write-Host "    To play with mods, launch the game using:" -ForegroundColor White
        Write-Host "    $smapiExePath" -ForegroundColor Green
        Write-Host ""
        Write-Host "    You can create a desktop shortcut to this file for easy access." -ForegroundColor Gray
    } else {
        Write-Host "    Steam Version:" -ForegroundColor Yellow
        Write-Host "    To play with mods, you need to update your Steam launch options:" -ForegroundColor White
        Write-Host ""
        Write-Host "    1. Open Steam and go to your Library" -ForegroundColor White
        Write-Host "    2. Right-click 'Stardew Valley' and select 'Properties'" -ForegroundColor White
        Write-Host "    3. In the 'Launch Options' field, enter:" -ForegroundColor White
        Write-Host ""
        Write-Host "       `"$smapiExePath`" %command%" -ForegroundColor Green
        Write-Host ""
        Write-Host "    4. Close the properties window and launch the game normally through Steam" -ForegroundColor White
        Write-Host ""
        Write-Host "    Alternative: You can also launch the game directly using:" -ForegroundColor Gray
        Write-Host "    $smapiExePath" -ForegroundColor Gray
    }
    Write-Host ""

    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "  Installation Complete!" -ForegroundColor Green
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "The NPC Quest Tracker mod tracks NPCs on your map and shows quest markers." -ForegroundColor White
    Write-Host "Open your map (M key) to see NPC locations and quest indicators!" -ForegroundColor White
    Write-Host ""

} finally {
    # Cleanup
    Write-Host "Cleaning up temporary files..." -ForegroundColor Gray
    try {
        Remove-Item -Path $workDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Cleanup completed." -ForegroundColor Gray
    } catch {
        Write-Host "Could not remove temporary directory: $workDir" -ForegroundColor Yellow
        Write-Host "You may delete it manually." -ForegroundColor Yellow
    }
    Write-Host ""
}

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
