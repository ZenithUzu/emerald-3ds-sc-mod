@echo off
setlocal
if "%~1"=="" (
    echo Uso: build.cmd MAKEROM.EXE PROFILE.ZIP [OUTPUT.EXE]
    exit /b 2
)
if "%~2"=="" exit /b 2
set "result=%~3"
if not defined result set "result=%CD%\Emerald3DS-Azahar.exe"
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 ^
  "/out:%result%" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ^
  "/resource:%~f1,makerom.exe" ^
  "/resource:%~dp0makerom-LICENSE.txt,makerom-LICENSE.txt" ^
  "/resource:%~dp0..\..\3ds_port\emerald3ds-azahar.rsf,azahar.rsf" ^
  "/resource:%~dp0..\..\3ds_port\mods\una-pantalla.ini,mod.ini" ^
  "/resource:%~f2,official-es.zip" ^
  "%~dp0EmeraldAzaharBuilder.cs" "%~dp0OfficialFolder.cs"
exit /b %ERRORLEVEL%
