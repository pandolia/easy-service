@echo off
powershell -c [Environment]::SetEnvironmentVariable('Path',[Environment]::GetEnvironmentVariable('Path','Machine')+';%~dp0','Machine')
echo register "%~dp0" to system path
pause