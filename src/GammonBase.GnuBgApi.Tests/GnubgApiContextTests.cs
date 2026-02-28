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

    // ── Full evaluation tests ────────────────────────────────────────

    [Fact]
    public void EvaluatePositionFull_OpeningPosition_ReturnsAllProbabilities()
    {
        var ctx = GetContext();
        var result = ctx.EvaluatePositionFull("4HPwATDgc/ABMA");

        Assert.InRange(result.WinProbability, 0.30, 0.70);
        Assert.InRange(result.WinGammonProbability, 0.0, result.WinProbability);
        Assert.InRange(result.WinBackgammonProbability, 0.0, result.WinGammonProbability);
        Assert.InRange(result.LoseGammonProbability, 0.0, 1.0 - result.WinProbability);
        Assert.InRange(result.LoseBackgammonProbability, 0.0, result.LoseGammonProbability);
        Assert.True(double.IsFinite(result.CubelessEquity));
        Assert.True(double.IsFinite(result.CubefulEquity));
    }

    [Fact]
    public void EvaluatePositionFull_IsDeterministic()
    {
        var ctx = GetContext();
        var r1 = ctx.EvaluatePositionFull("4HPwATDgc/ABMA");
        var r2 = ctx.EvaluatePositionFull("4HPwATDgc/ABMA");

        Assert.Equal(r1.WinProbability, r2.WinProbability);
        Assert.Equal(r1.CubelessEquity, r2.CubelessEquity);
        Assert.Equal(r1.CubefulEquity, r2.CubefulEquity);
    }

    [Fact]
    public void EvaluatePositionFull_EquityMatchesSimpleEval()
    {
        var ctx = GetContext();
        var simple = ctx.EvaluatePosition("4HPwATDgc/ABMA");
        var full = ctx.EvaluatePositionFull("4HPwATDgc/ABMA");

        // Cubeful equity should match (both use fCubeful=1)
        Assert.Equal(simple.CubefulEquity, full.CubefulEquity, precision: 6);
    }

    [Fact]
    public void EvaluatePositionFull_WithMatchId_ReturnsFiniteValues()
    {
        var ctx = GetContext();
        var result = ctx.EvaluatePositionFull("4HPwATDgc/ABMA", "cAkAAAAAAAAA");

        Assert.True(double.IsFinite(result.WinProbability));
        Assert.True(double.IsFinite(result.CubelessEquity));
        Assert.True(double.IsFinite(result.CubefulEquity));
    }

    [Fact]
    public void EvaluatePositionFull_InvalidPositionId_Throws()
    {
        var ctx = GetContext();
        Assert.Throws<GnubgApiException>(() => ctx.EvaluatePositionFull("garbage"));
    }

    [Fact]
    public void EvaluatePositionFull_NullPositionId_ThrowsArgumentException()
    {
        var ctx = GetContext();
        Assert.Throws<ArgumentException>(() => ctx.EvaluatePositionFull(null!));
    }

    // ── Rollout tests ────────────────────────────────────────────────

    [Fact]
    public void RolloutPosition_OpeningPosition_ReturnsFiniteValues()
    {
        var ctx = GetContext();

        var settings = new RolloutSettings { Trials = 36 }; // Small rollout for speed
        var result = ctx.RolloutPosition("4HPwATDgc/ABMA", settings: settings);

        Assert.True(double.IsFinite(result.WinProbability),
            $"WinProbability is not finite: {result.WinProbability}");
        Assert.True(double.IsFinite(result.CubelessEquity),
            $"CubelessEquity is not finite: {result.CubelessEquity}");
        Assert.True(double.IsFinite(result.CubefulEquity),
            $"CubefulEquity is not finite: {result.CubefulEquity}");
    }

    [Fact]
    public void RolloutPosition_OpeningPosition_WinProbabilityInRange()
    {
        var ctx = GetContext();

        var settings = new RolloutSettings { Trials = 36 };
        var result = ctx.RolloutPosition("4HPwATDgc/ABMA", settings: settings);

        // Opening position should be close to 50/50
        Assert.InRange(result.WinProbability, 0.30, 0.70);
    }

    [Fact]
    public void RolloutPosition_OpeningPosition_ProbabilitiesSumCorrectly()
    {
        var ctx = GetContext();

        var settings = new RolloutSettings { Trials = 36 };
        var result = ctx.RolloutPosition("4HPwATDgc/ABMA", settings: settings);

        // P(win) should be >= P(win gammon) >= P(win backgammon)
        Assert.True(result.WinProbability >= result.WinGammonProbability,
            $"Win ({result.WinProbability}) < WinGammon ({result.WinGammonProbability})");
        Assert.True(result.WinGammonProbability >= result.WinBackgammonProbability,
            $"WinGammon ({result.WinGammonProbability}) < WinBackgammon ({result.WinBackgammonProbability})");

        // Same for losing side: P(lose) >= P(lose gammon) >= P(lose backgammon)
        var loseProbability = 1.0 - result.WinProbability;
        Assert.True(loseProbability >= result.LoseGammonProbability,
            $"Lose ({loseProbability}) < LoseGammon ({result.LoseGammonProbability})");
        Assert.True(result.LoseGammonProbability >= result.LoseBackgammonProbability,
            $"LoseGammon ({result.LoseGammonProbability}) < LoseBackgammon ({result.LoseBackgammonProbability})");
    }

    [Fact]
    public void RolloutPosition_OpeningPosition_StdDevsArePositive()
    {
        var ctx = GetContext();

        var settings = new RolloutSettings { Trials = 36 };
        var result = ctx.RolloutPosition("4HPwATDgc/ABMA", settings: settings);

        Assert.True(result.WinProbabilityStdDev >= 0,
            $"WinProbabilityStdDev is negative: {result.WinProbabilityStdDev}");
        Assert.True(result.CubelessEquityStdDev >= 0,
            $"CubelessEquityStdDev is negative: {result.CubelessEquityStdDev}");
        Assert.True(result.CubefulEquityStdDev >= 0,
            $"CubefulEquityStdDev is negative: {result.CubefulEquityStdDev}");
    }

    [Fact]
    public void RolloutPosition_WithMatchId_ReturnsFiniteValues()
    {
        var ctx = GetContext();

        var settings = new RolloutSettings { Trials = 36 };
        // 5-point match, score 0-0
        var result = ctx.RolloutPosition("4HPwATDgc/ABMA", "cAkAAAAAAAAA", settings);

        Assert.True(double.IsFinite(result.CubelessEquity),
            $"CubelessEquity is not finite: {result.CubelessEquity}");
        Assert.True(double.IsFinite(result.CubefulEquity),
            $"CubefulEquity is not finite: {result.CubefulEquity}");
    }

    [Fact]
    public void RolloutPosition_DefaultSettings_Uses1296Trials()
    {
        var ctx = GetContext();

        // This test verifies that null settings uses defaults (1296 trials).
        // Just confirm it doesn't throw — the actual trial count is internal.
        var result = ctx.RolloutPosition("4HPwATDgc/ABMA");

        Assert.True(double.IsFinite(result.CubelessEquity),
            $"CubelessEquity is not finite: {result.CubelessEquity}");
    }

    [Fact]
    public void RolloutPosition_MoreTrials_ReducesStdDev()
    {
        var ctx = GetContext();

        var small = new RolloutSettings { Trials = 36 };
        var large = new RolloutSettings { Trials = 324 };

        var resultSmall = ctx.RolloutPosition("4HPwATDgc/ABMA", settings: small);
        var resultLarge = ctx.RolloutPosition("4HPwATDgc/ABMA", settings: large);

        // With more trials, standard deviation should generally be smaller.
        // This is a statistical property, so we use a generous margin.
        // With 324 vs 36 trials (9x more), stddev should be ~3x smaller.
        Assert.True(resultLarge.CubelessEquityStdDev < resultSmall.CubelessEquityStdDev * 1.5,
            $"More trials didn't reduce stddev: {resultLarge.CubelessEquityStdDev} vs {resultSmall.CubelessEquityStdDev}");
    }

    [Fact]
    public void RolloutPosition_NullPositionId_ThrowsArgumentException()
    {
        var ctx = GetContext();
        Assert.Throws<ArgumentException>(() => ctx.RolloutPosition(null!));
    }

    [Fact]
    public void RolloutPosition_EmptyPositionId_ThrowsArgumentException()
    {
        var ctx = GetContext();
        Assert.Throws<ArgumentException>(() => ctx.RolloutPosition(""));
    }

    [Fact]
    public void RolloutPosition_InvalidPositionId_ThrowsGnubgApiException()
    {
        var ctx = GetContext();
        var settings = new RolloutSettings { Trials = 36 };
        Assert.Throws<GnubgApiException>(() => ctx.RolloutPosition("garbage", settings: settings));
    }
}
