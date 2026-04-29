using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using GammonBase.Gnubg.Native;

namespace GammonBase.Gnubg;

/// <summary>
/// Wraps a native gnubg API context. Manages lifetime via <see cref="SafeHandleZeroOrMinusOneIsInvalid"/>.
/// </summary>
/// <remarks>
/// gnubg uses thread-local neural-net state that is only initialised for the thread
/// that calls <c>gnubgapi_init</c>. Move generation and best-move search access this
/// state and will crash (0xC0000005) if called from any other thread. To make the API
/// safe for use from async / thread-pool contexts, all native calls are marshalled to
/// a dedicated worker thread that lives for the lifetime of the context.
/// </remarks>
public sealed class GnubgApiContext : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// gnubg's canonical money-game match-id encoding: cube=1, centred,
    /// no Jacoby, no beavers, nMatchTo=0, no scores, no dice, not Crawford.
    /// Use this when evaluating a money position with default rules; for
    /// non-default money rules build a match-id explicitly.
    /// </summary>
    public const string DefaultMoneyMatchId = "cAkAAAAAAAAA";

    private readonly BlockingCollection<Action> _workQueue = new();
    private readonly Thread _workerThread;

    private GnubgApiContext() : base(true)
    {
        _workerThread = new Thread(WorkerLoop) { IsBackground = true, Name = "gnubg" };
        _workerThread.Start();
    }

    private void WorkerLoop()
    {
        foreach (var action in _workQueue.GetConsumingEnumerable())
            action();
    }

    /// <summary>Runs <paramref name="action"/> on the gnubg thread and blocks until it completes.</summary>
    private void RunOnWorker(Action action)
    {
        if (Thread.CurrentThread == _workerThread)
        {
            action();
            return;
        }

        using var done = new ManualResetEventSlim(false);
        Exception? caught = null;
        _workQueue.Add(() =>
        {
            try { action(); }
            catch (Exception ex) { caught = ex; }
            finally { done.Set(); }
        });
        done.Wait();
        if (caught != null) throw caught;
    }

    /// <summary>Runs <paramref name="func"/> on the gnubg thread and returns the result.</summary>
    private T RunOnWorker<T>(Func<T> func)
    {
        if (Thread.CurrentThread == _workerThread)
            return func();

        using var done = new ManualResetEventSlim(false);
        T result = default!;
        Exception? caught = null;
        _workQueue.Add(() =>
        {
            try { result = func(); }
            catch (Exception ex) { caught = ex; }
            finally { done.Set(); }
        });
        done.Wait();
        if (caught != null) throw caught;
        return result;
    }

    /// <summary>Creates a new native gnubg API context.</summary>
    /// <returns>A context handle that must be disposed after use.</returns>
    /// <exception cref="GnubgApiException">Thrown when the native context could not be allocated.</exception>
    public static GnubgApiContext Create()
    {
        var ctx = new GnubgApiContext();
        ctx.RunOnWorker(() =>
        {
            var h = GnubgApiNative.gnubgapi_create();
            if (h == IntPtr.Zero)
                throw new GnubgApiException(GnubgApiNativeHelpers.GetLastError());
            ctx.handle = h;
        });
        return ctx;
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
            throw new ArgumentException("weightsPath is required", nameof(weightsPath));
        if (string.IsNullOrWhiteSpace(weightsBinaryPath))
            throw new ArgumentException("weightsBinaryPath is required", nameof(weightsBinaryPath));

        RunOnWorker(() =>
        {
            var status = GnubgApiNative.gnubgapi_init(handle, weightsPath, weightsBinaryPath, dataDir, noBearoff ? 1 : 0);
            GnubgApiNativeHelpers.ThrowIfNotOk(status);
        });
    }

    /// <summary>Shuts down the evaluation engine and releases native resources held by the context.</summary>
    /// <remarks>
    /// Must be called explicitly before Dispose. After shutdown the handle is
    /// marked invalid so the SafeHandle finalizer will not attempt
    /// gnubgapi_destroy again (gnubg's MT_Close asserts on the main thread
    /// and calls abort() when run from the .NET finalizer thread).
    /// The small native context struct is intentionally leaked — it is a few
    /// bytes and the process is typically exiting anyway.
    /// </remarks>
    public void Shutdown()
    {
        if (!IsInvalid)
        {
            RunOnWorker(() => GnubgApiNative.gnubgapi_shutdown(handle));
            SetHandleAsInvalid();
        }
        _workQueue.CompleteAdding();
    }

    /// <summary>Evaluates a backgammon position and returns equity values.</summary>
    /// <param name="positionId">GNU Backgammon position ID string.</param>
    /// <param name="matchId">Match ID encoding cube/score/dice context. Money games are encoded with nMatchTo=0; never null.</param>
    /// <returns>An <see cref="GnubgEvaluationResult"/> containing equity and cubeful equity.</returns>
    /// <exception cref="GnubgApiException">Thrown when evaluation fails.</exception>
    public GnubgEvaluationResult EvaluatePosition(string positionId, string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        return RunOnWorker(() =>
        {
            var status = GnubgApiNative.gnubgapi_evaluate_position(handle, positionId, matchId, out var equity, out var cubeful);
            GnubgApiNativeHelpers.ThrowIfNotOk(status);
            return new GnubgEvaluationResult(equity, cubeful);
        });
    }

    /// <summary>Evaluates a position at the specified ply depth and returns equity values.</summary>
    /// <param name="positionId">GNU Backgammon position ID string.</param>
    /// <param name="plies">Number of plies (0=instant, 1=fast, 2=world-class).</param>
    /// <param name="matchId">Match ID encoding cube/score/dice context. Money games are encoded with nMatchTo=0; never null.</param>
    /// <returns>An <see cref="GnubgEvaluationResult"/> containing equity and cubeful equity.</returns>
    /// <exception cref="GnubgApiException">Thrown when evaluation fails.</exception>
    public GnubgEvaluationResult EvaluatePositionPlied(string positionId, uint plies, string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        return RunOnWorker(() =>
        {
            var status = GnubgApiNative.gnubgapi_evaluate_position_plied(handle, positionId, matchId, plies, out var equity, out var cubeful);
            GnubgApiNativeHelpers.ThrowIfNotOk(status);
            return new GnubgEvaluationResult(equity, cubeful);
        });
    }

    /// <summary>Evaluates a position and returns all 7 neural-net outputs (probabilities and equities).</summary>
    /// <param name="positionId">GNU Backgammon position ID string.</param>
    /// <param name="matchId">Match ID encoding cube/score/dice context. Money games are encoded with nMatchTo=0; never null.</param>
    /// <returns>A <see cref="GnubgFullEvaluationResult"/> with all probability and equity outputs.</returns>
    /// <exception cref="GnubgApiException">Thrown when evaluation fails.</exception>
    public GnubgFullEvaluationResult EvaluatePositionFull(string positionId, string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        return RunOnWorker(() =>
        {
            unsafe
            {
                double* output = stackalloc double[GnubgApiNative.NumRolloutOutputs];
                var status = GnubgApiNative.gnubgapi_evaluate_position_full(handle, positionId, matchId, output);
                GnubgApiNativeHelpers.ThrowIfNotOk(status);
                return new GnubgFullEvaluationResult(
                    WinProbability: output[0], WinGammonProbability: output[1],
                    WinBackgammonProbability: output[2], LoseGammonProbability: output[3],
                    LoseBackgammonProbability: output[4], CubelessEquity: output[5],
                    CubefulEquity: output[6]);
            }
        });
    }

    /// <summary>Evaluates a position at the specified ply depth returning all 7 outputs.</summary>
    /// <param name="positionId">GNU Backgammon position ID string.</param>
    /// <param name="plies">Number of plies (0=instant, 1=fast, 2=world-class).</param>
    /// <param name="matchId">Match ID encoding cube/score/dice context. Money games are encoded with nMatchTo=0; never null.</param>
    /// <returns>A <see cref="GnubgFullEvaluationResult"/> with all probability and equity outputs.</returns>
    /// <exception cref="GnubgApiException">Thrown when evaluation fails.</exception>
    public GnubgFullEvaluationResult EvaluatePositionFullPlied(string positionId, uint plies, string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        return RunOnWorker(() =>
        {
            unsafe
            {
                double* output = stackalloc double[GnubgApiNative.NumRolloutOutputs];
                var status = GnubgApiNative.gnubgapi_evaluate_position_full_plied(handle, positionId, matchId, plies, output);
                GnubgApiNativeHelpers.ThrowIfNotOk(status);
                return new GnubgFullEvaluationResult(
                    WinProbability: output[0], WinGammonProbability: output[1],
                    WinBackgammonProbability: output[2], LoseGammonProbability: output[3],
                    LoseBackgammonProbability: output[4], CubelessEquity: output[5],
                    CubefulEquity: output[6]);
            }
        });
    }

    /// <summary>Runs a Monte Carlo rollout of the given position.</summary>
    /// <param name="positionId">GNU Backgammon position ID string.</param>
    /// <param name="matchId">Match ID encoding cube/score/dice context. Money games are encoded with nMatchTo=0; never null.</param>
    /// <param name="settings">Rollout configuration. Pass <c>null</c> to use defaults (1 296 trials, cubeful, 0-ply chequer, 2-ply cube).</param>
    /// <returns>A <see cref="GnubgRolloutResult"/> with mean outputs and standard deviations.</returns>
    /// <exception cref="GnubgApiException">Thrown when the rollout fails.</exception>
    public GnubgRolloutResult RolloutPosition(string positionId, string matchId, RolloutSettings? settings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        var native = (settings ?? RolloutSettings.Default).ToNative();
        return RunOnWorker(() =>
        {
            unsafe
            {
                double* output = stackalloc double[GnubgApiNative.NumRolloutOutputs];
                double* stdDev = stackalloc double[GnubgApiNative.NumRolloutOutputs];
                var status = GnubgApiNative.gnubgapi_rollout_position(handle, positionId, matchId, in native, output, stdDev);
                GnubgApiNativeHelpers.ThrowIfNotOk(status);
                return new GnubgRolloutResult(
                    WinProbability: output[0], WinGammonProbability: output[1],
                    WinBackgammonProbability: output[2], LoseGammonProbability: output[3],
                    LoseBackgammonProbability: output[4], CubelessEquity: output[5],
                    CubefulEquity: output[6],
                    WinProbabilityStdDev: stdDev[0], WinGammonProbabilityStdDev: stdDev[1],
                    WinBackgammonProbabilityStdDev: stdDev[2], LoseGammonProbabilityStdDev: stdDev[3],
                    LoseBackgammonProbabilityStdDev: stdDev[4], CubelessEquityStdDev: stdDev[5],
                    CubefulEquityStdDev: stdDev[6]);
            }
        });
    }

    // ── Move generation ──

    /// <summary>Generates all legal moves for a position and dice roll.</summary>
    public GnubgMove[] GenerateMoves(string positionId, int die1, int die2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        return RunOnWorker(() =>
        {
            unsafe
            {
                var moves = stackalloc NativeMove[GnubgApiNative.MaxMoves];
                uint count = 0;
                var status = GnubgApiNative.gnubgapi_generate_moves(handle, positionId, die1, die2, moves, &count);
                GnubgApiNativeHelpers.ThrowIfNotOk(status);
                var result = new GnubgMove[count];
                for (int i = 0; i < count; i++)
                    result[i] = GnubgMove.FromNative(moves[i]);
                return result;
            }
        });
    }

    /// <summary>Finds the single best move using GnuBG's search.</summary>
    public GnubgMove FindBestMove(string positionId, string matchId, int die1, int die2, uint plies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        return RunOnWorker(() =>
        {
            unsafe
            {
                NativeMove move;
                var status = GnubgApiNative.gnubgapi_find_best_move(handle, positionId, matchId, die1, die2, plies, &move);
                GnubgApiNativeHelpers.ThrowIfNotOk(status);
                return GnubgMove.FromNative(move);
            }
        });
    }

    /// <summary>Generates all legal moves with evaluations, sorted best-first.</summary>
    public GnubgScoredMove[] GenerateMovesWithEval(string positionId, string matchId, int die1, int die2, uint plies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        return RunOnWorker(() =>
        {
            unsafe
            {
                var moves = stackalloc NativeScoredMove[GnubgApiNative.MaxMoves];
                uint count = 0;
                var status = GnubgApiNative.gnubgapi_generate_moves_with_eval(handle, positionId, matchId, die1, die2, plies, moves, &count);
                GnubgApiNativeHelpers.ThrowIfNotOk(status);
                var result = new GnubgScoredMove[count];
                for (int i = 0; i < count; i++)
                    result[i] = GnubgScoredMove.FromNative(moves[i]);
                return result;
            }
        });
    }

    /// <summary>Applies a move and returns the resulting position ID (sides swapped).</summary>
    public string ApplyMove(string positionId, int[] anMove)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        ArgumentNullException.ThrowIfNull(anMove);
        return RunOnWorker(() =>
        {
            unsafe
            {
                fixed (int* movePtr = anMove)
                {
                    var buf = stackalloc byte[16];
                    var status = GnubgApiNative.gnubgapi_apply_move(handle, positionId, movePtr, buf);
                    GnubgApiNativeHelpers.ThrowIfNotOk(status);
                    return Marshal.PtrToStringUTF8((IntPtr)buf) ?? string.Empty;
                }
            }
        });
    }

    // ── Game analysis ──

    /// <summary>Analyses a complete game from structured turn data.</summary>
    public GnubgAnalysisResult AnalyseGame(GnubgGameTurn[] turns, uint plies)
    {
        ArgumentNullException.ThrowIfNull(turns);
        var nativeTurns = new NativeGameTurn[turns.Length];
        for (int i = 0; i < turns.Length; i++)
            nativeTurns[i] = turns[i].ToNative();

        return RunOnWorker(() =>
        {
            unsafe
            {
                NativeAnalysisResult result;
                fixed (NativeGameTurn* ptr = nativeTurns)
                {
                    var status = GnubgApiNative.gnubgapi_analyse_game(handle, ptr, (uint)turns.Length, plies, &result);
                    GnubgApiNativeHelpers.ThrowIfNotOk(status);
                }
                return GnubgAnalysisResult.FromNative(result);
            }
        });
    }

    /// <summary>Analyses a game from a Jellyfish .mat file.</summary>
    public GnubgAnalysisResult AnalyseMat(string matPath, uint plies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matPath);
        return RunOnWorker(() =>
        {
            unsafe
            {
                NativeAnalysisResult result;
                var status = GnubgApiNative.gnubgapi_analyse_mat(handle, matPath, plies, &result);
                GnubgApiNativeHelpers.ThrowIfNotOk(status);
                return GnubgAnalysisResult.FromNative(result);
            }
        });
    }

    // ── Feature encoding ──

    /// <summary>Computes 248 neural-net input features from a position.</summary>
    public float[] PositionToFeatures(string positionId, bool isTopOnRoll)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        var features = new float[GnubgApiNative.FeatureDim];
        RunOnWorker(() =>
        {
            unsafe
            {
                fixed (float* ptr = features)
                {
                    var status = GnubgApiNative.gnubgapi_position_to_features(handle, positionId, isTopOnRoll ? 1 : 0, ptr);
                    GnubgApiNativeHelpers.ThrowIfNotOk(status);
                }
            }
        });
        return features;
    }

    // ── Version ──

    /// <summary>Gets the native API version.</summary>
    public static unsafe (uint Major, uint Minor, uint Patch) GetVersion()
    {
        uint major, minor, patch;
        GnubgApiNative.gnubgapi_get_version(&major, &minor, &patch);
        return (major, minor, patch);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Intentionally does NOT call gnubgapi_destroy. gnubg's thread cleanup
    /// (MT_Close → FreeMutex) asserts it is on the main thread; the .NET
    /// finalizer runs on a different thread, causing abort(). Callers must
    /// call <see cref="Shutdown"/> explicitly to cleanly release engine
    /// resources. The tiny native struct leak is acceptable at process exit.
    /// </remarks>
    protected override bool ReleaseHandle()
    {
        // No-op: see remarks. Shutdown() handles the real cleanup.
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

/// <summary>Full evaluation result with all 7 neural-net outputs.</summary>
/// <param name="WinProbability">Probability of winning.</param>
/// <param name="WinGammonProbability">Probability of winning a gammon.</param>
/// <param name="WinBackgammonProbability">Probability of winning a backgammon.</param>
/// <param name="LoseGammonProbability">Probability of losing a gammon.</param>
/// <param name="LoseBackgammonProbability">Probability of losing a backgammon.</param>
/// <param name="CubelessEquity">Cubeless equity.</param>
/// <param name="CubefulEquity">Cubeful equity.</param>
public readonly record struct GnubgFullEvaluationResult(
    double WinProbability,
    double WinGammonProbability,
    double WinBackgammonProbability,
    double LoseGammonProbability,
    double LoseBackgammonProbability,
    double CubelessEquity,
    double CubefulEquity);

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

// ── Move types ──

/// <summary>A legal backgammon move with up to 4 sub-moves.</summary>
public sealed class GnubgMove
{
    /// <summary>From-to pairs (0-indexed points, bar=24), -1 terminated.</summary>
    public int[] AnMove { get; init; } = [];
    /// <summary>Position ID after the move with sides swapped (next player on roll).</summary>
    public string ResultPositionId { get; init; } = string.Empty;
    /// <summary>Number of sub-moves (1-4).</summary>
    public uint SubMoveCount { get; init; }
    /// <summary>Total pips moved.</summary>
    public uint Pips { get; init; }

    /// <summary>Formats the move in GNU Backgammon notation (e.g. "13/8 6/1").</summary>
    public string ToNotation()
    {
        var parts = new List<string>();
        for (int i = 0; i < AnMove.Length - 1; i += 2)
        {
            int from = AnMove[i], to = AnMove[i + 1];
            if (from < 0) break;
            string f = from == 24 ? "bar" : (from + 1).ToString();
            string t = to < 0 ? "off" : (to + 1).ToString();
            parts.Add($"{f}/{t}");
        }
        return string.Join(" ", parts);
    }

    internal static unsafe GnubgMove FromNative(NativeMove n)
    {
        var move = new int[8];
        for (int i = 0; i < 8; i++) move[i] = n.AnMove[i];
        return new GnubgMove
        {
            AnMove = move,
            ResultPositionId = Marshal.PtrToStringUTF8((IntPtr)n.ResultPositionId) ?? string.Empty,
            SubMoveCount = n.NSubmoves,
            Pips = n.Pips
        };
    }
}

/// <summary>A move with its GnuBG evaluation, sorted best-first.</summary>
public sealed class GnubgScoredMove
{
    /// <summary>The move.</summary>
    public GnubgMove Move { get; init; } = new();
    /// <summary>Cubeful equity from on-roll perspective.</summary>
    public double Equity { get; init; }
    /// <summary>Win probability.</summary>
    public double WinProbability { get; init; }
    /// <summary>Win gammon probability.</summary>
    public double WinGammonProbability { get; init; }
    /// <summary>Win backgammon probability.</summary>
    public double WinBackgammonProbability { get; init; }
    /// <summary>Lose gammon probability.</summary>
    public double LoseGammonProbability { get; init; }
    /// <summary>Lose backgammon probability.</summary>
    public double LoseBackgammonProbability { get; init; }

    internal static unsafe GnubgScoredMove FromNative(NativeScoredMove n) => new()
    {
        Move = GnubgMove.FromNative(n.Move),
        Equity = n.Equity,
        WinProbability = n.Probs[0],
        WinGammonProbability = n.Probs[1],
        WinBackgammonProbability = n.Probs[2],
        LoseGammonProbability = n.Probs[3],
        LoseBackgammonProbability = n.Probs[4]
    };
}

/// <summary>Input for game analysis — one turn of a recorded game.</summary>
public sealed class GnubgGameTurn
{
    /// <summary>Which player was on roll (0 or 1).</summary>
    public int Player { get; init; }
    /// <summary>First die value (1-6).</summary>
    public int Die1 { get; init; }
    /// <summary>Second die value (1-6).</summary>
    public int Die2 { get; init; }
    /// <summary>Move as from-to pairs (0-indexed, bar=24), -1 terminated.</summary>
    public int[] AnMove { get; init; } = [-1, -1, -1, -1, -1, -1, -1, -1];

    internal unsafe NativeGameTurn ToNative()
    {
        var n = new NativeGameTurn { Player = Player, Die1 = Die1, Die2 = Die2 };
        for (int i = 0; i < 8; i++)
            n.AnMove[i] = i < AnMove.Length ? AnMove[i] : -1;
        return n;
    }
}

/// <summary>Result of analysing a complete game — chequerplay error statistics.</summary>
public sealed class GnubgAnalysisResult
{
    /// <summary>Total moves per player [0] and [1].</summary>
    public int[] TotalMoves { get; init; } = [0, 0];
    /// <summary>Moves with more than one legal option per player.</summary>
    public int[] UnforcedMoves { get; init; } = [0, 0];
    /// <summary>Skill counts per player: [player][skill] where skill 0=VeryBad, 1=Bad, 2=Doubtful, 3=None.</summary>
    public int[,] SkillCounts { get; init; } = new int[2, 4];
    /// <summary>Accumulated equity loss per player.</summary>
    public float[] TotalError { get; init; } = [0, 0];
    /// <summary>Average equity loss per unforced move.</summary>
    public float[] ErrorPerMove { get; init; } = [0, 0];
    /// <summary>Millipoints per move (error × 1000).</summary>
    public float[] Mpr { get; init; } = [0, 0];
    /// <summary>Rating string per player (e.g. "Beginner", "World Class").</summary>
    public string[] Rating { get; init; } = ["", ""];
    /// <summary>Number of games analysed.</summary>
    public int GameCount { get; init; }

    internal static unsafe GnubgAnalysisResult FromNative(NativeAnalysisResult n)
    {
        var skills = new int[2, 4];
        for (int p = 0; p < 2; p++)
            for (int s = 0; s < 4; s++)
                skills[p, s] = n.SkillCounts[p * 4 + s];

        return new GnubgAnalysisResult
        {
            TotalMoves = [n.TotalMoves[0], n.TotalMoves[1]],
            UnforcedMoves = [n.UnforcedMoves[0], n.UnforcedMoves[1]],
            SkillCounts = skills,
            TotalError = [n.TotalError[0], n.TotalError[1]],
            ErrorPerMove = [n.ErrorPerMove[0], n.ErrorPerMove[1]],
            Mpr = [n.Mpr[0], n.Mpr[1]],
            Rating =
            [
                Marshal.PtrToStringUTF8((IntPtr)n.Rating, 32)?.TrimEnd('\0') ?? "",
                Marshal.PtrToStringUTF8((IntPtr)(n.Rating + 32), 32)?.TrimEnd('\0') ?? ""
            ],
            GameCount = n.NGames
        };
    }
}
