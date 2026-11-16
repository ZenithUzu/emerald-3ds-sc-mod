@echo off
setlocal
if "%~1"=="" (
    echo Uso: build.cmd RUTA_A_MAKEROM.EXE [RUTA_DE_SALIDA.EXE]
    exit /b 2
)
set "result=%~2"
if not defined result set "result=%CD%\Emerald3DS-Azahar.exe"
"%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 ^
  "/out:%result%" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
  "/resource:%~f1,makerom.exe" ^
  "/resource:%~dp0makerom-LICENSE.txt,makerom-LICENSE.txt" ^
  "/resource:%~dp0..\..\3ds_port\emerald3ds-azahar.rsf,azahar.rsf" ^
  "/resource:%~dp0..\..\3ds_port\mods\una-pantalla.ini,mod.ini" ^
  "%~dp0EmeraldAzaharBuilder.cs"
exit /b %ERRORLEVEL%
