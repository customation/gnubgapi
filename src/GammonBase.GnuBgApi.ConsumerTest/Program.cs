// Consumer test: verifies the NuGet package works end-to-end when
// consumed via PackageReference (not project reference).
//
// Usage:
//   GNUBG_WEIGHTS=<path> GNUBG_WEIGHTS_BIN=<path> dotnet run
//
// Exit codes: 0 = success, 1 = test failure, 2 = missing config

using GammonBase.Gnubg;

var weightsPath = Environment.GetEnvironmentVariable("GNUBG_WEIGHTS");
var weightsBinPath = Environment.GetEnvironmentVariable("GNUBG_WEIGHTS_BIN");

if (string.IsNullOrWhiteSpace(weightsPath) || string.IsNullOrWhiteSpace(weightsBinPath))
{
    Console.Error.WriteLine("Set GNUBG_WEIGHTS and GNUBG_WEIGHTS_BIN environment variables.");
    return 2;
}

var passed = 0;
var failed = 0;

void Assert(bool condition, string name, string? detail = null)
{
    if (condition)
    {
        Console.WriteLine($"  PASS: {name}");
        passed++;
    }
    else
    {
        Console.Error.WriteLine($"  FAIL: {name}{(detail is null ? "" : $" — {detail}")}");
        failed++;
    }
}

try
{
    Console.WriteLine("=== GammonBase.GnuBgApi Consumer Test ===");
    Console.WriteLine();

    // 1. Create context
    Console.WriteLine("[1] Context lifecycle");
    using var ctx = GnubgApiContext.Create();
    Assert(!ctx.IsInvalid, "Create() returns valid handle");

    // 2. Init
    ctx.Init(weightsPath, weightsBinPath, noBearoff: true);
    Console.WriteLine("  Init succeeded (no-bearoff mode)");

    // 3. Evaluate opening position
    Console.WriteLine();
    Console.WriteLine("[2] Evaluate opening position (4HPwATDgc/ABMA)");
    var opening = ctx.EvaluatePosition("4HPwATDgc/ABMA");
    Console.WriteLine($"  Equity         = {opening.Equity:F6}");
    Console.WriteLine($"  CubefulEquity  = {opening.CubefulEquity:F6}");
    Assert(opening.Equity is > -0.10 and < 0.20,
        "Equity in expected range [-0.10, 0.20]", $"got {opening.Equity:F6}");
    Assert(double.IsFinite(opening.CubefulEquity),
        "CubefulEquity is finite");

    // 4. Evaluate with match ID
    Console.WriteLine();
    Console.WriteLine("[3] Evaluate with match ID (5-point match)");
    var match = ctx.EvaluatePosition("4HPwATDgc/ABMA", "cAkAAAAAAAAA");
    Console.WriteLine($"  Equity         = {match.Equity:F6}");
    Console.WriteLine($"  CubefulEquity  = {match.CubefulEquity:F6}");
    Assert(Math.Abs(opening.Equity - match.Equity) < 0.001,
        "Cubeless equity matches money game",
        $"diff = {Math.Abs(opening.Equity - match.Equity):F8}");

    // 5. Determinism
    Console.WriteLine();
    Console.WriteLine("[4] Determinism check");
    var repeat = ctx.EvaluatePosition("4HPwATDgc/ABMA");
    Assert(opening.Equity == repeat.Equity, "Equity is deterministic");
    Assert(opening.CubefulEquity == repeat.CubefulEquity, "CubefulEquity is deterministic");

    // 6. Error handling
    Console.WriteLine();
    Console.WriteLine("[5] Error handling");
    try { ctx.EvaluatePosition(""); Assert(false, "Empty position throws"); }
    catch (ArgumentException) { Assert(true, "Empty position throws ArgumentException"); }

    try { ctx.EvaluatePosition("garbage"); Assert(false, "Invalid position throws"); }
    catch (GnubgApiException) { Assert(true, "Invalid position throws GnubgApiException"); }

    // 7. Shutdown
    Console.WriteLine();
    Console.WriteLine("[6] Shutdown");
    ctx.Shutdown();
    Assert(true, "Shutdown succeeded");

    // Summary
    Console.WriteLine();
    Console.WriteLine($"=== {passed} passed, {failed} failed ===");
    return failed > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL: {ex}");
    return 1;
}
