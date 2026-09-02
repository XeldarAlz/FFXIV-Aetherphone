@echo off
setlocal
set "root=%~dp0..\.."
set "dll=%root%\src\Aetherphone.Harness\bin\Release\net10.0-windows\Aetherphone.Harness.dll"
if not exist "%dll%" (
  echo Harness not built. Run: dotnet build src\Aetherphone.Harness -c Release 1>&2
  exit /b 1
)
dotnet "%dll%" %*
