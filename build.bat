@echo off
rem Builds Hoard.exe with the C# compiler that ships with Windows (.NET Framework 4). Nothing to install.
rem The libraries in lib\ and the window's page in ui\ go inside the exe as resources: one file to download.
cd /d "%~dp0"
setlocal enabledelayedexpansion
set LIBS=
for %%f in (lib\*.dll) do set LIBS=!LIBS! /resource:%%f,%%~nxf
for %%f in (ui\*.html ui\*.css ui\*.js) do set LIBS=!LIBS! /resource:%%f,ui/%%~nxf
for %%f in (ui\fonts\*.woff2) do set LIBS=!LIBS! /resource:%%f,ui/fonts/%%~nxf
echo Compiling Hoard...
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize+ /platform:x64 /target:winexe /out:Hoard.exe ^
  /win32icon:icon\hoard.ico /win32manifest:hoard.manifest /resource:icon\hoard.ico,Hoard.ico ^
  /reference:lib\Microsoft.Web.WebView2.Core.dll /reference:lib\Microsoft.Web.WebView2.WinForms.dll ^
  /reference:System.Web.Extensions.dll %LIBS% ^
  Hoard.cs MainForm.cs AssemblyInfo.cs
if %errorlevel% neq 0 (
    echo Compilation failed.
    pause
    exit /b %errorlevel%
)
"%~dp0Hoard.exe" --selftest
if %errorlevel% neq 0 (
    echo Self-test failed.
    pause
    exit /b %errorlevel%
)
echo Packing Hoard-portable.zip (the same exe plus Hoard.ini)...
powershell -NoProfile -Command "$d = Join-Path $env:TEMP 'hoard-portable'; Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory $d | Out-Null; Copy-Item Hoard.exe $d; Set-Content (Join-Path $d 'Hoard.ini') '; Hoard settings. While this file sits next to Hoard.exe, nothing goes to the registry.'; Compress-Archive (Join-Path $d '*') Hoard-portable.zip -Force; Remove-Item $d -Recurse -Force"
echo Done: Hoard.exe and Hoard-portable.zip
