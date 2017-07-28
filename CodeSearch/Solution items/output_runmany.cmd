echo off
set _outdir=%1
set _solutiondir=%2
set _targetname=%3


echo outdir is %_outdir%
echo solutiondir is %_solutiondir%
echo projectname is %_targetname%

pushd %_solutiondir%

robocopy %_targetname%\%_outdir% output /xf *.log  /r:0  /is /xo

popd
exit 0
 