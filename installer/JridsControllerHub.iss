#define MyAppName "Jrids Controller Hub"
#define MyAppVersion "1.0.8"
#define MyAppPublisher "Jrids"
#define MyAppURL "https://x.com/imjrid"
#define MyAppExeName "Jrids Controller Hub.exe"

[Setup]
AppId={{8F3C2A91-4B6E-4D1A-9C7F-2E5A8B0D1C44}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL=https://www.youtube.com/@imjrid
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=JridsControllerHubSetup
SetupIconFile=..\assets\jrids.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifnotsilent

[Code]
const
  UninstallRegKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3C2A91-4B6E-4D1A-9C7F-2E5A8B0D1C44}_is1';

var
  ExistingInstallPage: TInputOptionWizardPage;

function GetUninstallString: String;
var
  UninstallString: String;
begin
  UninstallString := '';
  if not RegQueryStringValue(HKLM, UninstallRegKey, 'UninstallString', UninstallString) then
    RegQueryStringValue(HKCU, UninstallRegKey, 'UninstallString', UninstallString);
  Result := UninstallString;
end;

function IsAlreadyInstalled: Boolean;
begin
  Result := GetUninstallString() <> '';
end;

procedure InitializeWizard;
begin
  if not IsAlreadyInstalled() then
    Exit;

  ExistingInstallPage := CreateInputOptionPage(wpWelcome,
    'Already installed',
    'Jrids Controller Hub is already installed on this PC.',
    'Choose what this setup should do, then click Next.',
    True, False);
  ExistingInstallPage.Add('Update or reinstall');
  ExistingInstallPage.Add('Uninstall');
  ExistingInstallPage.Values[0] := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
  UninstallPath: String;
begin
  Result := True;
  if (ExistingInstallPage = nil) or (CurPageID <> ExistingInstallPage.ID) then
    Exit;

  if not ExistingInstallPage.Values[1] then
    Exit;

  UninstallPath := RemoveQuotes(GetUninstallString());
  if (UninstallPath <> '') and FileExists(UninstallPath) then
  begin
    if Exec(UninstallPath, '', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
      MsgBox('Jrids Controller Hub has been uninstalled.', mbInformation, MB_OK);
  end
  else
    MsgBox('Could not find the uninstaller. Remove it from Windows Settings → Apps instead.', mbError, MB_OK);

  Abort;
end;
