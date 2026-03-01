using MySqlDiffSharp.Models;
using MySqlDiffSharp.Utils;
using static MySqlDiffSharp.Utils.ConsoleUtils;

var config = new AppConfig(args);
if (config.ShowHelp || args.Length < 2)
{
    PrintHelp();
    return 0;
}

string arg1 = config.PositionalArgs[0];
string arg2 = config.PositionalArgs[1];

Log($"Parsing database 1: {arg1}");
var db1 = new Database(arg1, config, 1);

Log($"Parsing database 2: {arg2}");
var db2 = new Database(arg2, config, 2);

Log("Comparing databases...");
string diffs = DiffEngine.Compare(db1, db2, config);

if (string.IsNullOrWhiteSpace(diffs))
{
    Log("No differences found.");
    return 0;
}

Log("Diffs ready:");
Log("########################################################");
Console.Write(diffs);
Console.WriteLine();
Log("########################################################");

if (config.BatchApply || config.Apply)
{
    if (!config.BatchApply)
    {
        Log($"\nApply above changes to {db2.Name} [y/N] ? ");

        if (diffs.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase))
            Log("(CAUTION! Changes contain DROP TABLE commands.)");

        var reply = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (reply != "y" && reply != "yes")
            return 0;
    }

    await DiffEngine.ApplyDiffAsync(db2, diffs);
    Log("Successfully applied changes.");
}

return 0;



