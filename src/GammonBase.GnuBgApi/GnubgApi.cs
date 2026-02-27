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
