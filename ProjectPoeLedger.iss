; Optional installer source. Build Desktop.bat first, then compile with Inno Setup 6.
[Setup]
AppId=ProjectPoeLedger
AppName=Poe Ledger
AppVersion=0.38.1
DefaultDirName={localappdata}\Programs\ProjectPoeLedger
DefaultGroupName=Poe Ledger
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputBaseFilename=Project-Poe-Ledger-0.38.1-Setup
SetupIconFile=ProjectPoeLedger\PoeLedger.ico
UninstallDisplayIcon={app}\ProjectPoeLedger.exe
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
[Files]
Source: "Desktop\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
[Icons]
Name: "{group}\Poe Ledger"; Filename: "{app}\ProjectPoeLedger.exe"
Name: "{userdesktop}\Poe Ledger"; Filename: "{app}\ProjectPoeLedger.exe"
[Run]
Filename: "{app}\ProjectPoeLedger.exe"; Description: "Launch Poe Ledger"; Flags: nowait postinstall skipifsilent
