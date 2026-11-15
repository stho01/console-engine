@echo off
rem Starts the game in this folder in the classic console host (conhost), whatever Windows' default terminal is.
rem Windows Terminal ignores the window size and font the engine sets. Copied next to every example's exe.
setlocal
set "GAME="
for %%f in ("%~dp0*.exe") do if /i not "%%~nxf"=="ConsoleEngine.Logger.exe" set "GAME=%%~ff"
if not defined GAME (
    echo No game exe found next to %~nx0.
    pause
    exit /b 1
)
cd /d "%~dp0"
start "" conhost.exe "%GAME%"
