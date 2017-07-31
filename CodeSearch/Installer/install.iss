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
Filename: "{app}\indexer.exe"; Parameters: "install -username:{code:GetUser|Username} -password:{code:GetUser|Password}"; Flags: runhidden
Filename: "{app}\webhost.exe"; Parameters: "install -username:{code:GetUser|Username} -password:{code:GetUser|Password}"; Flags: runhidden
Filename: "{app}\indexer.exe"; Parameters: "start"; Flags: runhidden
Filename: "{app}\webhost.exe"; Parameters: "start"; Flags: runhidden

[Uninstallrun]
Filename: "{app}\indexer.exe"; Parameters: "uninstall"; Flags: runhidden
Filename: "{app}\webhost.exe"; Parameters: "uninstall"; Flags: runhidden
   
[Code]
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
    UpdaterServiceName := 'Indexer';
    WebhostServiceName := 'Webhost';
    UpdaterServiceRes := ServiceExists(UpdaterServiceName);
    WebhostServiceRes := ServiceExists(WebhostServiceName);
          
    if CurStep=ssInstall then
    begin
      if UpdaterServiceRes=True then
      begin
        SimpleStopService(UpdaterServiceName, True,True);
      end;
      if WebhostServiceRes=True then
      begin
        SimpleStopService(WebhostServiceName, True, True);
      end;
    end;

    if CurStep=ssPostInstall then 
    begin 
          Log('CurStepChanged ssPostInstall');
          UpdaterServiceStatus := SimpleQueryService(UpdaterServiceName);
          WebhostServiceStatus := SimpleQueryService(WebhostServiceName);
          AbortNow := False;
          if ((UpdaterServiceRes=True) and (UpdaterServiceStatus<>SERVICE_RUNNING)) or ((WebhostServiceRes=True) and (WebhostServiceStatus<>SERVICE_RUNNING)) then
          begin
              if MsgBox('Services could not be started. Please check service account credentials. Do you want to abort installation and remove services ?', mbConfirmation, MB_YESNO) = IDYES then
              begin
                  AbortNow := True;
              end;
          end;
          {check if the services are installed ok. If not, clean up and leave}
          if UpdaterServiceRes=False or WebhostServiceRes=False or AbortNow=True then
          begin
            if UpdaterServiceRes=True then
            begin
               SimpleDeleteService(UpdaterServiceName);
               Log('Deleting ' + UpdaterServiceName);
            end
            else
            begin
               Log(UpdaterServiceName + ' does not exist');
            end;
            if WebhostServiceRes=True then
            begin
               SimpleDeleteService(WebhostServiceName);
               Log('Deleting ' + WebhostServiceName);
            end
            else
            begin
              Log(WebhostServiceName + ' does not exist');
            end;
            Dir := ExpandConstant('{app}');
            TmpDir := ExpandConstant('{tmp}');
            Log('expanded installation dir : ' + Dir);
            Log('expanded temp dir : ' + TmpDir);
            DelTree(TmpDir, True, True, True);
            if DelTree(Dir, True, True, True)=False then
            begin
               MsgBox('Could not delete installation directory: ' + Dir, mbCriticalError, MB_OK); 
            end
            else if AbortNow=False then
            begin
               MsgBox('Could not register services' , mbCriticalError, MB_OK);
            end;
            InfoPage := nil
            WizardForm.Close();
            Exit;
          end;
          {write relevant stuff to the ini file}
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
    'Please enter user name and password.');
  ServiceAccountPage.Add('User name:', False);
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



