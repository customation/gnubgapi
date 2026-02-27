using GammonBase.Gnubg;

var options = Options.Parse(args);
if (!options.IsValid)
{
    Options.PrintUsage();
    return 2;
}

try
{
    using var ctx = GnubgApiContext.Create();
    ctx.Init(options.WeightsPath!, options.WeightsBinaryPath!, options.DataDir, options.NoBearoff);

    var result = ctx.EvaluatePosition(options.PositionId!, options.MatchId);
    Console.WriteLine($"equity={result.Equity:F6}");
    Console.WriteLine($"cubeful_equity={result.CubefulEquity:F6}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

internal sealed record class Options
{
    public string? WeightsPath { get; init; }
    public string? WeightsBinaryPath { get; init; }
    public string? DataDir { get; init; }
    public string? PositionId { get; init; }
    public string? MatchId { get; init; }
    public bool NoBearoff { get; init; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(WeightsPath) &&
        !string.IsNullOrWhiteSpace(WeightsBinaryPath) &&
        !string.IsNullOrWhiteSpace(PositionId);

    public static Options Parse(string[] args)
    {
        var options = new Options
        {
            WeightsPath = Environment.GetEnvironmentVariable("GNUBG_WEIGHTS"),
            WeightsBinaryPath = Environment.GetEnvironmentVariable("GNUBG_WEIGHTS_BIN"),
            DataDir = Environment.GetEnvironmentVariable("GNUBG_DATA_DIR"),
            PositionId = Environment.GetEnvironmentVariable("GNUBG_POSITION_ID"),
            MatchId = Environment.GetEnvironmentVariable("GNUBG_MATCH_ID"),
            NoBearoff = ParseBool(Environment.GetEnvironmentVariable("GNUBG_NO_BEAROFF"))
        };

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    return new Options();
                case "--weights":
                    options = options with { WeightsPath = Next(args, ref i, "--weights") };
                    break;
                case "--weights-bin":
                    options = options with { WeightsBinaryPath = Next(args, ref i, "--weights-bin") };
                    break;
                case "--data-dir":
                    options = options with { DataDir = Next(args, ref i, "--data-dir") };
                    break;
                case "--position-id":
                    options = options with { PositionId = Next(args, ref i, "--position-id") };
                    break;
                case "--match-id":
                    options = options with { MatchId = Next(args, ref i, "--match-id") };
                    break;
                case "--no-bearoff":
                    options = options with { NoBearoff = true };
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"Unknown option: {arg}");
                        return new Options();
                    }
                    break;
            }
        }

        return options;
    }

    private static string Next(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {name}");
        }

        index++;
        return args[index];
    }

    private static bool ParseBool(string? value)
    {
        return value != null &&
               (string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));
    }

    public static void PrintUsage()
    {
        Console.WriteLine("GnubgApi test harness");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/GammonBase.GnuBgApi.TestHarness ");
        Console.WriteLine("    --weights <path> --weights-bin <path> --position-id <id> [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --weights <path>       Path to gnubg.weights");
        Console.WriteLine("  --weights-bin <path>   Path to gnubg.wd");
        Console.WriteLine("  --data-dir <path>      Directory with gnubg_ts0.bd / gnubg_os0.bd");
        Console.WriteLine("  --position-id <id>     Position ID to evaluate");
        Console.WriteLine("  --match-id <id>        Optional match ID");
        Console.WriteLine("  --no-bearoff           Disable bearoff DBs");
        Console.WriteLine();
        Console.WriteLine("Environment variables:");
        Console.WriteLine("  GNUBG_WEIGHTS, GNUBG_WEIGHTS_BIN, GNUBG_DATA_DIR,");
        Console.WriteLine("  GNUBG_POSITION_ID, GNUBG_MATCH_ID, GNUBG_NO_BEAROFF");
    }
}
