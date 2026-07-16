[Setup]
AppName=KosovaPOS
AppVersion=1.0.0
DefaultDirName={pf}\KosovaPOS
DefaultGroupName=KosovaPOS
UninstallDisplayIcon={app}\KosovaPOS.exe
OutputDir=.
OutputBaseFilename=KosovaPOS_Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Default DB files or .env files might go here too if needed

[Icons]
Name: "{group}\KosovaPOS"; Filename: "{app}\KosovaPOS.exe"
Name: "{commondesktop}\KosovaPOS"; Filename: "{app}\KosovaPOS.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\KosovaPOS.exe"; Description: "Launch KosovaPOS"; Flags: nowait postinstall skipifsilent
