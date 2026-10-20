#define AppVersion GetEnv('GOATMETER_VERSION')
#if GetEnv('GOATMETER_CHANNEL') == 'beta'
  #define AppChannel "Beta"
  #define AppInstallFolder "GoatMeter Beta"
  #define AppIdentity "{{BBD765AF-61C4-476C-8D2D-772D90665C5F}"
#else
  #define AppChannel ""
  #define AppInstallFolder "GoatMeter"
  #define AppIdentity "{{A9EC39C0-6BCB-466B-92CE-E004CF7688BC}"
#endif
[Setup]
AppId={#AppIdentity}
AppName=GoatMeter {#AppChannel}
AppVersion={#AppVersion}
AppPublisher=GoatMeter
DefaultDirName={localappdata}\Programs\GoatMeter
DefaultGroupName=GoatMeter {#AppChannel}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=GoatMeter-Setup
SetupIconFile=..\src\Assets\Brand\GoatHead.ico
UninstallDisplayIcon={app}\GoatMeter.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\GoatMeter"; Filename: "{app}\GoatMeter.exe"
Name: "{autodesktop}\GoatMeter"; Filename: "{app}\GoatMeter.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\GoatMeter.exe"; Description: "Launch GoatMeter"; Flags: nowait postinstall skipifsilent
[Code]
function HasNpcap: Boolean;
begin
  Result := FileExists(ExpandConstant('{sys}\drivers\npcap.sys'));
end;
function NextButtonClick(CurPageID: Integer): Boolean;
var ErrorCode: Integer;
begin
  Result := True;
  if (CurPageID = wpReady) and not HasNpcap then
  begin
    if MsgBox('GoatMeter needs Npcap to read combat traffic. Download and install Npcap from its official website, then return here and click Install again. Open the download page now?', mbInformation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://npcap.com/#download', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;
