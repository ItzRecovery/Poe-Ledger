@echo off
cd /d "%~dp0"
if not exist "Desktop\ProjectPoeLedger.exe" (
  call "%~dp0Build Desktop.bat"
  if errorlevel 1 exit /b 1
)
start "" "%~dp0Desktop\ProjectPoeLedger.exe"
