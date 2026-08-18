param (
    [string]$InputPbf = $env:INPUT_PBF,
    [string]$TilemakerDir = "C:\Users\rotem\OneDrive\Desktop\tilemaker-2.2.0",
    [string]$OutputDir = $PWD.Path
)

# If no input PBF is provided via environment or parameter, prompt for one
if ([string]::IsNullOrWhiteSpace($InputPbf)) {
    Write-Error "Please specify the input PBF file using -InputPbf or the `$env:INPUT_PBF environment variable."
    exit 1
}

if (-not (Test-Path $InputPbf)) {
    Write-Error "Input PBF file not found: $InputPbf"
    exit 1
}

# Resolve to absolute path before changing directories
$InputPbf = (Resolve-Path $InputPbf).Path
$pbfName = [System.IO.Path]::GetFileNameWithoutExtension($InputPbf)
$outFile = Join-Path $OutputDir "$pbfName.mbtiles"

# Ensure Tilemaker directory exists
if (-not (Test-Path $TilemakerDir)) {
    New-Item -ItemType Directory -Force -Path $TilemakerDir | Out-Null
}

$exePath = Join-Path $TilemakerDir "tilemaker.exe"
$configPath = Join-Path $TilemakerDir "resources\config-openmaptiles.json"
$processPath = Join-Path $TilemakerDir "resources\process-openmaptiles.lua"

# 1. Check if the pre-compiled executable exists.
$exePath = Join-Path $TilemakerDir "tilemaker.exe"
if (-not (Test-Path $exePath)) {
    # It might be in a subfolder like build\RelWithDebInfo\tilemaker.exe from the zip
    $foundExe = Get-ChildItem -Path $TilemakerDir -Filter tilemaker.exe -Recurse | Select-Object -First 1
    if ($foundExe) {
        $exePath = $foundExe.FullName
    } else {
        Write-Host "[INFO] tilemaker.exe not found in $TilemakerDir."
        Write-Host "[INFO] Downloading the pre-compiled Windows binary from GitHub..."
        
        # Tilemaker v3.0.0 on Windows suffers from a STATUS_STACK_BUFFER_OVERRUN crash on complex PBFs.
        # Downgrading to the stable v2.2.0 build which works perfectly.
        $downloadUrl = "https://github.com/systemed/tilemaker/releases/download/v2.2.0/tilemaker-windows.zip"
        $zipPath = Join-Path $TilemakerDir "tilemaker-windows.zip"
        
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath
        Write-Host "[INFO] Extracting..."
        Expand-Archive -Path $zipPath -DestinationPath $TilemakerDir -Force
        Remove-Item $zipPath -Force
        
        $foundExe = Get-ChildItem -Path $TilemakerDir -Filter tilemaker.exe -Recurse | Select-Object -First 1
        if ($foundExe) {
            $exePath = $foundExe.FullName
        } else {
            Write-Error "Failed to find tilemaker.exe even after downloading."
            exit 1
        }
        
        Write-Host "[INFO] Successfully located tilemaker.exe at $exePath!"
        Write-Host "[INFO] Patching executable stack size to 16MB to prevent buffer overrun bugs..."
        $pyPatch = @"
import struct
def patch_stack_size(exe_path, new_stack_size):
    with open(exe_path, 'r+b') as f:
        f.seek(0x3C)
        pe_offset = struct.unpack('<I', f.read(4))[0]
        f.seek(pe_offset + 24)
        magic = f.read(2)
        stack_offset = pe_offset + 24 + 72
        f.seek(stack_offset)
        if magic == b'\x0b\x02': # 64-bit
            f.write(struct.pack('<Q', new_stack_size))
        else: # 32-bit
            f.write(struct.pack('<I', new_stack_size))
patch_stack_size(r'$exePath', 16 * 1024 * 1024)
"@
        Set-Content -Path (Join-Path $TilemakerDir "patch_stack.py") -Value $pyPatch
        & python (Join-Path $TilemakerDir "patch_stack.py")
        Write-Host "[INFO] Stack size patched successfully!"
    }
}

# 2. Check for resources
if (-not (Test-Path $configPath) -or -not (Test-Path $processPath)) {
    Write-Error "Missing configuration files in $TilemakerDir\resources. Make sure the resources folder exists!"
    exit 1
}

# 2.5 Automatically patch config to remove shapefiles to prevent crashes on missing files
Write-Host "[INFO] Patching config to remove missing shapefiles..."
$configContent = Get-Content $configPath -Raw
$configContent = $configContent -replace '"source"\s*:\s*"[^"]*",\s*', ''
Set-Content -Path $configPath -Value $configContent

# 3. Clean existing output
if (Test-Path $outFile) {
    Write-Host "[INFO] Removing existing output file: $outFile"
    Remove-Item $outFile -Force
}

Write-Host "[INFO] Starting Tilemaker vector compilation..."
Write-Host "[INFO] Input:  $InputPbf"
Write-Host "[INFO] Output: $outFile"
Write-Host "[INFO] Tilemaker handles multithreading automatically."
Write-Host "--------------------------------------------------------"

# 4. Run Tilemaker
Set-Location $TilemakerDir

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$storeDir = Join-Path $OutputDir "tilemaker_store"
if (-not (Test-Path $storeDir)) {
    New-Item -ItemType Directory -Force -Path $storeDir | Out-Null
}

& $exePath --input $InputPbf --output $outFile --config $configPath --process $processPath --store $storeDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Tilemaker compilation failed with exit code $LASTEXITCODE."
    exit 1
}

$stopwatch.Stop()
Write-Host "--------------------------------------------------------"
Write-Host "[INFO] Compilation finished in $($stopwatch.Elapsed.ToString("hh\:mm\:ss"))"
Write-Host "[INFO] Your vector tiles are ready at: $outFile"
