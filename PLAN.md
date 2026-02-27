# Implementation Plan ? gnubgapi

This plan sets up a forked `gnubg` build pipeline that emits cross?platform native binaries, then consumes them from a .NET `net10.0` package via P/Invoke under the `GammonBase` namespace.

## Goals
- Cross?platform evaluator usable on Windows (dev), Linux (App Service/Docker), and future macOS.
- Native library interface (P/Invoke), not CLI process spawning.
- Ship native binaries via NuGet packaging from CI artifacts.
- Keep a path to open?sourcing/GPL compliance.

## Phase 0 ? Repo Wiring & Baselines
- Add a `customation/gnubg` fork remote for the existing `gnubg` checkout.
- Create a `customation/gnubg` fork repo (if not already) and push the current branch.
- In `gnubgapi`, add a `docs/` folder with architecture notes and licensing notes.

## Phase 1 ? Native Wrapper Design (C API)
- Define a **minimal C API** in a new `gnubgapi` native shim (to be compiled alongside gnubg):
  - Init/shutdown
  - Load weights/bearoff DB
  - Evaluate a position
  - Run a rollout
  - Optional: parse/serialize position IDs
- Decide on ABI stability rules (C ABI, versioned symbols, `gnubgapi_get_version`).
- Define data structures for positions, cube state, and evaluation output (prefer POD structs).

## Phase 2 ? Forked gnubg Build Integration
- In the `gnubg` fork:
  - Add a new `libgnubgapi` target that exposes the C API.
  - Ensure builds produce shared libraries:
    - Windows: `gnubgapi.dll`
    - Linux: `libgnubgapi.so`
    - macOS: `libgnubgapi.dylib`
  - Ensure static assets (weights, bearoff DB) can be loaded via explicit paths.
- Add build options to disable GUI and optional features not required for API.

## Phase 3 ? CI Build Matrix & Artifacts
- Add GitHub Actions workflow in `gnubg` fork:
  - OS matrix: `windows-latest`, `ubuntu-latest`, `macos-latest`.
  - Build native libs + package artifacts (library + license + NOTICE + data files if needed).
  - Upload artifacts for `gnubgapi` consumption.
- Version artifacts with tags or commit SHA.

## Phase 4 ? NuGet Native Asset Packaging
- In `gnubgapi`:
  - Add `runtimes/` native assets layout in a `.nupkg` (RID?based):
    - `runtimes/win-x64/native/gnubgapi.dll`
    - `runtimes/linux-x64/native/libgnubgapi.so`
    - `runtimes/osx-x64/native/libgnubgapi.dylib`
  - Decide whether to bundle weights/bearoff DB or download on first use.
  - Ensure licensing files are included in the package.

## Phase 5 ? .NET P/Invoke Layer (GammonBase)
- Create a `GammonBase.Native` project with `DllImport`/`LibraryImport` bindings.
- Create a `GammonBase` facade API:
  - `Evaluator` class with `EvaluatePosition`, `Rollout`, etc.
  - Safe handle / lifetime management.
- Add RID?specific load logic and clear error messages if native library is missing.

## Phase 6 ? Integration & Validation
- Add tests that:
  - Load the native library on each OS.
  - Evaluate known positions with deterministic expected outputs.
- Add performance benchmarks (optional) for common evaluation paths.

## Phase 7 ? Packaging & Release
- Add `gnubgapi` CI:
  - Build + test
  - Pack NuGet
  - Publish to internal feed or GitHub Packages
- Document usage and licensing constraints.

## Key Decisions / Risks
- **Scope**: ?everything gnubg can do? is large. Start with evaluation + rollout, then expand.
- **ABI stability**: changes in upstream gnubg could break API. Keep shim layer minimal and versioned.
- **Licensing**: if distributing binaries, ensure GPL compliance and proper notices.
- **Data files**: weights/bearoff DB size impacts packaging; may need download strategy.

## Next Concrete Steps
1. Add `customation/gnubg` fork remote and push current state.
2. Sketch C API header and minimal shim implementation in the fork.
3. Add a first CI workflow in the fork that builds the shim library on Windows/Linux.
4. Scaffold `gnubgapi` solution with `net10.0` and a placeholder P/Invoke binding layer.
