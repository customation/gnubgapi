# gnubgapi native build

Self-contained build for `libgnubgapi.{so,dll}`.

## Layout

- `gnubgapi.c`, `gnubgapi.h` — our C wrapper around gnubg's eval engine.
  Defines a stable C ABI that `GammonBase.GnuBgApi` P/Invokes against.
- `gnubg/` — git submodule, pinned to a specific upstream commit. Treat as
  read-only. We don't edit anything in here. Bumping to a newer gnubg = update
  the submodule pin, retag, re-release.
- `tests/` — small C harness programs for sanity-checking the wrapper.
- `build.sh` — Linux + MSYS2/MinGW64 build script. Compiles `gnubgapi.c`
  alongside the gnubg source files it needs (`eval.c`, `matchid.c`, etc.) and
  links against gnubg's `lib/libevent.la` plus glib/gmp.
- `build.ps1` — Windows entry point that delegates to `build.sh` inside
  MSYS2's MinGW64 shell.

The build does **not** modify the gnubg submodule. It runs gnubg's
`autoreconf` + `./configure` to produce `config.h` (which the gnubg sources
need), then builds gnubg's `lib/` to get `libevent.a`. Our wrapper is then
compiled directly from this directory, pulling source files out of the
submodule by absolute path.

## Build it

After cloning gnubgapi for the first time:

```bash
git submodule update --init --recursive
```

Linux:

```bash
cd native
./build.sh
# → ../runtimes/linux-x64/native/libgnubgapi.so
```

Windows (PowerShell, MSYS2 installed at `C:\msys64`):

```powershell
cd native
.\build.ps1
# → ..\runtimes\win-x64\native\libgnubgapi.dll
```

## Bumping gnubg

```bash
cd native/gnubg
git fetch origin
git checkout <new-tag-or-commit>
cd ../..
git add native/gnubg
git commit -m "Bump gnubg to <ref>"
```

Then run the build, verify the consumer test still passes, retag gnubgapi.
