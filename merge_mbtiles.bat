@echo off
setlocal enabledelayedexpansion

:: =====================================================================
:: Configuration
:: =====================================================================
set FINAL_OUTPUT=%~dp0israel.mbtiles

:: Global min and max zoom for metadata update
set OVERALL_MIN_ZOOM=%~2
set OVERALL_MAX_ZOOM=%~3
if "%OVERALL_MIN_ZOOM%"=="" set OVERALL_MIN_ZOOM=6
if "%OVERALL_MAX_ZOOM%"=="" set OVERALL_MAX_ZOOM=14

:: =====================================================================
:: Check / Download sqlite3.exe if it doesn't exist
:: =====================================================================
set SQLITE_EXE=%~dp0sqlite3.exe
if not exist "%SQLITE_EXE%" (
    echo [INFO] sqlite3.exe not found in batch folder. Searching PATH...
    where sqlite3.exe >nul 2>nul
    if errorlevel 0 (
        set SQLITE_EXE=sqlite3.exe
    ) else (
        echo [INFO] Downloading sqlite3.exe...
        powershell -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://www.sqlite.org/2024/sqlite-tools-win-x64-3450200.zip' -OutFile '%~dp0sqlite-tools.zip'" 2>nul
        if exist "%~dp0sqlite-tools.zip" (
            echo [INFO] Extracting SQLite tools...
            powershell -Command "Expand-Archive -Path '%~dp0sqlite-tools.zip' -DestinationPath '%~dp0sqlite-temp' -Force"
            for /r "%~dp0sqlite-temp" %%F in (sqlite3.exe) do copy /Y "%%F" "%~dp0" >nul
            rmdir /S /Q "%~dp0sqlite-temp" 2>nul
            del /Q "%~dp0sqlite-tools.zip" 2>nul
        )
    )
)

:: =====================================================================
:: Merge Databases
:: =====================================================================

if not exist "%~dp0part1.mbtiles" (
    if exist "%~dp0output_merged.mbtiles" (
        echo [INFO] Master output_merged.mbtiles already produced by parallel script.
        copy /Y "%~dp0output_merged.mbtiles" "%FINAL_OUTPUT%" >nul
        goto :UPDATE_METADATA
    )
    echo [ERROR] Base file part1.mbtiles does not exist. Cannot merge.
    if /I not "%~1"=="nopause" pause
    exit /b 1
)

echo [INFO] Creating final output file by copying part1.mbtiles...
copy /Y "%~dp0part1.mbtiles" "%FINAL_OUTPUT%" >nul

for %%F in ("%~dp0part*.mbtiles") do (
    if not "%%~nxF"=="part1.mbtiles" (
        echo [INFO] Merging %%~nxF into final output...
        "%SQLITE_EXE%" "%FINAL_OUTPUT%" "ATTACH DATABASE '%%~F' AS toMerge; INSERT OR REPLACE INTO tiles SELECT * FROM toMerge.tiles; DETACH DATABASE toMerge;"
    )
)

:UPDATE_METADATA
echo [INFO] Updating metadata zoom levels...
"%SQLITE_EXE%" "%FINAL_OUTPUT%" "INSERT OR REPLACE INTO metadata (name, value) VALUES ('minzoom', '%OVERALL_MIN_ZOOM%');"
"%SQLITE_EXE%" "%FINAL_OUTPUT%" "INSERT OR REPLACE INTO metadata (name, value) VALUES ('maxzoom', '%OVERALL_MAX_ZOOM%');"
"%SQLITE_EXE%" "%FINAL_OUTPUT%" "INSERT OR REPLACE INTO metadata (name, value) VALUES ('format', 'png');"

echo [INFO] Cleaning up part files...
del /q "%~dp0part*.mbtiles" 2>nul
del /q "%~dp0part*.json" 2>nul
del /q "%~dp0output_merged.mbtiles" 2>nul

:: Copy to Demo debug directory if present
if exist "%~dp0Mapsui48.Demo\bin\Debug" (
    echo [INFO] Deploying updated map to Demo debug directory...
    copy /Y "%FINAL_OUTPUT%" "%~dp0Mapsui48.Demo\bin\Debug\somemap.mbtiles" >nul
    copy /Y "%FINAL_OUTPUT%" "%~dp0Mapsui48.Demo\bin\Debug\israel-and-palestine-260720.osm.mbtiles" >nul
)

echo ===================================================================
echo [SUCCESS] Final map is ready at: %FINAL_OUTPUT%
echo ===================================================================

if /I not "%~1"=="nopause" pause
exit /b 0
