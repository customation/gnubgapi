GammonBase.GnuBgApi
===================

P/Invoke bindings for the `gnubgapi` native library built from the
`customation/gnubg` fork.

Usage (C#)
----------

```csharp
using GammonBase.Gnubg;

using var ctx = GnubgApiContext.Create();
ctx.Init(
    weightsPath: "/path/to/gnubg.weights",
    weightsBinaryPath: "/path/to/gnubg.wd",
    dataDir: "/path/to/data"); // contains gnubg_ts0.bd / gnubg_os0.bd

var result = ctx.EvaluatePosition("ADAAQAkIAAAAAA");
Console.WriteLine(result.Equity);
```

Native binaries
---------------

This package expects native binaries under `runtimes/<rid>/native/`:
- Windows: `gnubgapi.dll` or `libgnubgapi.dll`
- Linux: `libgnubgapi.so`
- macOS: `libgnubgapi.dylib`

The `dataDir` should contain the bearoff databases `gnubg_ts0.bd` and
`gnubg_os0.bd` (or you can disable bearoff in `Init`).

Build
-----

On some .NET 10 installations, solution builds (`.slnx`) can fail due to
missing workload SDK locator folders. If `dotnet build GammonBase.GnuBgApi.slnx`
fails with no errors, use the provided script instead:

```powershell
.\build.ps1
```

This builds the test harness project directly with `MSBuildEnableWorkloadResolver=false`.
