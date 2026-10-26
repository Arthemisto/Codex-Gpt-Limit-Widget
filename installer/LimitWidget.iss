#ifndef AppVersion
  #define AppVersion "0.4.0"
#endif
#ifndef ProjectRoot
  #define ProjectRoot ".."
#endif
#ifndef PublishDir
  #define PublishDir ProjectRoot + "\artifacts\installer-payload"
#endif

[Setup]
AppId={{3F2EA369-086F-4184-9087-D8E939162E29}
AppName=Codex Limit Widget
AppVersion={#AppVersion}
AppPublisher=Arthemisto
AppPublisherURL=https://github.com/Arthemisto/Codex-Gpt-Limit-Widget
AppSupportURL=https://github.com/Arthemisto/Codex-Gpt-Limit-Widget/issues
AppUpdatesURL=https://github.com/Arthemisto/Codex-Gpt-Limit-Widget/releases
DefaultDirName={localappdata}\Programs\Codex Limit Widget
DefaultGroupName=Codex Limit Widget
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
AppMutex=Local\CodexGptLimitWidget
CloseApplications=no
RestartApplications=no
OutputDir={#ProjectRoot}\artifacts\installer
OutputBaseFilename=CodexLimitWidget-{#AppVersion}-setup-x64
SetupIconFile={#ProjectRoot}\src\LimitWidget\Assets\app.ico
UninstallDisplayIcon={app}\LimitWidget.exe
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\LimitWidget.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Codex Limit Widget"; Filename: "{app}\LimitWidget.exe"
Name: "{autodesktop}\Codex Limit Widget"; Filename: "{app}\LimitWidget.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LimitWidget.exe"; Description: "{cm:LaunchProgram,Codex Limit Widget}"; Flags: nowait postinstall skipifsilent

[Code]
const
  StartupKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupName = 'CodexGptLimitWidget';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Preserve an already-enabled startup preference, pointing it to the installed EXE. }
  if (CurStep = ssPostInstall) and RegValueExists(HKCU, StartupKey, StartupName) then
    RegWriteStringValue(HKCU, StartupKey, StartupName,
      '"' + ExpandConstant('{app}\LimitWidget.exe') + '"');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if (CurUninstallStep = usUninstall) and
     RegQueryStringValue(HKCU, StartupKey, StartupName, Command) and
     (CompareText(Command, '"' + ExpandConstant('{app}\LimitWidget.exe') + '"') = 0) then
    RegDeleteValue(HKCU, StartupKey, StartupName);
  { Account credentials and preferences are user data and are not deleted here. }
end;
