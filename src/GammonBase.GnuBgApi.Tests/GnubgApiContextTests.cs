using GammonBase.Gnubg;

namespace GammonBase.GnuBgApi.Tests;

/// <summary>
/// Shared fixture that creates a single gnubg context for the entire test run.
/// gnubg has global state that does not support multiple init/shutdown cycles,
/// so we initialise once and share across all tests in the collection.
/// </summary>
public sealed class GnubgFixture : IDisposable
{
    public GnubgApiContext? Context { get; }
    public string? SkipReason { get; }

    public GnubgFixture()
    {
        var weightsPath = Environment.GetEnvironmentVariable("GNUBG_WEIGHTS");
        var weightsBinPath = Environment.GetEnvironmentVariable("GNUBG_WEIGHTS_BIN");

        if (string.IsNullOrWhiteSpace(weightsPath) || string.IsNullOrWhiteSpace(weightsBinPath))
        {
            SkipReason = "GNUBG_WEIGHTS and GNUBG_WEIGHTS_BIN environment variables are not set.";
            return;
        }

        if (!File.Exists(weightsPath))
        {
            SkipReason = $"Weights file not found: {weightsPath}";
            return;
        }

        if (!File.Exists(weightsBinPath))
        {
            SkipReason = $"Binary weights file not found: {weightsBinPath}";
            return;
        }

        var dataDir = Environment.GetEnvironmentVariable("GNUBG_DATA_DIR");

        Context = GnubgApiContext.Create();
        Context.Init(weightsPath, weightsBinPath, dataDir, noBearoff: true);
    }

    public void Dispose()
    {
        if (Context is not null && !Context.IsInvalid)
        {
            Context.Shutdown();
            Context.Dispose();
        }
    }
}

/// <summary>
/// Integration tests for <see cref="GnubgApiContext"/>.
///
/// These tests require GNU Backgammon data files (weights, bearoff DBs).
/// Set the following environment variables before running:
///   GNUBG_WEIGHTS     — path to gnubg.weights
///   GNUBG_WEIGHTS_BIN — path to gnubg.wd
///   GNUBG_DATA_DIR    — (optional) directory containing gnubg_ts0.bd / gnubg_os0.bd
///
/// Tests are automatically skipped when the data files are not configured.
/// </summary>
public sealed class GnubgApiContextTests : IClassFixture<GnubgFixture>
{
    private readonly GnubgFixture _fixture;

    public GnubgApiContextTests(GnubgFixture fixture)
    {
        _fixture = fixture;
    }

    private GnubgApiContext GetContext()
    {
        if (_fixture.SkipReason is not null) Assert.Skip(_fixture.SkipReason);
        return _fixture.Context!;
    }

    private void SkipIfNoNativeLibrary()
    {
        if (_fixture.SkipReason is not null) Assert.Skip(_fixture.SkipReason);
    }

    // ── Opening position ────────────────────────────────────────────

    [Fact]
    public void EvaluatePosition_OpeningPosition_ReturnsExpectedEquityRange()
    {
        var ctx = GetContext();

        // Standard backgammon opening: all 15 checkers in starting position
        var result = ctx.EvaluatePosition("4HPwATDgc/ABMA");

        // The opening is roughly equal but slightly positive for the player on roll
        Assert.InRange(result.Equity, -0.10, 0.20);
        Assert.InRange(result.CubefulEquity, -0.15, 0.25);
    }

    [Fact]
    public void EvaluatePosition_OpeningPosition_IsDeterministic()
    {
        var ctx = GetContext();

        var result1 = ctx.EvaluatePosition("4HPwATDgc/ABMA");
        var result2 = ctx.EvaluatePosition("4HPwATDgc/ABMA");

        Assert.Equal(result1.Equity, result2.Equity, precision: 10);
        Assert.Equal(result1.CubefulEquity, result2.CubefulEquity, precision: 10);
    }

    // ── Known positions with match IDs ──────────────────────────────

    [Fact]
    public void EvaluatePosition_WithMatchId_CubelessEquityMatchesMoneyGame()
    {
        var ctx = GetContext();

        // Money game (no match ID)
        var money = ctx.EvaluatePosition("4HPwATDgc/ABMA");

        // 5-point match, score 0-0 (match ID: cAkAAAAAAAAA)
        var match = ctx.EvaluatePosition("4HPwATDgc/ABMA", "cAkAAAAAAAAA");

        // Cubeless equity should be very close or identical regardless of match context
        Assert.Equal(money.Equity, match.Equity, precision: 6);
    }

    // ── Extreme positions ───────────────────────────────────────────

    [Fact]
    public void EvaluatePosition_StrongBearoffPosition_ReturnsLargePositiveEquity()
    {
        var ctx = GetContext();

        // Player has all 15 checkers on their 1-point (about to bear off)
        // Opponent has all 15 on their 24-point (far from home)
        var result = ctx.EvaluatePosition("AAAA/xgAAAAAAMA");

        Assert.True(result.Equity > 0.5,
            $"Expected large positive equity for strong bearoff position, got {result.Equity}");
    }

    // ── Error handling ──────────────────────────────────────────────

    [Fact]
    public void EvaluatePosition_NullPositionId_ThrowsArgumentException()
    {
        var ctx = GetContext();
        Assert.Throws<ArgumentException>(() => ctx.EvaluatePosition(null!));
    }

    [Fact]
    public void EvaluatePosition_EmptyPositionId_ThrowsArgumentException()
    {
        var ctx = GetContext();
        Assert.Throws<ArgumentException>(() => ctx.EvaluatePosition(""));
    }

    [Fact]
    public void EvaluatePosition_WhitespacePositionId_ThrowsArgumentException()
    {
        var ctx = GetContext();
        Assert.Throws<ArgumentException>(() => ctx.EvaluatePosition("   "));
    }

    [Fact]
    public void EvaluatePosition_InvalidPositionId_ThrowsGnubgApiException()
    {
        var ctx = GetContext();
        Assert.Throws<GnubgApiException>(() => ctx.EvaluatePosition("not-a-valid-position-id"));
    }

    // ── Context lifecycle ───────────────────────────────────────────

    [Fact]
    public void Create_ReturnsNonInvalidHandle()
    {
        SkipIfNoNativeLibrary();

        // Just verify we can create a context (don't init — that's done by the fixture)
        // We only test that the factory method returns a valid handle.
        // Note: we don't Dispose here to avoid messing with global state.
        Assert.False(_fixture.Context!.IsInvalid);
    }

    [Fact]
    public void Init_WithNullWeightsPath_ThrowsArgumentException()
    {
        SkipIfNoNativeLibrary();

        // Use the existing context to test argument validation
        // (this is checked on the managed side before calling native code)
        using var ctx = GnubgApiContext.Create();
        Assert.Throws<ArgumentException>(() => ctx.Init(null!, "some-path"));
    }

    [Fact]
    public void Init_WithNullWeightsBinPath_ThrowsArgumentException()
    {
        SkipIfNoNativeLibrary();

        using var ctx = GnubgApiContext.Create();
        Assert.Throws<ArgumentException>(() => ctx.Init("some-path", null!));
    }

    // ── Multiple evaluations ────────────────────────────────────────

    [Fact]
    public void EvaluatePosition_MultiplePositions_AllReturnFiniteEquity()
    {
        var ctx = GetContext();

        string[] positionIds =
        [
            "4HPwATDgc/ABMA",   // Opening
            "sG2wATDgc/ABMA",   // After 31 (8/5, 6/5)
            "4HPwATDgc/ABEA",   // Opening from other side
        ];

        foreach (var posId in positionIds)
        {
            var result = ctx.EvaluatePosition(posId);

            Assert.True(double.IsFinite(result.Equity),
                $"Position {posId}: Equity is not finite ({result.Equity})");
            Assert.True(double.IsFinite(result.CubefulEquity),
                $"Position {posId}: CubefulEquity is not finite ({result.CubefulEquity})");
            Assert.InRange(result.Equity, -3.0, 3.0);
            Assert.InRange(result.CubefulEquity, -3.0, 3.0);
        }
    }
}
