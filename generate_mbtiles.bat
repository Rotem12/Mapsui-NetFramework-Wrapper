@echo off
setlocal enabledelayedexpansion

:: =====================================================================
:: Configuration - Edit these variables as needed
:: =====================================================================

if "%INPUT_PBF%"=="" set INPUT_PBF=E:\Downloads\israel-and-palestine-260720.osm.pbf
if not exist "%INPUT_PBF%" set INPUT_PBF=%~dp0israel-and-palestine-260720.osm.pbf
if not exist "%INPUT_PBF%" set INPUT_PBF=%~dp0israel.osm.pbf

if "%MAPERITIVE_DIR%"=="" set MAPERITIVE_DIR=C:\Users\rotem\OneDrive\Desktop\Maperitive
if not exist "%MAPERITIVE_DIR%" set MAPERITIVE_DIR=C:\Maperitive

:: Set your desired total zoom range here!
if "%MIN_ZOOM%"=="" set MIN_ZOOM=7
if "%MAX_ZOOM%"=="" set MAX_ZOOM=14

:: Set parallel worker instances (8 workers = ~6.4 GB RAM, 6 workers = ~4.8 GB RAM)
if "%NUM_INSTANCES%"=="" set NUM_INSTANCES=8

:: =====================================================================
:: Execute Dynamic Parallel Generation via PowerShell
:: =====================================================================

echo ===================================================================
echo [INFO] MAPERITIVE PARALLEL GENERATOR LAUNCHER
echo [INFO] Input PBF      : %INPUT_PBF%
echo [INFO] Maperitive Dir : %MAPERITIVE_DIR%
echo [INFO] Zoom Range     : %MIN_ZOOM% to %MAX_ZOOM%
echo [INFO] Workers        : %NUM_INSTANCES% Instances (Low Memory & BelowNormal Priority)
echo ===================================================================

:: Clean up old generated part files
del /q "%~dp0part*.mbtiles" 2>nul
del /q "%~dp0part*.json" 2>nul
del /q "%~dp0output_merged.mbtiles" 2>nul

:: Run PowerShell script with arguments
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0maperitive_parallel.ps1" -MinZoom %MIN_ZOOM% -MaxZoom %MAX_ZOOM% -InputPbf "%INPUT_PBF%" -MaperitiveDir "%MAPERITIVE_DIR%" -NumInstances %NUM_INSTANCES%

if errorlevel 1 (
    echo [ERROR] PowerShell execution failed.
    pause
    exit /b 1
)

echo [INFO] Handing over to merge script...
call "%~dp0merge_mbtiles.bat" nopause %MIN_ZOOM% %MAX_ZOOM%

echo ===================================================================
echo [SUCCESS] Master generation pipeline finished successfully!
echo ===================================================================
pause
exit /b 0
