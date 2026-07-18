; FileLock インストーラースクリプト (Inno Setup 6)
; ビルド時に /DMyAppVersion=x.x.x を指定してバージョンを反映する
; 例: ISCC.exe /DMyAppVersion=1.0.0 setup.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#define MyAppName "FileLock"
#define MyAppExeName "FileLock.exe"
#define MyAppPublisher "FileLock"

[Setup]
AppId={{B6E1B6D2-6F1E-4C7A-9B0A-FILELOCKAPP1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={userpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=FileLock-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
; 家族向けの簡易ロック用途のため管理者権限は不要。ユーザーごと(自分のアカウントのみ)にインストールする。
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64
SetupLogging=yes

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Files]
; dotnet publish の自己完結・単一exe出力を同梱
Source: "..\publish\FileLock.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "デスクトップにアイコンを作成する"; GroupDescription: "追加のアイコン:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} を起動する"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; アンインストール開始前に、パスワードなしで全ファイルのロックを解除する。
; 「パスワードを忘れても困らないように」という要件に対応（アンインストール自体にもパスワードは不要）。
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unlock-all"; Flags: runhidden waituntilterminated; RunOnceId: "UnlockAllBeforeUninstall"
