#define MyAppNameZh "CAD 云线批注 (CAD Auto CloudNote)"
#define MyAppNameEn "CadAutoCloudNote"
#define MyAppVersion "0.1.0"
#define MyPublisher "Mr.yang"
#define DllPrefix "CadAutoCloudNote"

; 自动检测编译环境输出目录
#ifexist "..\build\R19\CadAutoCloudNote_R19.dll"
  #define OutputPath "..\build"
#else
  #define OutputPath "..\build"
#endif

[Setup]
VersionInfoVersion={#MyAppVersion}
AppId={{8F3E2D7B-1A9C-4068-B572-941D60C75A23}
PrivilegesRequired=lowest

; 1. 界面与控制面板显示名称 (全中文)
AppName={#MyAppNameZh}
AppVerName={#MyAppNameZh} v{#MyAppVersion}
AppPublisher={#MyPublisher}

; 2. 安装包输出名称
OutputBaseFilename={#MyAppNameEn}_Setup_v{#MyAppVersion}
OutputDir=..\build\Installer

; 3. 底层安装路径 (保持英文，防止 CAD 读取中文路径时偶发乱码)
DefaultDirName={userappdata}\InkVerse\{#MyAppNameEn}

; 4. 开始菜单文件夹名称 (全中文)
DefaultGroupName={#MyAppNameZh}
DisableProgramGroupPage=no
AllowNoIcons=yes

DirExistsWarning=no
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=Icon.ico
UninstallDisplayIcon={app}\Icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
RestartIfNeededByRun=no

[Files]
; 1. 资源文件
Source: "Icon.ico"; DestDir: "{app}"; DestName: "Icon.ico"; Flags: ignoreversion
Source: "Readme.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "author_contact.png"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; 2. R17 ~ R27 世代专属版本目录
Source: "{#OutputPath}\R17\*"; DestDir: "{app}\R17"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R17'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R18\*"; DestDir: "{app}\R18"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R18'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R19\*"; DestDir: "{app}\R19"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R19'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R20\*"; DestDir: "{app}\R20"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R20'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R21\*"; DestDir: "{app}\R21"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R21'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R22\*"; DestDir: "{app}\R22"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R22'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R23\*"; DestDir: "{app}\R23"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R23'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R24\*"; DestDir: "{app}\R24"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R24'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R25\*"; DestDir: "{app}\R25"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R25'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R26\*"; DestDir: "{app}\R26"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R26'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "{#OutputPath}\R27\*"; DestDir: "{app}\R27"; Excludes: "*.pdb,*.xml"; Check: IsVersionChecked('R27'); Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: checkablealone

[Icons]
Name: "{group}\{#MyAppNameZh} - 使用说明"; Filename: "{app}\Readme.html"; IconFilename: "{app}\Icon.ico"; Comment: "查看如何使用 {#MyAppNameZh}"
Name: "{group}\{#MyAppNameZh} - 打开安装目录"; Filename: "{app}"; Comment: "打开 {#MyAppNameZh} 安装文件夹"
Name: "{group}\卸载 {#MyAppNameZh}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Icon.ico"; Comment: "将 {#MyAppNameZh} 从当前电脑移除"
Name: "{autoprograms}\{#MyAppNameZh}\{#MyAppNameZh} - 使用说明"; Filename: "{app}\Readme.html"; IconFilename: "{app}\Icon.ico"; Comment: "查看如何使用 {#MyAppNameZh}"
Name: "{autoprograms}\{#MyAppNameZh}\{#MyAppNameZh} - 打开安装目录"; Filename: "{app}"; Comment: "打开 {#MyAppNameZh} 安装文件夹"
Name: "{autoprograms}\{#MyAppNameZh}\卸载 {#MyAppNameZh}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Icon.ico"; Comment: "将 {#MyAppNameZh} 从当前电脑移除"
Name: "{autodesktop}\{#MyAppNameZh}"; Filename: "{app}\Readme.html"; IconFilename: "{app}\Icon.ico"; Comment: "{#MyAppNameZh} - AutoCAD 智能云线与批注工具"; Tasks: desktopicon

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Run]
Filename: "{app}\Readme.html"; Description: "立即查看使用说明 (图文手册)"; Flags: postinstall shellexec skipifsilent

[Code]
var
  AcadVersionPage: TInputOptionWizardPage;
  FoundRNames: TStringList;
  G_DeleteUserData: Boolean;

// ==========================================
// 进程防护模块
// ==========================================
function IsAppRunning(const FileName: string): Boolean;
var
  WbemLocator, WbemService, WbemObjectSet: Variant;
begin
  Result := False;
  try
    WbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    WbemService := WbemLocator.ConnectServer('', 'root\CIMV2', '', '');
    WbemObjectSet := WbemService.ExecQuery('SELECT Name FROM Win32_Process WHERE Name="' + FileName + '"');
    Result := not VarIsNull(WbemObjectSet) and (WbemObjectSet.Count > 0);
  except
  end;
end;

procedure ForceKillCAD();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM acad.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM accoreconsole.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000); 
end;

// ==========================================
// 路径列表处理工具
// ==========================================
function AddToPathList(const PathList, NewPath: string): string;
var
  SearchStr, Target: string;
begin
  SearchStr := ';' + Lowercase(Trim(PathList)) + ';';
  Target := ';' + Lowercase(Trim(NewPath)) + ';';
  if Pos(Target, SearchStr) = 0 then
  begin
    if Trim(PathList) = '' then Result := Trim(NewPath)
    else Result := Trim(PathList) + ';' + Trim(NewPath);
  end else Result := PathList;
end;

function RemoveFromPathList(const PathList, TargetPath: string): string;
var
  P: Integer;
  TempList, CurrentPath, Res, TargetL: string;
begin
  TempList := PathList + ';';
  Res := '';
  TargetL := Lowercase(Trim(TargetPath));
  P := Pos(';', TempList);
  while P > 0 do
  begin
    CurrentPath := Trim(Copy(TempList, 1, P - 1));
    if (CurrentPath <> '') and (Lowercase(CurrentPath) <> TargetL) then
    begin
      if Res = '' then Res := CurrentPath
      else Res := Res + ';' + CurrentPath;
    end;
    Delete(TempList, 1, P);
    P := Pos(';', TempList);
  end;
  Result := Res;
end;

// ==========================================
// 版本映射归一化函数
// ==========================================
function GetBaseRFromRName(const RName: string; out DisplayName: string): string;
begin
  Result := '';
  DisplayName := '';
  if Pos('R17', RName) > 0 then begin DisplayName := 'AutoCAD 2007-2009'; Result := 'R17'; end
  else if Pos('R18', RName) > 0 then begin DisplayName := 'AutoCAD 2010-2012'; Result := 'R18'; end
  else if Pos('R19', RName) > 0 then begin DisplayName := 'AutoCAD 2013-2014'; Result := 'R19'; end
  else if Pos('R20', RName) > 0 then begin DisplayName := 'AutoCAD 2015-2016'; Result := 'R20'; end
  else if Pos('R21', RName) > 0 then begin DisplayName := 'AutoCAD 2017'; Result := 'R21'; end
  else if Pos('R22', RName) > 0 then begin DisplayName := 'AutoCAD 2018'; Result := 'R22'; end
  else if Pos('R23', RName) > 0 then begin DisplayName := 'AutoCAD 2019-2020'; Result := 'R23'; end
  else if Pos('R24', RName) > 0 then begin DisplayName := 'AutoCAD 2021-2024'; Result := 'R24'; end
  else if Pos('R25.0', RName) > 0 then begin DisplayName := 'AutoCAD 2025'; Result := 'R25'; end
  else if Pos('R25.1', RName) > 0 then begin DisplayName := 'AutoCAD 2026'; Result := 'R26'; end
  else if Pos('R25', RName) > 0 then begin DisplayName := 'AutoCAD 2025'; Result := 'R25'; end
  else if Pos('R26.0', RName) > 0 then begin DisplayName := 'AutoCAD 2027'; Result := 'R27'; end
  else if Pos('R26', RName) > 0 then begin DisplayName := 'AutoCAD 2026'; Result := 'R26'; end
  else if Pos('R27', RName) > 0 then begin DisplayName := 'AutoCAD 2027'; Result := 'R27'; end;
end;

// ==========================================
// CAD 单实例注册/反注册公共过程
// ==========================================
procedure ConfigureAcadProfile(const ProfileKey, VersionPath, RootTrustedFormat, TrustedPathFormat: string; IsInstall: Boolean);
var
  CurrentPath, NewPath: string;
begin
  // 1. 注册支持搜索路径 (ACAD)
  if RegQueryStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'ACAD', CurrentPath) then
  begin
    if IsInstall then
      NewPath := AddToPathList(CurrentPath, VersionPath)
    else
      NewPath := RemoveFromPathList(CurrentPath, VersionPath);
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'ACAD', NewPath);
  end;

  // 2. 注册受信任路径 (TRUSTEDPATHS) - 同时写入 \Variables 和 \General 节点
  if RegQueryStringValue(HKEY_CURRENT_USER, ProfileKey + '\Variables', 'TRUSTEDPATHS', CurrentPath) then
  begin
    if IsInstall then
    begin
      NewPath := AddToPathList(CurrentPath, RootTrustedFormat);
      NewPath := AddToPathList(NewPath, TrustedPathFormat);
    end else begin
      NewPath := RemoveFromPathList(CurrentPath, RootTrustedFormat);
      NewPath := RemoveFromPathList(NewPath, TrustedPathFormat);
    end;
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\Variables', 'TRUSTEDPATHS', NewPath);
  end else if IsInstall then begin
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\Variables', 'TRUSTEDPATHS', RootTrustedFormat + ';' + TrustedPathFormat);
  end;

  if RegQueryStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'TRUSTEDPATHS', CurrentPath) then
  begin
    if IsInstall then
    begin
      NewPath := AddToPathList(CurrentPath, RootTrustedFormat);
      NewPath := AddToPathList(NewPath, TrustedPathFormat);
    end else begin
      NewPath := RemoveFromPathList(CurrentPath, RootTrustedFormat);
      NewPath := RemoveFromPathList(NewPath, TrustedPathFormat);
    end;
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'TRUSTEDPATHS', NewPath);
  end else if IsInstall then begin
    RegWriteStringValue(HKEY_CURRENT_USER, ProfileKey + '\General', 'TRUSTEDPATHS', RootTrustedFormat + ';' + TrustedPathFormat);
  end;
end;

procedure ProcessAcadInstance(const RKey, InstName, BaseR, InstallRoot: string; IsInstall: Boolean);
var
  AppKey, AcadInstKey, ProfileKey: string;
  VersionPath, RootTrustedFormat, TrustedPathFormat, DllPath: string;
  ProfileNames: TArrayOfString;
  k: Integer;
begin
  VersionPath := InstallRoot + '\' + BaseR;
  RootTrustedFormat := InstallRoot + '\...';
  TrustedPathFormat := VersionPath + '\...';
  
  DllPath := VersionPath + '\{#DllPrefix}_' + BaseR + '.dll';
  if not FileExists(DllPath) then
    DllPath := VersionPath + '\{#DllPrefix}.dll';
    
  AppKey := RKey + '\' + InstName + '\Applications\{#MyAppNameEn}';

  if IsInstall then
  begin
    if FileExists(DllPath) then
    begin
      RegWriteDWordValue(HKEY_CURRENT_USER, AppKey, 'LOADCTRLS', 2);
      RegWriteDWordValue(HKEY_CURRENT_USER, AppKey, 'MANAGED', 1);
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey, 'LOADER', DllPath);
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey, 'DESCRIPTION', '{#MyAppNameZh}');
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey + '\Commands', 'CN', 'CN');
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey + '\Commands', 'CNQ', 'CNQ');
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey + '\Commands', 'CNP', 'CNP');
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey + '\Commands', 'NOTEPANEL', 'NOTEPANEL');
      RegWriteStringValue(HKEY_CURRENT_USER, AppKey + '\Commands', '云线批注看板', '云线批注看板');
    end;
  end else begin
    if RegKeyExists(HKEY_CURRENT_USER, AppKey) then
      RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, AppKey);
  end;

  AcadInstKey := RKey + '\' + InstName + '\Profiles';
  if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadInstKey, ProfileNames) then
  begin
    for k := 0 to GetArrayLength(ProfileNames) - 1 do
    begin
      ProfileKey := AcadInstKey + '\' + ProfileNames[k];
      ConfigureAcadProfile(ProfileKey, VersionPath, RootTrustedFormat, TrustedPathFormat, IsInstall);
    end;
  end;
end;

// ==========================================
// 阶段1：安装前置检查
// ==========================================
function InitializeSetup(): Boolean;
var
  AcadKey: string;
begin
  Result := True;
  AcadKey := 'Software\Autodesk\AutoCAD';
  
  if not RegKeyExists(HKEY_CURRENT_USER, AcadKey) then
  begin
    MsgBox('当前用户未运行过 AutoCAD。请先启动一次 AutoCAD 后，再安装 {#MyAppNameZh}。', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  while IsAppRunning('acad.exe') or IsAppRunning('accoreconsole.exe') do
  begin
    case MsgBox('检测到 [ AutoCAD ] 正在运行。为确保底层依赖注册成功，请关闭 CAD。' + #13#10#13#10 +
              '  【确定】(是) ：我已保存图纸并退出 CAD，继续。' + #13#10 +
              '  【强制关闭】(否) ：我不保留图纸，直接强制结束 CAD。' + #13#10 +
              '  【取消】  ：退出安装。', mbConfirmation, MB_YESNOCANCEL) of
      IDYES: begin end;
      IDNO:
        begin
          ForceKillCAD();
          if not (IsAppRunning('acad.exe') or IsAppRunning('accoreconsole.exe')) then Break;
        end;
      IDCANCEL:
        begin
          Result := False;
          Exit;
        end;
    end;
  end;
end;

// ==========================================
// 阶段2：向导初始化 (CAD 2007~2027 嗅探)
// ==========================================
procedure InitializeWizard;
var
  AcadKey, RKey, RName, DisplayName, BaseR: string;
  RNames, InstNames: TArrayOfString;
  i: Integer;
begin
  FoundRNames := TStringList.Create;
  AcadVersionPage := CreateInputOptionPage(wpSelectDir,
    '选择挂载版本', '检测到以下兼容的 AutoCAD 版本', '请勾选需要挂载【{#MyAppNameZh}】插件的版本：', False, False);

  AcadKey := 'Software\Autodesk\AutoCAD';
  if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadKey, RNames) then
  begin
    for i := 0 to GetArrayLength(RNames) - 1 do
    begin
      RName := RNames[i];
      BaseR := GetBaseRFromRName(RName, DisplayName);

      if DisplayName <> '' then
      begin
        RKey := AcadKey + '\' + RName;
        if RegGetSubkeyNames(HKEY_CURRENT_USER, RKey, InstNames) then
        begin
          if GetArrayLength(InstNames) > 0 then
          begin
            AcadVersionPage.Add(DisplayName + ' [' + RName + ']');
            AcadVersionPage.Values[AcadVersionPage.CheckListBox.Items.Count - 1] := True;
            FoundRNames.Add(RName + '|' + BaseR);
          end;
        end;
      end;
    end;
  end;

  if AcadVersionPage.CheckListBox.Items.Count = 0 then
  begin
     AcadVersionPage.Add('未检测到兼容版本 (2007-2027)');
     AcadVersionPage.CheckListBox.ItemEnabled[0] := False;
  end;
end;

function IsVersionChecked(TargetR: string): Boolean;
var
  i: Integer;
begin
  Result := False;
  if Assigned(FoundRNames) then
    for i := 0 to AcadVersionPage.CheckListBox.Items.Count - 1 do
      if AcadVersionPage.Values[i] and (Pos('|' + TargetR, FoundRNames[i]) > 0) then
      begin
        Result := True;
        Exit;
      end;
end;

// ==========================================
// 阶段3：写入加载项、依赖路径锚点
// ==========================================
procedure CurStepChanged(CurStep: TSetupStep);
var
  AcadKey, RKey, ItemData, RName, BaseR, InstallRoot: string;
  InstNames: TArrayOfString;
  i, j: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    InstallRoot := ExpandConstant('{app}');
    AcadKey := 'Software\Autodesk\AutoCAD';
    RegWriteStringValue(HKEY_CURRENT_USER, 'Software\{#MyAppNameEn}', 'InstallDir', InstallRoot);

    for i := 0 to AcadVersionPage.CheckListBox.Items.Count - 1 do
    begin
      if AcadVersionPage.Values[i] then
      begin
        ItemData := FoundRNames[i];
        RName := Copy(ItemData, 1, Pos('|', ItemData) - 1);
        BaseR := Copy(ItemData, Pos('|', ItemData) + 1, Length(ItemData));
        RKey := AcadKey + '\' + RName;

        if RegGetSubkeyNames(HKEY_CURRENT_USER, RKey, InstNames) then
          for j := 0 to GetArrayLength(InstNames) - 1 do
            ProcessAcadInstance(RKey, InstNames[j], BaseR, InstallRoot, True);
      end;
    end;
  end;
end;

// ==========================================
// 阶段4：卸载清理过程
// ==========================================
function InitializeUninstall(): Boolean;
begin
  Result := True; G_DeleteUserData := False;
  if UninstallSilent then Exit;

  while IsAppRunning('acad.exe') or IsAppRunning('accoreconsole.exe') do
  begin
    case MsgBox('检测到 [ AutoCAD ] 正在运行。为确保文件清除完整，请先关闭 CAD。' + #13#10#13#10 +
              '  【确定】(是) ：我已保存图纸并退出 CAD，继续。' + #13#10 +
              '  【强制关闭】(否) ：直接强制结束 CAD 进程（未保存的数据将丢失）。' + #13#10 +
              '  【取消】  ：放弃并退出卸载。', mbConfirmation, MB_YESNOCANCEL) of
      IDYES: begin end;
      IDNO:
        begin
          ForceKillCAD();
          if not (IsAppRunning('acad.exe') or IsAppRunning('accoreconsole.exe')) then Break;
        end;
      IDCANCEL: begin Result := False; Exit; end;
    end;
  end;

  if MsgBox('即将卸载 {#MyAppNameZh}。是否清除用户配置数据？', mbConfirmation, MB_YESNO) = IDYES then
    G_DeleteUserData := True;
end;

procedure SafeRemoveEmptyDir(const DirPath: string);
begin
  if DirExists(DirPath) then
    RemoveDir(DirPath);
end;

procedure CleanupInstalledFolders(const InstallRoot: string);
var
  i: Integer;
  BaseR, RDir: string;
begin
  for i := 17 to 27 do
  begin
    BaseR := 'R' + IntToStr(i);
    RDir := InstallRoot + '\' + BaseR;
    SafeRemoveEmptyDir(RDir);
  end;

  SafeRemoveEmptyDir(InstallRoot);
  // 若上级套件品牌目录 InkVerse 为空，则安全清理
  SafeRemoveEmptyDir(ExtractFilePath(RemoveBackslash(InstallRoot)));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AcadKey, RKey, InstallRoot, BaseR, DummyName: string;
  RNames, InstNames: TArrayOfString;
  i, j: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    if G_DeleteUserData then
      RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\{#MyAppNameEn}');
    InstallRoot := ExpandConstant('{app}');
    AcadKey := 'Software\Autodesk\AutoCAD';

    if RegGetSubkeyNames(HKEY_CURRENT_USER, AcadKey, RNames) then
    begin
      for i := 0 to GetArrayLength(RNames) - 1 do
      begin
        BaseR := GetBaseRFromRName(RNames[i], DummyName);
        if BaseR <> '' then
        begin
          RKey := AcadKey + '\' + RNames[i];
          if RegGetSubkeyNames(HKEY_CURRENT_USER, RKey, InstNames) then
            for j := 0 to GetArrayLength(InstNames) - 1 do
              ProcessAcadInstance(RKey, InstNames[j], BaseR, InstallRoot, False);
        end;
      end;
    end;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    InstallRoot := ExpandConstant('{app}');
    if G_DeleteUserData then
    begin
      DeleteFile(InstallRoot + '\CloudNoteConfig.json');
      DeleteFile(InstallRoot + '\Config.json');
    end;

    CleanupInstalledFolders(InstallRoot);
  end;
end;
