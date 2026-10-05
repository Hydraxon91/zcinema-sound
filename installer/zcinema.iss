; ZCinema Sound — Inno Setup 6 script
; Builds an installer for the native C# app. Equalizer APO is a *detected
; prerequisite* (GPLv2) — it is never bundled; the installer links to its
; official download and aborts until it is present.
;
; Build:  ISCC.exe /DAppExeDir="<publish dir>" /DVersion=0.1.0 zcinema.iss
; (or use ..\installer\build-installer.ps1)

#ifndef AppExeDir
  #define AppExeDir "..\src-app\ZCinemaSound.App\bin\Release\net10.0-windows\win-x64\publish"
#endif
#ifndef Version
  #define Version "0.1.0"
#endif
#ifndef OutputDir
  #define OutputDir "output"
#endif

[Setup]
AppId={{B7E1C2D4-9A3F-4C6E-8B2A-1D5E7F903C11}
AppName=ZCinema Sound
AppVersion={#Version}
AppPublisher=ZCinema Sound
DefaultDirName={autopf}\ZCinema Sound
DefaultGroupName=ZCinema Sound
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=ZCinemaSound-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=..\assets\ZCinemaSound.ico
UninstallDisplayIcon={app}\ZCinemaSound.App.exe
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "autostart"; Description: "Start ZCinema Sound with Windows (tray)"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "{#AppExeDir}\ZCinemaSound.App.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\ZCinema Sound"; Filename: "{app}\ZCinemaSound.App.exe"
Name: "{autodesktop}\ZCinema Sound"; Filename: "{app}\ZCinemaSound.App.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ZCinemaSound"; ValueData: """{app}\ZCinemaSound.App.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
; wire the Equalizer APO profile + config include (headless, quiet)
Filename: "{app}\ZCinemaSound.App.exe"; Parameters: "--install --quiet"; Flags: runhidden waituntilterminated; StatusMsg: "Wiring the Equalizer APO config..."
; offer to open Equalizer APO's Device Selector so the APO attaches to the Z Cinema
Filename: "{code:DeviceSelectorPath}"; Description: "Open Equalizer APO Device Selector now (tick 'Speakers (Z Cinema)', then reboot)"; Flags: postinstall nowait skipifsilent; Check: HasDeviceSelector
; launch the app
Filename: "{app}\ZCinemaSound.App.exe"; Description: "Launch ZCinema Sound"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; revert the config.txt include before files are removed
Filename: "{app}\ZCinemaSound.App.exe"; Parameters: "--uninstall --quiet"; Flags: runhidden waituntilterminated

[Code]
function EqualizerApoInstalled(): Boolean;
var
  s: string;
begin
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\EqualizerAPO', 'InstallPath', s) and DirExists(s);
  if not Result then
    Result := RegQueryStringValue(HKLM, 'SOFTWARE\EqualizerAPO', 'ConfigPath', s) and DirExists(s);
end;

function EqualizerApoInstallPath(): string;
begin
  if not RegQueryStringValue(HKLM, 'SOFTWARE\EqualizerAPO', 'InstallPath', Result) then
    Result := '';
end;

function DeviceSelectorPath(): string;
var
  base: string;
begin
  base := EqualizerApoInstallPath();
  Result := '';
  if base = '' then Exit;
  if FileExists(AddBackslash(base) + 'DeviceSelector.exe') then
    Result := AddBackslash(base) + 'DeviceSelector.exe'
  else if FileExists(AddBackslash(base) + 'Configurator.exe') then
    Result := AddBackslash(base) + 'Configurator.exe';
end;

function HasDeviceSelector(): Boolean;
begin
  Result := DeviceSelectorPath() <> '';
end;

function InitializeSetup(): Boolean;
var
  r: Integer;
begin
  if EqualizerApoInstalled() then
  begin
    Result := True;
    Exit;
  end;

  ShellExec('open', 'https://sourceforge.net/projects/equalizerapo/', '', '', SW_SHOWNORMAL, ewNoWait, r);
  while not EqualizerApoInstalled() do
  begin
    r := MsgBox('Equalizer APO is required (GPLv2 - it is not bundled with this installer).' + #13#10 + #13#10 +
                'A browser was opened to its download page. Install Equalizer APO, then click Retry.' + #13#10 + #13#10 +
                'https://sourceforge.net/projects/equalizerapo/',
                mbError, MB_RETRYCANCEL);
    if r = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
  Result := True;
end;
