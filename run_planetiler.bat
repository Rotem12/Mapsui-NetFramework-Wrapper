@echo off
setlocal

:: Check if a file was dragged and dropped onto the bat file
if "%~1"=="" (
    set /p INPUT_PBF="Enter the path to your .osm.pbf file: "
) else (
    set INPUT_PBF=%~1
)

:: Remove quotes if present
set INPUT_PBF=%INPUT_PBF:"=%

if not exist "%INPUT_PBF%" (
    echo [ERROR] Could not find the file: "%INPUT_PBF%"
    echo Make sure you provided a valid path to your .osm.pbf file.
    pause
    exit /b 1
)

echo [INFO] Launching Planetiler Compiler for: "%INPUT_PBF%"
echo.

:: Execute the powershell script located in the same directory
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%~dp0planetiler_compile.ps1" -InputPbf "%INPUT_PBF%"

echo.
pause
