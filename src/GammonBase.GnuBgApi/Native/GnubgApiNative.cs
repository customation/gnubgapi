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

/// <summary>
/// Mirrors the native <c>gnubgapi_move</c> struct layout.
/// Must be kept in sync with <c>gnubgapi.h</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeMove
{
    public fixed int AnMove[8];
    public fixed byte ResultPositionId[16];
    public uint NSubmoves;
    public uint Pips;
}

/// <summary>
/// Mirrors the native <c>gnubgapi_scored_move</c> struct layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeScoredMove
{
    public NativeMove Move;
    public double Equity;
    public fixed double Probs[5];
}

/// <summary>
/// Mirrors the native <c>gnubgapi_game_turn</c> struct layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeGameTurn
{
    public int Player;
    public int Die1, Die2;
    public fixed int AnMove[8];
}

/// <summary>
/// Mirrors the native <c>gnubgapi_analysis_result</c> struct layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeAnalysisResult
{
    public fixed int TotalMoves[2];
    public fixed int UnforcedMoves[2];
    public fixed int SkillCounts[8]; // [2][4] flattened row-major
    public fixed float TotalError[2];
    public fixed float ErrorPerMove[2];
    public fixed float Mpr[2];
    public fixed byte Rating[64]; // [2][32] flattened
    public int NGames;
}

internal static partial class GnubgApiNative
{
    private const string LibraryName = "gnubgapi";

    /// <summary>Number of output values from a rollout (matches <c>GNUBGAPI_NUM_ROLLOUT_OUTPUTS</c>).</summary>
    internal const int NumRolloutOutputs = 7;

    /// <summary>Maximum number of legal moves for any position/dice (matches <c>GNUBGAPI_MAX_MOVES</c>).</summary>
    internal const int MaxMoves = 3060;

    /// <summary>Number of neural-net input features (matches <c>GNUBGAPI_FEATURE_DIM</c>).</summary>
    internal const int FeatureDim = 248;

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
    internal static partial GnubgApiStatus gnubgapi_evaluate_position_plied(
        IntPtr ctx,
        string positionId,
        string? matchId,
        uint nPlies,
        out double equity,
        out double cubefulEquity);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_evaluate_position_full(
        IntPtr ctx,
        string positionId,
        string? matchId,
        double* outOutput);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_evaluate_position_full_plied(
        IntPtr ctx,
        string positionId,
        string? matchId,
        uint nPlies,
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

    // ── Move generation ──

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_generate_moves(
        IntPtr ctx,
        string positionId,
        int die1,
        int die2,
        NativeMove* outMoves,
        uint* outCount);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_apply_move(
        IntPtr ctx,
        string positionId,
        int* anMove,
        byte* outPositionId);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_find_best_move(
        IntPtr ctx,
        string positionId,
        string? matchId,
        int die1,
        int die2,
        uint nPlies,
        NativeMove* outMove);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_generate_moves_with_eval(
        IntPtr ctx,
        string positionId,
        string? matchId,
        int die1,
        int die2,
        uint nPlies,
        NativeScoredMove* outMoves,
        uint* outCount);

    // ── Game analysis ──

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_analyse_game(
        IntPtr ctx,
        NativeGameTurn* turns,
        uint numTurns,
        uint nPlies,
        NativeAnalysisResult* outResult);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_analyse_mat(
        IntPtr ctx,
        string matPath,
        uint nPlies,
        NativeAnalysisResult* outResult);

    // ── Feature encoding ──

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static unsafe partial GnubgApiStatus gnubgapi_position_to_features(
        IntPtr ctx,
        string positionId,
        int isTopOnRoll,
        float* outFeatures);

    // ── Version ──

    [LibraryImport(LibraryName)]
    internal static unsafe partial void gnubgapi_get_version(
        uint* major,
        uint* minor,
        uint* patch);
}
