@echo off
setlocal
for /f "delims=" %%P in ('git --exec-path') do set "gitcore=%%P"
set "bash=%gitcore%\..\..\..\bin\bash.exe"
if not exist "%bash%" (
  echo Git Bash not found next to git. Install Git for Windows, or run tools/guards/check from Git Bash. 1>&2
  exit /b 1
)
"%bash%" "%~dp0check" %*
