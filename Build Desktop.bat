@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 goto missing
 dotnet --list-sdks | findstr /b "10." >nul
if errorlevel 1 goto missing
echo Building Poe Ledger 0.38.1. NuGet restore may use the internet; no PoE API calls.
dotnet publish "ProjectPoeLedger\ProjectPoeLedger.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0Desktop" > "%~dp0build-log.txt" 2>&1
if errorlevel 1 (
 type "%~dp0build-log.txt"
 echo Build failed. Send build-log.txt for diagnosis.
 pause
 exit /b 1
)
echo Build succeeded. Start Project Poe Ledger.bat to launch.
pause
exit /b 0
:missing
echo Install .NET 10 SDK x64: https://dotnet.microsoft.com/download/dotnet/10.0
pause
exit /b 1
