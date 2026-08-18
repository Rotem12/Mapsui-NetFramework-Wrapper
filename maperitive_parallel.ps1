param (
    [int]$MinZoom = 7,
    [int]$MaxZoom = 14,
    [string]$InputPbf = "somemap.osm.pbf",
    [string]$MaperitiveDir = "",
    [int]$NumInstances = 0,
    [double]$MinLat = 0,
    [double]$MaxLat = 0,
    [double]$MinLon = 0,
    [double]$MaxLon = 0
)

if ($env:MIN_ZOOM) { $MinZoom = [int]$env:MIN_ZOOM }
if ($env:MAX_ZOOM) { $MaxZoom = [int]$env:MAX_ZOOM }
if ($env:INPUT_PBF) { $InputPbf = $env:INPUT_PBF }
if ($env:MAPERITIVE_DIR) { $MaperitiveDir = $env:MAPERITIVE_DIR }
if ($env:NUM_INSTANCES) { $NumInstances = [int]$env:NUM_INSTANCES }

if ($NumInstances -le 0) {
    # Default to 8 workers for low memory consumption (~6.4 GB RAM)
    $NumInstances = 8
}

$workingDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $workingDir) { $workingDir = $PWD.Path }

if (-not (Test-Path $InputPbf)) {
    $altPbf = Join-Path $workingDir $InputPbf
    if (Test-Path $altPbf) { $InputPbf = $altPbf }
}

if (Test-Path $InputPbf) {
    $InputPbf = (Get-Item $InputPbf).FullName
}

# --- Auto-Detect Maperitive Directory ---
if (-not $MaperitiveDir -or -not (Test-Path (Join-Path $MaperitiveDir "Maperitive.Console.exe"))) {
    $candidatePaths = @(
        "C:\Users\rotem\OneDrive\Desktop\Maperitive",
        "$env:USERPROFILE\OneDrive\Desktop\Maperitive",
        "$env:USERPROFILE\Desktop\Maperitive",
        "C:\Maperitive",
        "E:\Maperitive"
    )
    foreach ($cand in $candidatePaths) {
        if (Test-Path (Join-Path $cand "Maperitive.Console.exe")) {
            $MaperitiveDir = $cand
            break
        }
    }
}

if (-not (Test-Path (Join-Path $MaperitiveDir "Maperitive.Console.exe"))) {
    Write-Host "[ERROR] Could not locate Maperitive.Console.exe!"
    Write-Host "[ERROR] Checked directory: '$MaperitiveDir'"
    Write-Host "[ERROR] Please specify -MaperitiveDir <path-to-maperitive>."
    exit 1
}

Write-Host "=================================================================="
Write-Host " 🚀 MAPERITIVE STAGGERED $NumInstances-CORE WORKLOAD GENERATOR"
Write-Host "=================================================================="
Write-Host "[INFO] Input PBF       : $InputPbf"
Write-Host "[INFO] Maperitive Dir  : $MaperitiveDir"
Write-Host "[INFO] Zoom Range      : $MinZoom to $MaxZoom"
Write-Host "[INFO] Core Capacity   : $NumInstances Workers (Low Memory Footprint)"

# --- 1. Auto-Detect Bounding Box if not supplied ---
if ($MinLat -eq 0 -and $MaxLat -eq 0) {
    Write-Host "[INFO] Auto-detecting spatial bounding box from PBF header..."
    try {
        $dumpToolExe = Join-Path $workingDir "DumpTool\bin\Debug\net10.0\DumpTool.exe"
        if (-not (Test-Path $dumpToolExe)) {
            $dumpToolExe = Join-Path $workingDir "DumpTool\bin\Debug\net8.0\DumpTool.exe"
        }
        
        if (Test-Path $dumpToolExe) {
            $bboxOutput = & $dumpToolExe "$InputPbf" | Select-String -Pattern "BBOX:"
            if ($bboxOutput) {
                $parts = ($bboxOutput.ToString().Replace("BBOX:", "")).Split(",")
                $MinLat = [double]$parts[0]
                $MinLon = [double]$parts[1]
                $MaxLat = [double]$parts[2]
                $MaxLon = [double]$parts[3]
                Write-Host "[SUCCESS] Auto-detected BBox: Lat($MinLat to $MaxLat), Lon($MinLon to $MaxLon)"
            }
        }
    } catch {
        Write-Host "[WARN] Auto-detection failed. Falling back to default region."
    }

    if ($MinLat -eq 0 -and $MaxLat -eq 0) {
        $MinLat = 29.45; $MaxLat = 33.35; $MinLon = 34.20; $MaxLon = 35.90
        Write-Host "[INFO] Using fallback BBox: Lat($MinLat to $MaxLat), Lon($MinLon to $MaxLon)"
    }
}

# --- 2. Mathematically Optimal Worker Allocation ---
$zoomDiff = $MaxZoom - $MinZoom
$tasks = @()

if ($zoomDiff -ge 2 -and $NumInstances -ge 8) {
    # Tier 1: Lower Zooms (MinZoom to MaxZoom - 2) -> 1 Worker
    $tasks += @{
        Name = "Part01_Z${MinZoom}_to_Z$($MaxZoom-2)_LowZooms"
        MinZ = $MinZoom
        MaxZ = $MaxZoom - 2
        UseBounds = $false
    }

    # Tier 2: Penultimate Zoom (MaxZoom - 1) -> 2 Workers (Latitude Strips)
    $penultWorkers = [Math]::Max(1, [Math]::Floor(($NumInstances - 1) * 0.25))
    $pLatStep = ($MaxLat - $MinLat) / $penultWorkers
    for ($p = 0; $p -lt $penultWorkers; $p++) {
        $pMinLat = [Math]::Round($MinLat + ($p * $pLatStep), 6)
        if ($p -eq ($penultWorkers - 1)) { $pMaxLat = $MaxLat } else { $pMaxLat = [Math]::Round($MinLat + (($p + 1) * $pLatStep), 6) }
        $partNumStr = ($p + 2).ToString("D2")

        $tasks += @{
            Name = "Part${partNumStr}_Z$($MaxZoom-1)_Penult_Strip$($p+1)"
            MinZ = $MaxZoom - 1
            MaxZ = $MaxZoom - 1
            UseBounds = $true
            BBox = "$MinLon,$pMinLat,$MaxLon,$pMaxLat"
        }
    }

    # Tier 3: Max Zoom (MaxZoom) -> Remaining Workers (Grid)
    $maxZWorkers = $NumInstances - 1 - $penultWorkers
    $maxZCols = [Math]::Ceiling([Math]::Sqrt($maxZWorkers * 1.3))
    $maxZRows = [Math]::Ceiling($maxZWorkers / $maxZCols)
    $mLatStep = ($MaxLat - $MinLat) / $maxZRows
    $mLonStep = ($MaxLon - $MinLon) / $maxZCols

    $mCount = 1
    for ($r = 0; $r -lt $maxZRows; $r++) {
        for ($c = 0; $c -lt $maxZCols; $c++) {
            if ($tasks.Count -ge $NumInstances) { break }
            $cMinLat = [Math]::Round($MinLat + ($r * $mLatStep), 6)
            if ($r -eq ($maxZRows - 1)) { $cMaxLat = $MaxLat } else { $cMaxLat = [Math]::Round($MinLat + (($r + 1) * $mLatStep), 6) }
            $cMinLon = [Math]::Round($MinLon + ($c * $mLonStep), 6)
            if ($c -eq ($maxZCols - 1)) { $cMaxLon = $MaxLon } else { $cMaxLon = [Math]::Round($MinLon + (($c + 1) * $mLonStep), 6) }

            $partNumStr = ($tasks.Count + 1).ToString("D2")
            $tasks += @{
                Name = "Part${partNumStr}_Z${MaxZoom}_MaxZ_R${r}_C${c}"
                MinZ = $MaxZoom
                MaxZ = $MaxZoom
                UseBounds = $true
                BBox = "$cMinLon,$cMinLat,$cMaxLon,$cMaxLat"
            }
            $mCount++
        }
    }
} else {
    # Dynamic fallback for smaller core counts
    $lowCount = 1
    $tasks += @{ Name = "Part01_LowZooms"; MinZ = $MinZoom; MaxZ = [Math]::Max($MinZoom, $MaxZoom - 2); UseBounds = $false }
    
    $rem = $NumInstances - 1
    $pCount = [Math]::Max(1, [Math]::Floor($rem * 0.25))
    $mCount = $rem - $pCount

    # Penultimate zoom split
    $pLatStep = ($MaxLat - $MinLat) / $pCount
    for ($p = 0; $p -lt $pCount; $p++) {
        $pMinLat = [Math]::Round($MinLat + ($p * $pLatStep), 6)
        if ($p -eq ($pCount - 1)) { $pMaxLat = $MaxLat } else { $pMaxLat = [Math]::Round($MinLat + (($p + 1) * $pLatStep), 6) }
        $partStr = ($p + 2).ToString("D2")
        $tasks += @{ Name = "Part${partStr}_Penult_P$($p+1)"; MinZ = $MaxZoom - 1; MaxZ = $MaxZoom - 1; UseBounds = $true; BBox = "$MinLon,$pMinLat,$MaxLon,$pMaxLat" }
    }

    # Max zoom split
    $mCols = [Math]::Ceiling([Math]::Sqrt($mCount * 1.5))
    $mRows = [Math]::Ceiling($mCount / $mCols)
    $mLatStep = ($MaxLat - $MinLat) / $mRows
    $mLonStep = ($MaxLon - $MinLon) / $mCols
    $idx = $pCount + 2
    for ($r = 0; $r -lt $mRows; $r++) {
        for ($c = 0; $c -lt $mCols; $c++) {
            if ($tasks.Count -ge $NumInstances) { break }
            $cMinLat = [Math]::Round($MinLat + ($r * $mLatStep), 6)
            if ($r -eq ($mRows - 1)) { $cMaxLat = $MaxLat } else { $cMaxLat = [Math]::Round($MinLat + (($r + 1) * $mLatStep), 6) }
            $cMinLon = [Math]::Round($MinLon + ($c * $mLonStep), 6)
            if ($c -eq ($mCols - 1)) { $cMaxLon = $MaxLon } else { $cMaxLon = [Math]::Round($MinLon + (($c + 1) * $mLonStep), 6) }
            $partStr = ($idx).ToString("D2")
            $tasks += @{ Name = "Part${partStr}_MaxZ_R${r}_C${c}"; MinZ = $MaxZoom; MaxZ = $MaxZoom; UseBounds = $true; BBox = "$cMinLon,$cMinLat,$cMaxLon,$cMaxLat" }
            $idx++
        }
    }
}

Write-Host "[INFO] Optimized Workload Schedule ($($tasks.Count) Workers):"
foreach ($t in $tasks) {
    if ($t.UseBounds) {
        Write-Host "   - [$($t.Name)] Zooms $($t.MinZ)-$($t.MaxZ) | BBox: $($t.BBox)"
    } else {
        Write-Host "   - [$($t.Name)] Zooms $($t.MinZ)-$($t.MaxZ) | Full Region"
    }
}

# --- 3. Execute Parallel Maperitive Processes with BelowNormal Priority ---
$waitJobs = @()
$cleanupDirs = @()
$cleanupFiles = @()
$partFiles = @()

$partIdx = 1
foreach ($t in $tasks) {
    $partName = "part${partIdx}"
    $outFile = Join-Path $workingDir "${partName}.mbtiles"
    $partFiles += $outFile

    $cloneDir = Join-Path $env:TEMP "Maperitive_${partName}_$(Get-Random)"
    $cleanupDirs += $cloneDir
    Copy-Item -Path $MaperitiveDir -Destination $cloneDir -Recurse -Force

    $mscript = Join-Path $env:TEMP "map_${partName}_$(Get-Random).mscript"
    $cleanupFiles += $mscript
    $rulesetPath = Join-Path $workingDir "Transparent.mrules"

    if ($t.UseBounds) {
        $boundsCmd = "set-geo-bounds $($t.BBox)"
    } else {
        $boundsCmd = "geo-bounds-use-source"
    }

    $scriptContent = @"
clear-map
use-ruleset location="$rulesetPath"
load-source "$InputPbf"
$boundsCmd
generate-mbtiles file="$outFile" minzoom=$($t.MinZ) maxzoom=$($t.MaxZ)
exit
"@
    Set-Content -Path $mscript -Value $scriptContent -Encoding UTF8

    $exePath = Join-Path $cloneDir "Maperitive.Console.exe"
    $jobScript = {
        param($exe, $mscript, $tag, $workDir)
        Set-Location -Path $workDir
        try {
            [System.Diagnostics.Process]::GetCurrentProcess().PriorityClass = [System.Diagnostics.ProcessPriorityClass]::BelowNormal
        } catch {}
        & $exe $mscript | ForEach-Object { "[$tag] $_" }
    }

    Write-Host "[LAUNCH] Starting worker $($t.Name)..."
    $job = Start-Job -ScriptBlock $jobScript -ArgumentList $exePath, $mscript, $t.Name, $cloneDir
    $waitJobs += $job
    $partIdx++

    # Stagger launch by 1.5s to prevent PBF parser thread contention & memory spikes
    Start-Sleep -Milliseconds 1500
}

# --- 4. Monitor & Stream Progress Logs ---
$masterLog = Join-Path $workingDir "generation.log"
Clear-Content $masterLog -ErrorAction SilentlyContinue

Write-Host "[INFO] All $($tasks.Count) parallel processes running smoothly with low background priority..."

while ($waitJobs | Where-Object { $_.State -eq 'Running' }) {
    $out = Receive-Job -Job $waitJobs
    if ($out) {
        $out | Write-Host
        $out | Out-File -FilePath $masterLog -Append -Encoding UTF8
    }
    Start-Sleep -Seconds 1
}

$out = Receive-Job -Job $waitJobs
if ($out) {
    $out | Write-Host
    $out | Out-File -FilePath $masterLog -Append -Encoding UTF8
}
$waitJobs | Remove-Job

# --- 5. Verify & Retry Missing Parts ---
$missingParts = @()
for ($i = 0; $i -lt $partFiles.Count; $i++) {
    if (-not (Test-Path $partFiles[$i])) {
        $missingParts += $tasks[$i]
    }
}

if ($missingParts.Count -gt 0) {
    Write-Host "[WARN] $($missingParts.Count) worker process(es) encountered an error. Retrying missing parts sequentially..."
    for ($i = 0; $i -lt $partFiles.Count; $i++) {
        $pf = $partFiles[$i]
        $tk = $tasks[$i]
        if (-not (Test-Path $pf)) {
            Write-Host "[RETRY] Rendering missing part $($tk.Name)..."
            $mscript = Join-Path $env:TEMP "map_retry_$(Get-Random).mscript"
            $boundsCmd = if ($tk.UseBounds) { "set-geo-bounds $($tk.BBox)" } else { "geo-bounds-use-source" }
            $scriptContent = @"
clear-map
use-ruleset location="$workingDir\Transparent.mrules"
load-source "$InputPbf"
$boundsCmd
generate-mbtiles file="$pf" minzoom=$($tk.MinZ) maxzoom=$($tk.MaxZ)
exit
"@
            Set-Content -Path $mscript -Value $scriptContent -Encoding UTF8
            $exePath = Join-Path $MaperitiveDir "Maperitive.Console.exe"
            & $exePath $mscript | Write-Host
            Remove-Item $mscript -Force -ErrorAction SilentlyContinue
        }
    }
}

# --- 6. Merge MBTiles into Single File ---
$finalMbtiles = Join-Path $workingDir "output_merged.mbtiles"
Write-Host "[INFO] Merging $($partFiles.Count) part MBTiles into $finalMbtiles using SQLite..."

if (Test-Path $finalMbtiles) { Remove-Item $finalMbtiles -Force }

$firstPart = $partFiles[0]
if (Test-Path $firstPart) {
    Copy-Item $firstPart $finalMbtiles -Force

    for ($i = 1; $i -lt $partFiles.Count; $i++) {
        $part = $partFiles[$i]
        if (Test-Path $part) {
            Write-Host "[INFO] Merging $part into $finalMbtiles..."
            $sqliteCmd = "ATTACH DATABASE '$part' AS part; INSERT OR REPLACE INTO tiles SELECT * FROM part.tiles; DETACH DATABASE part;"
            try {
                $p = Start-Process -FilePath "sqlite3.exe" -ArgumentList "`"$finalMbtiles`" `"$sqliteCmd`"" -NoNewWindow -Wait -PassThru
            } catch {
                Write-Host "[WARN] sqlite3.exe not found in PATH. Part files remain saved as part1.mbtiles, part2.mbtiles, etc."
            }
        }
    }
}

# --- 7. Cleanup ---
Write-Host "[INFO] Cleaning up temporary folders and files..."
foreach ($f in $cleanupFiles) { if (Test-Path $f) { Remove-Item $f -Force -ErrorAction SilentlyContinue } }
foreach ($d in $cleanupDirs) { if (Test-Path $d) { Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue } }

Write-Host "=================================================================="
Write-Host " 🎉 PARALLEL TILE GENERATION COMPLETE ($($tasks.Count) WORKERS SYNCHRONIZED)"
Write-Host "=================================================================="
