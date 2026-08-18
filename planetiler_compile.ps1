param (
    [string]$InputPbf = $env:INPUT_PBF,
    [string]$ToolsDir = "$PWD\tools",
    [string]$OutputDir = $PWD.Path
)

if ([string]::IsNullOrWhiteSpace($InputPbf)) {
    Write-Error "Please specify the input PBF file using -InputPbf or the `$env:INPUT_PBF environment variable."
    exit 1
}

if (-not (Test-Path $InputPbf)) {
    Write-Error "Input PBF file not found: $InputPbf"
    exit 1
}

$InputPbf = (Resolve-Path $InputPbf).Path
$pbfName = [System.IO.Path]::GetFileNameWithoutExtension($InputPbf)
$outFile = Join-Path $OutputDir "$pbfName.mbtiles"

if (-not (Test-Path $ToolsDir)) {
    New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null
}

$javaDir = Join-Path $ToolsDir "jdk-21"
$planetilerJar = Join-Path $ToolsDir "planetiler.jar"

# 1. Ensure Java 21+ is available
$javaExe = "java"
$javaVer = & java -version 2>&1
if ($javaVer -notmatch 'version "21' -and $javaVer -notmatch 'version "22' -and $javaVer -notmatch 'version "23') {
    $javaExe = Get-ChildItem -Path $javaDir -Filter java.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 | Select-Object -ExpandProperty FullName
    
    if (-not $javaExe -or -not (Test-Path $javaExe)) {
        Write-Host "[INFO] Java 21+ is required for Planetiler. Downloading portable JDK 21..."
        $jdkUrl = "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse"
        $jdkZip = Join-Path $ToolsDir "jdk.zip"
        Invoke-WebRequest -Uri $jdkUrl -OutFile $jdkZip
        
        Write-Host "[INFO] Extracting JDK 21..."
        Expand-Archive -Path $jdkZip -DestinationPath $javaDir -Force
        Remove-Item $jdkZip -Force
        
        $javaExe = Get-ChildItem -Path $javaDir -Filter java.exe -Recurse | Select-Object -First 1 | Select-Object -ExpandProperty FullName
        Write-Host "[INFO] Portable Java 21 installed at $javaExe"
    }
}

# 2. Ensure Planetiler is available
if (-not (Test-Path $planetilerJar)) {
    Write-Host "[INFO] Downloading Planetiler..."
    $planetilerUrl = "https://github.com/onthegomap/planetiler/releases/latest/download/planetiler.jar"
    Invoke-WebRequest -Uri $planetilerUrl -OutFile $planetilerJar
    Write-Host "[INFO] Successfully downloaded Planetiler!"
}

# 3. Clean existing output
if (Test-Path $outFile) {
    Write-Host "[INFO] Removing existing output file: $outFile"
    Remove-Item $outFile -Force
}

Write-Host "[INFO] Starting Planetiler vector compilation..."
Write-Host "[INFO] Input:  $InputPbf"
Write-Host "[INFO] Output: $outFile"
Write-Host "--------------------------------------------------------"

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

& $javaExe -Xmx4g -jar $planetilerJar --osm-path="$InputPbf" --output="$outFile" --force --download

if ($LASTEXITCODE -ne 0) {
    Write-Error "Planetiler compilation failed with exit code $LASTEXITCODE."
    exit 1
}

$stopwatch.Stop()
Write-Host "--------------------------------------------------------"
Write-Host "[INFO] Compilation finished in $($stopwatch.Elapsed.ToString("hh\:mm\:ss"))"
Write-Host "[INFO] Your vector tiles are ready at: $outFile"
