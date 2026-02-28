using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using GammonBase.Gnubg.Native;

namespace GammonBase.Gnubg;

/// <summary>
/// Wraps a native gnubg API context. Manages lifetime via <see cref="SafeHandleZeroOrMinusOneIsInvalid"/>.
/// </summary>
public sealed class GnubgApiContext : SafeHandleZeroOrMinusOneIsInvalid
{
    private GnubgApiContext() : base(true)
    {
    }

    /// <summary>Creates a new native gnubg API context.</summary>
    /// <returns>A context handle that must be disposed after use.</returns>
    /// <exception cref="GnubgApiException">Thrown when the native context could not be allocated.</exception>
    public static GnubgApiContext Create()
    {
        var handle = GnubgApiNative.gnubgapi_create();
        if (handle == IntPtr.Zero)
        {
            throw new GnubgApiException(GnubgApiNativeHelpers.GetLastError());
        }

        return new GnubgApiContext { handle = handle };
    }

    /// <summary>Initialises the gnubg evaluation engine with neural-net weights and optional bearoff databases.</summary>
    /// <param name="weightsPath">Path to the <c>gnubg.weights</c> text file.</param>
    /// <param name="weightsBinaryPath">Path to the <c>gnubg.wd</c> binary weights file.</param>
    /// <param name="dataDir">Directory containing bearoff databases (<c>gnubg_ts0.bd</c>, <c>gnubg_os0.bd</c>). May be <c>null</c>.</param>
    /// <param name="noBearoff">When <c>true</c>, skip loading bearoff databases entirely.</param>
    /// <exception cref="GnubgApiException">Thrown when initialisation fails.</exception>
    public void Init(string weightsPath, string weightsBinaryPath, string? dataDir = null, bool noBearoff = false)
    {
        if (string.IsNullOrWhiteSpace(weightsPath))
        {
            throw new ArgumentException("weightsPath is required", nameof(weightsPath));
        }

        if (string.IsNullOrWhiteSpace(weightsBinaryPath))
        {
            throw new ArgumentException("weightsBinaryPath is required", nameof(weightsBinaryPath));
        }

        var status = GnubgApiNative.gnubgapi_init(handle, weightsPath, weightsBinaryPath, dataDir, noBearoff ? 1 : 0);
        GnubgApiNativeHelpers.ThrowIfNotOk(status);
    }

    /// <summary>Shuts down the evaluation engine and releases native resources held by the context.</summary>
    public void Shutdown()
    {
        if (!IsInvalid)
        {
            GnubgApiNative.gnubgapi_shutdown(handle);
        }
    }

    /// <summary>Evaluates a backgammon position and returns equity values.</summary>
    /// <param name="positionId">GNU Backgammon position ID string.</param>
    /// <param name="matchId">Optional match ID for match-play equity calculations.</param>
    /// <returns>An <see cref="GnubgEvaluationResult"/> containing equity and cubeful equity.</returns>
    /// <exception cref="GnubgApiException">Thrown when evaluation fails.</exception>
    public GnubgEvaluationResult EvaluatePosition(string positionId, string? matchId = null)
    {
        if (string.IsNullOrWhiteSpace(positionId))
        {
            throw new ArgumentException("positionId is required", nameof(positionId));
        }

        var status = GnubgApiNative.gnubgapi_evaluate_position(handle, positionId, matchId, out var equity, out var cubeful);
        GnubgApiNativeHelpers.ThrowIfNotOk(status);
        return new GnubgEvaluationResult(equity, cubeful);
    }

    /// <summary>Runs a Monte Carlo rollout of the given position.</summary>
    /// <param name="positionId">GNU Backgammon position ID string.</param>
    /// <param name="matchId">Optional match ID for match-play rollouts.</param>
    /// <param name="settings">Rollout configuration. Pass <c>null</c> to use defaults (1 296 trials, cubeful, 0-ply chequer, 2-ply cube).</param>
    /// <returns>A <see cref="GnubgRolloutResult"/> with mean outputs and standard deviations.</returns>
    /// <exception cref="GnubgApiException">Thrown when the rollout fails.</exception>
    public unsafe GnubgRolloutResult RolloutPosition(string positionId, string? matchId = null, RolloutSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(positionId))
        {
            throw new ArgumentException("positionId is required", nameof(positionId));
        }

        var native = (settings ?? RolloutSettings.Default).ToNative();

        double* output = stackalloc double[GnubgApiNative.NumRolloutOutputs];
        double* stdDev = stackalloc double[GnubgApiNative.NumRolloutOutputs];

        var status = GnubgApiNative.gnubgapi_rollout_position(handle, positionId, matchId, in native, output, stdDev);
        GnubgApiNativeHelpers.ThrowIfNotOk(status);

        return new GnubgRolloutResult(
            WinProbability: output[0],
            WinGammonProbability: output[1],
            WinBackgammonProbability: output[2],
            LoseGammonProbability: output[3],
            LoseBackgammonProbability: output[4],
            CubelessEquity: output[5],
            CubefulEquity: output[6],
            WinProbabilityStdDev: stdDev[0],
            WinGammonProbabilityStdDev: stdDev[1],
            WinBackgammonProbabilityStdDev: stdDev[2],
            LoseGammonProbabilityStdDev: stdDev[3],
            LoseBackgammonProbabilityStdDev: stdDev[4],
            CubelessEquityStdDev: stdDev[5],
            CubefulEquityStdDev: stdDev[6]
        );
    }

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        GnubgApiNative.gnubgapi_destroy(handle);
        return true;
    }
}

/// <summary>Exception thrown when a native gnubg API call fails.</summary>
public sealed class GnubgApiException : Exception
{
    /// <summary>Creates a new <see cref="GnubgApiException"/> with the specified error message.</summary>
    /// <param name="message">The error message from the native layer.</param>
    public GnubgApiException(string message) : base(message)
    {
    }
}

/// <summary>The result of a gnubg position evaluation.</summary>
/// <param name="Equity">Money-game equity (positive favours the player on roll).</param>
/// <param name="CubefulEquity">Cubeful equity accounting for cube ownership and match score.</param>
public readonly record struct GnubgEvaluationResult(double Equity, double CubefulEquity);

/// <summary>Configuration for a Monte Carlo rollout.</summary>
public sealed class RolloutSettings
{
    /// <summary>Number of games to simulate. Default is 1 296 (36²).</summary>
    public uint Trials { get; init; } = 1296;

    /// <summary>Whether to use cubeful rollout. Default is <c>true</c>.</summary>
    public bool Cubeful { get; init; } = true;

    /// <summary>Whether to use variance reduction. Default is <c>true</c>.</summary>
    public bool VarianceReduction { get; init; } = true;

    /// <summary>Number of plies for chequer (move) decisions during the rollout. Default is 0.</summary>
    public uint ChequerPlies { get; init; } = 0;

    /// <summary>Number of plies for cube decisions during the rollout. Default is 2.</summary>
    public uint CubePlies { get; init; } = 2;

    /// <summary>Random seed. 0 uses the default seed.</summary>
    public uint Seed { get; init; } = 0;

    /// <summary>Whether to truncate the rollout with bearoff evaluation. Default is <c>true</c>.</summary>
    public bool Truncate { get; init; } = true;

    /// <summary>Ply at which to truncate. Default is 10.</summary>
    public uint TruncatePlies { get; init; } = 10;

    /// <summary>Default rollout settings: 1 296 trials, cubeful, variance reduction, 0-ply chequer, 2-ply cube.</summary>
    public static RolloutSettings Default { get; } = new();

    internal NativeRolloutSettings ToNative() => new()
    {
        NTrials = Trials,
        Cubeful = Cubeful ? 1 : 0,
        VarianceReduction = VarianceReduction ? 1 : 0,
        ChequerPlies = ChequerPlies,
        CubePlies = CubePlies,
        Seed = Seed,
        Truncate = Truncate ? 1 : 0,
        TruncatePlies = TruncatePlies,
    };
}

/// <summary>Full rollout result with mean values and standard deviations for all seven outputs.</summary>
/// <param name="WinProbability">Probability of winning.</param>
/// <param name="WinGammonProbability">Probability of winning a gammon.</param>
/// <param name="WinBackgammonProbability">Probability of winning a backgammon.</param>
/// <param name="LoseGammonProbability">Probability of losing a gammon.</param>
/// <param name="LoseBackgammonProbability">Probability of losing a backgammon.</param>
/// <param name="CubelessEquity">Cubeless equity.</param>
/// <param name="CubefulEquity">Cubeful equity.</param>
/// <param name="WinProbabilityStdDev">Standard deviation of win probability.</param>
/// <param name="WinGammonProbabilityStdDev">Standard deviation of win gammon probability.</param>
/// <param name="WinBackgammonProbabilityStdDev">Standard deviation of win backgammon probability.</param>
/// <param name="LoseGammonProbabilityStdDev">Standard deviation of lose gammon probability.</param>
/// <param name="LoseBackgammonProbabilityStdDev">Standard deviation of lose backgammon probability.</param>
/// <param name="CubelessEquityStdDev">Standard deviation of cubeless equity.</param>
/// <param name="CubefulEquityStdDev">Standard deviation of cubeful equity.</param>
public readonly record struct GnubgRolloutResult(
    double WinProbability,
    double WinGammonProbability,
    double WinBackgammonProbability,
    double LoseGammonProbability,
    double LoseBackgammonProbability,
    double CubelessEquity,
    double CubefulEquity,
    double WinProbabilityStdDev,
    double WinGammonProbabilityStdDev,
    double WinBackgammonProbabilityStdDev,
    double LoseGammonProbabilityStdDev,
    double LoseBackgammonProbabilityStdDev,
    double CubelessEquityStdDev,
    double CubefulEquityStdDev
);

internal static class GnubgApiNativeHelpers
{
    public static void ThrowIfNotOk(GnubgApiStatus status)
    {
        if (status == GnubgApiStatus.Ok)
        {
            return;
        }

        throw new GnubgApiException(GetLastError());
    }

    public static string GetLastError()
    {
        var ptr = GnubgApiNative.gnubgapi_get_last_error();
        if (ptr == IntPtr.Zero)
        {
            return "Unknown gnubgapi error";
        }

        return Marshal.PtrToStringUTF8(ptr) ?? "Unknown gnubgapi error";
    }
}
