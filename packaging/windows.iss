#ifndef AppVersion
  #define AppVersion "0.9.1"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif
#ifndef LicenseDir
  #define LicenseDir "..\release\licenses"
#endif

[Setup]
#ifdef InstallerSmokeTest
AppId={{D5680EBB-0CE3-457B-8F93-A16F10A6BCE6}
AppName=Samsung Tizen Brightness Installer Test
OutputBaseFilename=Samsung.Tizen.Brightness.Setup.Smoke
#else
AppId={{12ED6BCA-B948-4AEC-B671-56195B919C32}
AppName=Samsung Tizen Brightness
AppMutex=Local\M70BBrightness.SingleInstance
OutputBaseFilename=Samsung.Tizen.Brightness.Setup.{#AppVersion}
#endif
AppVersion={#AppVersion}
AppPublisher=dttutty
AppPublisherURL=https://github.com/dttutty/samsung-tizen-brightness
AppSupportURL=https://github.com/dttutty/samsung-tizen-brightness/issues
AppUpdatesURL=https://github.com/dttutty/samsung-tizen-brightness/releases/latest
DefaultDirName={localappdata}\Programs\SamsungTizenBrightness
DefaultGroupName=Samsung Tizen Brightness
UninstallDisplayIcon={app}\Samsung.Tizen.Brightness.exe
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
SetupIconFile=..\src\SamsungTizenBrightness\assets\samsung-tizen-tv.ico
OutputDir={#OutputDir}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "{#PublishDir}\Samsung Tizen 亮度.exe"; DestDir: "{app}"; DestName: "Samsung.Tizen.Brightness.exe"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.zh-CN.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.ko.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.es.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\screenshots\brightness-flyout.*.png"; DestDir: "{app}\docs\screenshots"; Flags: ignoreversion
Source: "{#LicenseDir}\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Samsung Tizen Brightness"; Filename: "{app}\Samsung.Tizen.Brightness.exe"
Name: "{group}\{cm:UninstallProgram,Samsung Tizen Brightness}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\Samsung.Tizen.Brightness.exe"; Parameters: "--open"; Description: "{cm:LaunchProgram,Samsung Tizen Brightness}"; Flags: nowait postinstall skipifsilent

#ifndef InstallerSmokeTest
[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""Samsung Tizen Brightness"" /F"; Flags: runhidden; RunOnceId: "SamsungBrightnessRemoveLoginTask"
#endif
