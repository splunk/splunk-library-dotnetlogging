@echo off

set DOTNETBASEPATH=%WINDIR%\Microsoft.Net
set DOTNETPATH=%DOTNETBASEPATH%\Framework
if exist %DOTNETBASEPATH%\Framework64 (set DOTNETPATH=%DOTNETBASEPATH%\Framework64) ELSE (set DOTNETPATH=%DOTNETBASEPATH%\Framework)

if exist %DOTNETPATH%\v2.0.50727\MSBuild.exe set MSBUILD=%DOTNETPATH%\v2.0.50727\MSBuild.exe
if exist %DOTNETPATH%\v3.0\MSBuild.exe set MSBUILD=%DOTNETPATH%\v3.0\MSBuild.exe
if exist %DOTNETPATH%\v3.5\MSBuild.exe set MSBUILD=%DOTNETPATH%\v3.5\MSBuild.exe
if exist %DOTNETPATH%\v4.0.30319\MSBuild.exe set MSBUILD=%DOTNETPATH%\v4.0.30319\MSBuild.exe

echo MSBuild path is %MSBUILD%
tools\nuget.exe restore
%MSBUILD% Splunk.Logging.sln  /t:build /nologo