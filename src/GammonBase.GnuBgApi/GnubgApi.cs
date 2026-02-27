using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using GammonBase.Gnubg.Native;

namespace GammonBase.Gnubg;

public sealed class GnubgApiContext : SafeHandleZeroOrMinusOneIsInvalid
{
    private GnubgApiContext() : base(true)
    {
    }

    public static GnubgApiContext Create()
    {
        var handle = GnubgApiNative.gnubgapi_create();
        if (handle == IntPtr.Zero)
        {
            throw new GnubgApiException(GnubgApiNativeHelpers.GetLastError());
        }

        return new GnubgApiContext { handle = handle };
    }

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

    public void Shutdown()
    {
        if (!IsInvalid)
        {
            GnubgApiNative.gnubgapi_shutdown(handle);
        }
    }

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

    protected override bool ReleaseHandle()
    {
        GnubgApiNative.gnubgapi_destroy(handle);
        return true;
    }
}

public sealed class GnubgApiException : Exception
{
    public GnubgApiException(string message) : base(message)
    {
    }
}

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
