#include "services.iss"

[Setup]
AppName=Codesearch
AppVersion=0.9
AllowRootDirectory=Yes
DefaultDirName={pf}\Codesearch
DefaultGroupName=Codesearch
SourceDir=..\output
OutputDir=..\installation\
OutputBaseFilename=Codesearch
UninstallDisplayIcon={app}\Indexer.exe
AppPublisher=Codesearch
AppVerName=Codesearch 1.0
       
[Files]
Source: "*.*"; DestDir: "{app}"; Excludes: "*.log, *.pdb, *.xml, *.ini"; Flags: recursesubdirs replacesameversion onlyifdoesntexist
Source: "*.xml"; DestDir: "{app}"; Flags: recursesubdirs onlyifdoesntexist
Source: "*.ini"; DestDir: "{app}"; Flags: recursesubdirs onlyifdoesntexist

 
[Run]
Filename: "{app}\indexer.exe"; Parameters: "uninstall"; Flags: runhidden
Filename: "{app}\webhost.exe"; Parameters: "uninstall"; Flags: runhidden
Filename: "{app}\indexer.exe"; Parameters: "install -username:{code:GetUser|Username} -password:{code:GetUser|Password}"; Flags: runhidden
Filename: "{app}\webhost.exe"; Parameters: "install -username:{code:GetUser|Username} -password:{code:GetUser|Password}"; Flags: runhidden
Filename: "{app}\indexer.exe"; Parameters: "start"; Flags: runhidden
Filename: "{app}\webhost.exe"; Parameters: "start"; Flags: runhidden

[Uninstallrun]
Filename: "{app}\indexer.exe"; Parameters: "uninstall"; Flags: runhidden
Filename: "{app}\webhost.exe"; Parameters: "uninstall"; Flags: runhidden
   
[Code]
#ifdef UNICODE
  #define AW "W"
#else
  #define AW "A"
#endif
const  
  LOGON32_LOGON_INTERACTIVE = 2;
  LOGON32_LOGON_NETWORK = 3;
  LOGON32_LOGON_BATCH = 4;
  LOGON32_LOGON_SERVICE = 5;
  LOGON32_LOGON_UNLOCK = 7;
  LOGON32_LOGON_NETWORK_CLEARTEXT = 8;
  LOGON32_LOGON_NEW_CREDENTIALS = 9;

  LOGON32_PROVIDER_DEFAULT = 0;
  LOGON32_PROVIDER_WINNT40 = 2;
  LOGON32_PROVIDER_WINNT50 = 3;

  ERROR_SUCCESS = 0;
  ERROR_LOGON_FAILURE = 1326;

function LogonUser(lpszUsername, lpszDomain, lpszPassword: string;
  dwLogonType, dwLogonProvider: DWORD; var phToken: THandle): BOOL;
  external 'LogonUser{#AW}@advapi32.dll stdcall';

var
  ServerDetailsPage: TInputQueryWizardPage;

function TryLogonUser(const Domain, UserName, Password: string; 
  var ErrorCode: Longint): Boolean;
var
  Token: THandle;
begin
  Result := LogonUser(UserName, Domain, Password, LOGON32_LOGON_INTERACTIVE,
    LOGON32_PROVIDER_DEFAULT, Token);
  ErrorCode := DLLGetLastError;
end;

procedure ParseDomainUserName(const Value: string; var Domain,
  UserName: string);
var
  DelimPos: Integer;
begin
  DelimPos := Pos('\', Value);
  if DelimPos = 0 then
  begin
    Domain := '.';
    UserName := Value;
  end
  else
  begin
    Domain := Copy(Value, 1, DelimPos - 1);
    UserName := Copy(Value, DelimPos + 1, MaxInt);
  end;
end;

var
  ServiceAccountPage: TInputQueryWizardPage;
  TFSAccountPage: TInputQueryWizardPage;
  TFSServerPage: TInputQueryWizardPage;
  InfoPage: TOutputMsgWizardPage;
 
procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  Confirm:= False;
  Cancel:= True;
end;

function NextButtonClick(PageId: Integer): Boolean;
var
  Domain: string;
  UserName: string;
  Password: string;
  ErrorCode: Longint;
begin
    Result := True;
    if PageId=102 then
    begin
        ParseDomainUserName(ServiceAccountPage.Values[0], Domain, UserName);
        Password := ServiceAccountPage.Values[1];
        TryLogonUser(Domain, UserName, Password, ErrorCode);
        case ErrorCode of
          ERROR_SUCCESS:
          begin
          MsgBox('Successfully validated account credentials', mbInformation, MB_OK);
          Result := True;
          end;
          ERROR_LOGON_FAILURE:
          begin
          MsgBox('The user name or password is incorrect', mbError, MB_OK);
          Result := False;
          end;
        else
          begin
          MsgBox('Login failed!' + #13#10 + SysErrorMessage(DLLGetLastError),
          mbError, MB_OK);
          Result := False;
          end;
      end;
    end;
end;


Procedure CurStepChanged(CurStep: TSetupStep);
var
Param : String;
ResultCode : Integer;
UpdaterServiceRes : Boolean;
WebhostServiceRes : Boolean;
UpdaterServiceName : String;
WebhostServiceName : String;
UpdaterServiceStatus : Longword;
WebhostServiceStatus : Longword;
Dir : String;
TmpDir : String;
IniFile : String;
AbortNow : Boolean;

begin 
   

    {remember: the services get uninstaller and installed again in the Run section higher up}
    if CurStep=ssPostInstall then 
    begin 
          Log('CurStepChanged ssPostInstall');
          Log('Writing ini settings');
          IniFile := ExpandConstant('{app}'+ '\settings.ini');
          Log('Writing settings to ' + IniFile);
          if IniKeyExists('tfs','Username', IniFile) = true then
          begin
            DeleteIniEntry('tfs', 'Username', IniFile);
          end;       
          SetIniString('tfs','Username', ExpandConstant('{code:GetUserTfs|Username}'), IniFile); 
          if IniKeyExists('tfs','Password', IniFile) = true then
          begin
            DeleteIniEntry('tfs', 'Password', IniFile);
          end;
          SetIniString('tfs','Password', ExpandConstant('{code:GetUserTfs|Password}'), IniFile); 
          if IniKeyExists('tfs','ServerUrl', IniFile) = true then
          begin
            DeleteIniEntry('tfs', 'ServerUrl', IniFile);
          end;
          SetIniString('tfs','ServerUrl', ExpandConstant('{code:GetTfsServer|ServerUrl}'), IniFile); 
    end;       
end; 
  
procedure InitializeWizard;
begin
   Log('InitializeWizard called');
   TFSAccountPage := CreateInputQueryPage(wpSelectDir,
    'Team Foundation Server Credentials', 'The TFS account to use',
    'Please enter user name and password.');
  TFSAccountPage.Add('User name:', False);
  TFSAccountPage.Add('Password:', True);
  TFSAccountPage.Values[0] := 'BuildTFSSpider'
  TFSAccountPage.Values[1] := 'hj7hqzqzATS2vN8WBG3q'
 
  TFSServerPage := CreateInputQueryPage(wpSelectDir,
    'Team Foundation Server', 'TFS server',
    'Please enter a valid TFS server URL.');
  TFSServerPage.Add('TFS Server:', False);
  TFSServerPage.Values[0] := 'http://tfsserver:8080/tfs'

  ServiceAccountPage := CreateInputQueryPage(wpSelectDir,
    'Service Credentials', 'The Windows service account to use',
    'Please enter domain user name and password.');
  ServiceAccountPage.Add('Domain user name (domain\user):', False);
  ServiceAccountPage.Add('Password:', True);

  InfoPage := CreateOutputMsgPage(WpInfoBefore, 'Important information', 'Please read the following information before continuing', 
  'CodeSearch will store code and index data on your computer in the same place that the application is installed. This data will have to be removed manually after uninstallation.');

end;

function GetUser(Param: String): String;
begin
  Log('GetUser called: ' + Param);
  if Param = 'Username' then
    Result := ServiceAccountPage.Values[0]
  else if Param = 'Password' then
    Result := ServiceAccountPage.Values[1];
end;

function GetUserTfs(Param: String): String;
begin
  Log('GetUserTfs called: ' + Param);
  if Param = 'Username' then
    Result := TFSAccountPage.Values[0]
  else if Param = 'Password' then
    Result := TFSAccountPage.Values[1];
end;

function GetTfsServer(Param: String): String;
begin
  Log('GetTfsServer called: ' + Param);
  if Param = 'ServerUrl' then
    Result := TFSServerPage.Values[0]
end;

// Exec with output stored in result.
// ResultString will only be altered if True is returned.
function ExecWithResult(const Filename, Params, WorkingDir: String; const ShowCmd: Integer; const Wait: TExecWait; var ResultCode: Integer; var ResultString: AnsiString): Boolean;
var
  TempFilename: String;
  Command: String;
begin
  TempFilename := ExpandConstant('{tmp}\~execwithresult.txt');
  // Exec via cmd and redirect output to file. Must use special string-behavior to work.
  Command := Format('"%s" /S /C ""%s" %s > "%s""', [ExpandConstant('{cmd}'), Filename, Params, TempFilename]);
  Result := Exec(ExpandConstant('{cmd}'), Command, WorkingDir, ShowCmd, Wait, ResultCode);
  if not Result then
    Exit;
  LoadStringFromFile(TempFilename, ResultString);  // Cannot fail
  DeleteFile(TempFilename);
  // Remove new-line at the end
  if (Length(ResultString) >= 2) and (ResultString[Length(ResultString) - 1] = #13) and (ResultString[Length(ResultString)] = #10) then
    Delete(ResultString, Length(ResultString) - 1, 2);
end;

function InstallServices(): Boolean;
var
  ServiceName: String;
  ResultCode: Integer;
  ExecStdOut: AnsiString;
begin
  ServiceName := ExpandConstant('{app}\indexer.exe');
  Result := ExecWithResult(ServiceName, '', '', SW_HIDE, ewWaitUntilTerminated, ResultCode, ExecStdOut);
end;



