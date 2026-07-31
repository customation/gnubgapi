Param(
  [string]$Tag = 'latest',
  [string]$Owner = 'customation',
  [string]$Repo = 'gnubg'
)

$ErrorActionPreference = 'Stop'

$runtimesDir = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'runtimes'

# Asset is what the release publishes; File is what the runtime folder must
# contain. They differ because linux-x64 and linux-arm64 both build a file
# called libgnubgapi.so — and osx-arm64 and osx-x64 both build
# libgnubgapi.dylib — while a release cannot hold two assets under one name, so
# the RID is in the asset name and stripped back off here.
$targets = @(
  @{ Rid = 'win-x64';     Asset = 'libgnubgapi-win-x64.dll';      File = 'libgnubgapi.dll' },
  @{ Rid = 'linux-x64';   Asset = 'libgnubgapi-linux-x64.so';     File = 'libgnubgapi.so' },
  @{ Rid = 'linux-arm64'; Asset = 'libgnubgapi-linux-arm64.so';   File = 'libgnubgapi.so' },
  @{ Rid = 'osx-arm64';   Asset = 'libgnubgapi-osx-arm64.dylib';  File = 'libgnubgapi.dylib' },
  @{ Rid = 'osx-x64';     Asset = 'libgnubgapi-osx-x64.dylib';    File = 'libgnubgapi.dylib' }
)

foreach ($target in $targets) {
  $dir = Join-Path $runtimesDir "$($target.Rid)/native"
  New-Item -ItemType Directory -Path $dir -Force | Out-Null

  $dest = Join-Path $dir $target.File
  Write-Host "Downloading $($target.Asset) for $($target.Rid)..." -ForegroundColor Cyan

  try {
    if ($Tag -eq 'latest') {
      gh release download --repo "$Owner/$Repo" --pattern $target.Asset --dir $dir --clobber
    } else {
      gh release download $Tag --repo "$Owner/$Repo" --pattern $target.Asset --dir $dir --clobber
    }

    # Land it under the name the loader expects, inside its own RID folder.
    $downloaded = Join-Path $dir $target.Asset
    if (Test-Path $downloaded) {
      Move-Item -Path $downloaded -Destination $dest -Force
    } elseif (-not (Test-Path $dest)) {
      throw "neither $($target.Asset) nor $($target.File) is present in $dir after download"
    }

    Write-Host "  -> $dest" -ForegroundColor Green
  }
  catch {
    Write-Warning "Failed to download $($target.Asset): $_"
  }
}

Write-Host "`nNative binaries:" -ForegroundColor Cyan
Get-ChildItem -Path $runtimesDir -Recurse -File | ForEach-Object {
  Write-Host "  $($_.FullName) ($([math]::Round($_.Length / 1KB, 1)) KB)"
}
