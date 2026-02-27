Param(
  [ValidateSet('Debug','Release')]
  [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:MSBuildEnableWorkloadResolver = 'false'

$project = Join-Path $repoRoot 'src\GammonBase.GnuBgApi.TestHarness\GammonBase.GnuBgApi.TestHarness.csproj'

Write-Host "Building $project ($Configuration)" -ForegroundColor Cyan

dotnet build $project -c $Configuration
