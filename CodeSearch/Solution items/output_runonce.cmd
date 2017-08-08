echo off
set _solutiondir=%1
echo solutiondir is %_solutiondir%

pushd %_solutiondir%

del /f/s/q output\*.*
git clean -q -f

robocopy ".\solution items" output nlog.config /r:0  /is
robocopy ".\solution items" output install.cmd /r:0  /is
robocopy ".\solution items" output eula.rtf /r:0  /is

robocopy Dependencies\ctags58\ output\bin ctags.exe /xf *.log  /r:0  /is 
robocopy Dependencies\jetty-runner-9.3.9.v20160517  output\lib  /xf *.log  /r:0  /is 
robocopy Dependencies\jre1.8.0_91\bin  output\bin /xf *.log  /r:0  /is /s 
robocopy Dependencies\jre1.8.0_91\lib  output\lib  /r:0  /is /s 
robocopy Dependencies\opengrok\org output configuration.xml  /r:0  /is 
robocopy Dependencies\opengrok\org\plugins output\lib\plugins   /r:0  /is 
robocopy Dependencies\opengrok\org\lib output\lib /r:0  /is /s 
robocopy Dependencies\opengrok\org\lib\lib output\lib opengrok.jar /r:0  /is /s 
robocopy Dependencies\opengrok\org\lib\lib output\lib\source\web-inf\lib  /r:0  /is /s 
robocopy Dependencies\opengrok\overwrites output configuration.xml  /r:0  /is 
robocopy Dependencies\opengrok\overwrites\lib  output\lib   /r:0  /is /s 
 

popd 
exit 0
 