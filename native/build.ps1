Param(
  [string]$OutDir = $null
)
# Build libgnubgapi.dll on Windows by delegating to build.sh inside MSYS2's
# MinGW64 environment. gnubg is autotools-based and the toolchain that
# upstream targets on Windows is mingw-w64; native MSVC isn't supported.
#
# Requires: MSYS2 installed at C:\msys64 with the mingw-w64-x86_64 toolchain
#           plus glib2, gmp, autoconf, automake, libtool, make, bison, flex,
#           pkg-config, gettext-devel.

$ErrorActionPreference = 'Stop'

$nativeDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Split-Path -Parent $nativeDir
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'runtimes\win-x64\native' }
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$msys2Bash = 'C:\msys64\usr\bin\bash.exe'
if (-not (Test-Path $msys2Bash)) {
  throw "MSYS2 bash not found at $msys2Bash. Install MSYS2 and the mingw-w64 toolchain (see header)."
}

# Convert Windows paths to MSYS2-style for the bash invocation.
function ToMsysPath([string]$p) {
  $abs = (Resolve-Path -LiteralPath $p -ErrorAction SilentlyContinue)
  if (-not $abs) { $abs = (Join-Path (Get-Location) $p) }
  $abs = "$abs"
  return ($abs -replace '^([A-Za-z]):', '/$1' -replace '\\','/').ToLower()
}

$nativeMsys = ToMsysPath $nativeDir
$outMsys    = ToMsysPath $OutDir

# We invoke the same build.sh, but it needs to use Windows-compatible
# linker output and target a .dll. We pass an env var so build.sh switches
# behaviour rather than maintaining two scripts that drift.
$env:GNUBGAPI_TARGET = 'win-x64'
& $msys2Bash -lc "MSYSTEM=MINGW64 source /etc/profile && cd '$nativeMsys' && GNUBGAPI_TARGET=win-x64 ./build.sh '$outMsys'"
if ($LASTEXITCODE -ne 0) { throw "build.sh failed with exit code $LASTEXITCODE" }

Write-Host "Done: $OutDir\libgnubgapi.dll" -ForegroundColor Green
