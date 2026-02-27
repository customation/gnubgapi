# Implementation Plan — gnubgapi

This plan sets up a forked `gnubg` build pipeline that emits cross-platform native binaries, then consumes them from a .NET `net10.0` package via P/Invoke under the `GammonBase` namespace.

## Goals
- Cross-platform evaluator usable on Windows (dev), Linux (App Service/Docker), and future macOS.
- Native library interface (P/Invoke), not CLI process spawning.
- Ship native binaries via NuGet packaging from CI artifacts.
- Keep a path to open-sourcing/GPL compliance.

## Phase 0 — Repo Wiring & Baselines — :white_check_mark: DONE
- ~~Add a `customation/gnubg` fork remote for the existing `gnubg` checkout.~~
- ~~Create a `customation/gnubg` fork repo (if not already) and push the current branch.~~
- In `gnubgapi`, add a `docs/` folder with architecture notes and licensing notes.

> **Status:** Fork remote exists and is pushed. Docs folder deferred (not blocking).

## Phase 1 — Native Wrapper Design (C API) — :white_check_mark: DONE
- Define a **minimal C API** in a new `gnubgapi` native shim (to be compiled alongside gnubg):
  - Init/shutdown
  - Load weights/bearoff DB
  - Evaluate a position
  - ~~Run a rollout~~
  - Optional: parse/serialize position IDs
- Decide on ABI stability rules (C ABI, versioned symbols, `gnubgapi_get_version`).
- Define data structures for positions, cube state, and evaluation output (prefer POD structs).

> **Status:** C API defined in `api/gnubgapi.h` — covers create/destroy, init/shutdown, evaluate_position, get_version, get_last_error. Rollout deferred to future iteration.

## Phase 2 — Forked gnubg Build Integration — :white_check_mark: DONE
- In the `gnubg` fork:
  - Add a new `libgnubgapi` target that exposes the C API.
  - Ensure builds produce shared libraries:
    - Windows: `gnubgapi.dll` :white_check_mark:
    - Linux: `libgnubgapi.so` :white_check_mark:
    - macOS: `libgnubgapi.dylib` (deferred)
  - Ensure static assets (weights, bearoff DB) can be loaded via explicit paths.
- Add build options to disable GUI and optional features not required for API.

> **Status:** `configure.ac` changed to `LT_INIT` (was `disable-shared`). `api/Makefile.am` cleaned up (removed format sources, added AC defines). Stubs added in `gnubgapi.c` for linker symbols. Builds successfully on Windows (MSYS2/MinGW64) and Linux.

## Phase 3 — CI Build Matrix & Artifacts — :white_check_mark: DONE
- Add GitHub Actions workflow in `gnubg` fork:
  - OS matrix: `windows-latest`, `ubuntu-latest` :white_check_mark:, ~~`macos-latest`~~ (deferred).
  - Build native libs + package artifacts (library + license + NOTICE + data files if needed).
  - Upload artifacts for `gnubgapi` consumption.
- Version artifacts with tags or commit SHA.

> **Status:** `.github/workflows/build-native.yml` created. CI green on both platforms. Release `v0.1.0` published with both `libgnubgapi.dll` and `libgnubgapi.so` attached.

## Phase 4 — NuGet Native Asset Packaging — :white_check_mark: DONE
- In `gnubgapi`:
  - Add `runtimes/` native assets layout in a `.nupkg` (RID-based):
    - `runtimes/win-x64/native/libgnubgapi.dll` :white_check_mark:
    - `runtimes/linux-x64/native/libgnubgapi.so` :white_check_mark:
    - `runtimes/osx-x64/native/libgnubgapi.dylib` (deferred)
  - ~~Decide whether to bundle weights/bearoff DB or download on first use.~~
  - Ensure licensing files are included in the package.

> **Status:** Native binaries committed to repo (private gnubg fork prevents cross-repo CI download). `.nupkg` verified to contain both RID-specific native binaries.

## Phase 5 — .NET P/Invoke Layer (GammonBase) — :white_check_mark: DONE
- Create a `GammonBase.Native` project with `DllImport`/`LibraryImport` bindings.
- Create a `GammonBase` facade API:
  - `Evaluator` class with `EvaluatePosition`, ~~`Rollout`~~, etc.
  - Safe handle / lifetime management.
- Add RID-specific load logic and clear error messages if native library is missing.

> **Status:** `GnubgApiNative.cs` uses source-generated `LibraryImport` with `NativeLibrary.SetDllImportResolver` for cross-platform resolution. `GnubgApiContext` extends `SafeHandleZeroOrMinusOneIsInvalid`. Already existed before this effort — no changes needed.

## Phase 6 — Integration & Validation — :construction: PARTIAL
- Add tests that:
  - Load the native library on each OS.
  - Evaluate known positions with deterministic expected outputs.
- Add performance benchmarks (optional) for common evaluation paths.

> **Status:** No unit/integration tests added yet. Local Windows build was verified manually. Need a consumer test to confirm end-to-end.

## Phase 7 — Packaging & Release — :white_check_mark: DONE
- Add `gnubgapi` CI:
  - ~~Build + test~~
  - Pack NuGet :white_check_mark:
  - Publish to GitHub Packages :white_check_mark:
- Document usage and licensing constraints.

> **Status:** `GammonBase.GnuBgApi` v0.1.4 published to `https://nuget.pkg.github.com/customation`. CI workflow: `.github/workflows/publish-package.yml`. Documentation deferred.

## Key Decisions / Risks
- **Scope**: "everything gnubg can do" is large. Start with evaluation ~~+ rollout~~, then expand.
- **ABI stability**: changes in upstream gnubg could break API. Keep shim layer minimal and versioned.
- **Licensing**: if distributing binaries, ensure GPL compliance and proper notices.
- **Data files**: weights/bearoff DB size impacts packaging; may need download strategy.

## Remaining Work
1. :x: **Consumer test** — Reference `GammonBase.GnuBgApi` from gammondb (or a test project), restore from GitHub Packages, call `EvaluatePosition`, confirm it works end-to-end.
2. :x: **Integration tests** — Add test project with known position evaluations.
3. :x: **macOS support** — Add `osx-x64`/`osx-arm64` to build matrix if needed.
4. :x: **Docs folder** — Architecture notes, licensing (GPL) compliance notes.
5. :x: **Rollout API** — Expose `gnubg_rollout` through the C shim if needed.
6. :x: **XML doc warnings** — Add XML comments to public types (CI shows 10 warnings).
