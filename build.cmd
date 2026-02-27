@echo off
setlocal

set CONFIGURATION=Release
if not "%~1"=="" set CONFIGURATION=%~1

set "REPO_ROOT=%~dp0"
set "DOTNET_CLI_HOME=%REPO_ROOT%.dotnet"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "MSBuildEnableWorkloadResolver=false"

set "PROJECT=%REPO_ROOT%src\GammonBase.GnuBgApi.TestHarness\GammonBase.GnuBgApi.TestHarness.csproj"

dotnet build "%PROJECT%" -c %CONFIGURATION%
exit /b %ERRORLEVEL%
