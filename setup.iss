[Setup]
AppName=Park Zone KaraokeClub
AppVersion=1.0
AppPublisher=Park Zone
DefaultDirName={autopf}\KaraokeClub
DefaultGroupName=KaraokeClub
OutputDir=C:\Users\User\Desktop\KaraokeClub_output\Installer
OutputBaseFilename=KaraokeClub_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:"; Flags: unchecked

[Files]
Source: "C:\Users\User\Desktop\KaraokeClub_output\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KaraokeClub"; Filename: "{app}\KaraokeClub.exe"
Name: "{group}\Удалить KaraokeClub"; Filename: "{uninstallexe}"
Name: "{commondesktop}\KaraokeClub"; Filename: "{app}\KaraokeClub.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\KaraokeClub.exe"; Description: "Запустить приложение"; Flags: nowait postinstall skipifsilent