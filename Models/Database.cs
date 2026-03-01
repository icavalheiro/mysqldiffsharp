using System.Diagnostics;
using System.Text.RegularExpressions;
using SqlDiffSharp.Models;
using static SqlDiffSharp.Utils.ConsoleUtils;

public partial class Database
{
    public string Name { get; set; }
    public string SourceType { get; set; }
    public Dictionary<string, Table> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly int dbNum;
    private readonly AppConfig config;

    public Database(string arg, AppConfig config, int dbNum)
    {
        this.config = config;
        this.dbNum = dbNum;
        string text;

        if (arg.StartsWith("db:"))
        {
            Name = arg[3..];
            SourceType = "db";
            text = Dump();
        }
        else if (File.Exists(arg))
        {
            Name = arg;
            SourceType = "file";
            text = File.ReadAllText(arg);
        }
        else
        {
            Name = arg;
            SourceType = "db";
            text = Dump();
        }

        ParseDefs(text);
    }

    public List<string> GetAuthArgs()
    {
        var args = new List<string>();
        var host = config.GetAuth("host", dbNum);
        var user = config.GetAuth("user", dbNum);
        var pass = config.GetAuth("password", dbNum);
        var port = config.GetAuth("port", dbNum);
        var socket = config.GetAuth("socket", dbNum);

        if (!string.IsNullOrEmpty(host))
        {
            args.Add("-h");
            args.Add(host);
        }

        if (!string.IsNullOrEmpty(user))
        {
            args.Add("-u");
            args.Add(user);
        }

        if (!string.IsNullOrEmpty(pass))
            args.Add($"-p{pass}");

        if (!string.IsNullOrEmpty(port))
        {
            args.Add("-P");
            args.Add(port);
        }

        if (!string.IsNullOrEmpty(socket))
        {
            args.Add("-S");
            args.Add(socket);
        }

        return args;
    }

    private string Dump()
    {
        Log($"Running mysqldump for {Name}");
        var args = GetAuthArgs();
        args.Add("-d"); // no data
        args.Add("--compact");
        args.Add(Name);

        var psi = new ProcessStartInfo
        {
            FileName = "mysqldump",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var process = Process.Start(psi);
        if (process == null)
            throw new Exception("Failed to start mysqldump.");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new Exception($"mysqldump failed: {error}");

        return output;
    }

    private void ParseDefs(string text)
    {
        // Remove comments and directives
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                        .Where(l => !SetSelectRegex().IsMatch(l));

        string cleanText = string.Join("\n", lines).Replace("`", "");

        // Split into CREATE TABLE blocks
        var tableBlocks = CreateTableSelectRegex().Split(cleanText);

        foreach (var block in tableBlocks)
        {
            if (string.IsNullOrWhiteSpace(block))
                continue;

            if (block.TrimStart().StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            {
                var table = new Table(block);
                Tables.Add(table.Name!, table);
                Log($"Parsed table: {table.Name}");
            }
        }
    }

    [GeneratedRegex(@"^\s*(#|--|SET|/\*)")]
    private static partial Regex SetSelectRegex();
    [GeneratedRegex(@"(?im)^\s*(?=CREATE\s+TABLE\s+)", RegexOptions.None, "en-us")]
    private static partial Regex CreateTableSelectRegex();
}
