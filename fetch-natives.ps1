Param(
  [string]$Tag = 'latest',
  [string]$Owner = 'customation',
  # This repository, which is where build-native.yml publishes the release. The
  # default used to be 'gnubg' -- a repo that holds no such assets -- so every
  # invocation without an explicit -Repo failed at the download.
  [string]$Repo = 'gnubgapi'
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
  # macOS assets are tarballs, not single files: build.sh bundles glib, gobject,
  # gthread, libintl, gmp and pcre2 beside the library and rewrites the
  # references to @loader_path, so they only work together. Archive = $true
  # means "extract into the runtime folder" rather than "copy and rename".
  @{ Rid = 'osx-arm64';   Asset = 'libgnubgapi-osx-arm64.tar.gz'; Archive = $true },
  @{ Rid = 'osx-x64';     Asset = 'libgnubgapi-osx-x64.tar.gz';   Archive = $true }
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
    if ($target.Archive) {
      if (-not (Test-Path $downloaded)) {
        throw "$($target.Asset) is not present in $dir after download"
      }
      # tar ships with Windows 10 1803 and later. Extracting in place gives the
      # library and every dylib bundled beside it, which is the point: a macOS
      # runtime folder holding only libgnubgapi.dylib is one that cannot load.
      tar -xzf $downloaded -C $dir
      if ($LASTEXITCODE -ne 0) { throw "could not extract $($target.Asset)" }
      Remove-Item $downloaded -Force
      $dylibs = @(Get-ChildItem -Path $dir -Filter *.dylib)
      if ($dylibs.Count -lt 2) {
        throw "$($target.Rid) has $($dylibs.Count) dylib(s) after extraction; the bundled dependencies are missing"
      }
    } elseif (Test-Path $downloaded) {
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
