using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GammonBase.Gnubg.Native;

/// <summary>
/// Mirrors the native <c>gnubgapi_rollout_settings</c> struct layout.
/// Must be kept in sync with <c>gnubgapi.h</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeRolloutSettings
{
    public uint NTrials;
    public int Cubeful;
    public int VarianceReduction;
    public uint ChequerPlies;
    public uint CubePlies;
    public uint Seed;
    public int Truncate;
    public uint TruncatePlies;
}

internal static partial class GnubgApiNative
{
    private const string LibraryName = "gnubgapi";

    /// <summary>Number of output values from a rollout (matches <c>GNUBGAPI_NUM_ROLLOUT_OUTPUTS</c>).</summary>
    internal const int NumRolloutOutputs = 7;

    static GnubgApiNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(GnubgApiNative).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return TryLoad(new[] { "libgnubgapi.dll", "gnubgapi.dll" }, assembly, searchPath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return TryLoad(new[] { "libgnubgapi.dylib" }, assembly, searchPath);
        }

        return TryLoad(new[] { "libgnubgapi.so" }, assembly, searchPath);
    }

    private static IntPtr TryLoad(string[] names, Assembly assembly, DllImportSearchPath? searchPath)
    {
        foreach (var name in names)
        {
            if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr gnubgapi_get_last_error();

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial IntPtr gnubgapi_create();

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void gnubgapi_destroy(IntPtr ctx);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial GnubgApiStatus gnubgapi_init(
        IntPtr ctx,
        string weightsPath,
        string weightsBinaryPath,
        string? dataDir,
        int noBearoff);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void gnubgapi_shutdown(IntPtr ctx);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial GnubgApiStatus gnubgapi_evaluate_position(
        IntPtr ctx,
        string positionId,
        string? matchId,
        out double equity,
        out double cubefulEquity);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_evaluate_position_full(
        IntPtr ctx,
        string positionId,
        string? matchId,
        double* outOutput);

    [LibraryImport(LibraryName)]
    internal static partial void gnubgapi_rollout_settings_default(ref NativeRolloutSettings settings);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_rollout_position(
        IntPtr ctx,
        string positionId,
        string? matchId,
        in NativeRolloutSettings settings,
        double* outOutput,
        double* outStdDev);
}
