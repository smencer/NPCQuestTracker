@echo off
REM Launch SMAPI with output to both console and log file

REM Change this path to your SMAPI installation directory
set SMAPI_PATH=S:\SteamLibrary\steamapps\common\Stardew Valley

echo Starting SMAPI...
echo.

REM Navigate to SMAPI directory
cd /d "%SMAPI_PATH%"

REM Launch SMAPI - output goes to console AND gets captured to file
REM Using PowerShell's Tee-Object to split the output
REM powershell -Command "& { & '%SMAPI_PATH%\StardewModdingAPI.exe' } 2>&1 | Tee-Object -FilePath '%LOG_FILE%'"
"%SMAPI_PATH%\StardewModdingAPI.exe"

echo.
echo Game closed. Log saved to: %LOG_FILE%
pause
