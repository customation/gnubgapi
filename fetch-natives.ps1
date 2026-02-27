Param(
  [string]$Tag = 'latest',
  [string]$Owner = 'customation',
  [string]$Repo = 'gnubg'
)

$ErrorActionPreference = 'Stop'

$runtimesDir = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'runtimes'

$targets = @(
  @{ Rid = 'win-x64'; File = 'libgnubgapi.dll' },
  @{ Rid = 'linux-x64'; File = 'libgnubgapi.so' }
)

foreach ($target in $targets) {
  $dir = Join-Path $runtimesDir "$($target.Rid)/native"
  New-Item -ItemType Directory -Path $dir -Force | Out-Null

  $dest = Join-Path $dir $target.File
  Write-Host "Downloading $($target.File) for $($target.Rid)..." -ForegroundColor Cyan

  try {
    if ($Tag -eq 'latest') {
      gh release download --repo "$Owner/$Repo" --pattern $target.File --dir $dir --clobber
    } else {
      gh release download $Tag --repo "$Owner/$Repo" --pattern $target.File --dir $dir --clobber
    }
    Write-Host "  -> $dest" -ForegroundColor Green
  }
  catch {
    Write-Warning "Failed to download $($target.File): $_"
  }
}

Write-Host "`nNative binaries:" -ForegroundColor Cyan
Get-ChildItem -Path $runtimesDir -Recurse -File | ForEach-Object {
  Write-Host "  $($_.FullName) ($([math]::Round($_.Length / 1KB, 1)) KB)"
}
