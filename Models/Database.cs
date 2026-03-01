using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;
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
            var dumpTask = GetDatabaseSchemaAsync();
            Task.WhenAll([dumpTask]);
            text = dumpTask.Result;
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
            var dumpTask = GetDatabaseSchemaAsync();
            Task.WhenAll([dumpTask]);
            text = dumpTask.Result;
        }

        ParseDefs(text);
    }

    // public List<string> GetAuthArgs()
    // {
    //     var args = new List<string>();
    //     var host = config.GetAuth("host", dbNum);
    //     var user = config.GetAuth("user", dbNum);
    //     var pass = config.GetAuth("password", dbNum);
    //     var port = config.GetAuth("port", dbNum);
    //     var socket = config.GetAuth("socket", dbNum);

    //     if (!string.IsNullOrEmpty(host))
    //     {
    //         args.Add("-h");
    //         args.Add(host);
    //     }

    //     if (!string.IsNullOrEmpty(user))
    //     {
    //         args.Add("-u");
    //         args.Add(user);
    //     }

    //     if (!string.IsNullOrEmpty(pass))
    //         args.Add($"-p{pass}");

    //     if (!string.IsNullOrEmpty(port))
    //     {
    //         args.Add("-P");
    //         args.Add(port);
    //     }

    //     if (!string.IsNullOrEmpty(socket))
    //     {
    //         args.Add("-S");
    //         args.Add(socket);
    //     }

    //     return args;
    // }
    private string GetDbConnectionString()
    {
        var host = config.GetAuth("host", dbNum);
        var user = config.GetAuth("user", dbNum);
        var pass = config.GetAuth("password", dbNum);
        var port = config.GetAuth("port", dbNum);
        var socket = config.GetAuth("socket", dbNum);

        var connectionStringBuilder = new MySqlConnectionStringBuilder
        {
            Server = host,
            UserID = user,
            Password = pass,
            Port = uint.Parse(port ?? "3306"),
            Database = Name,
            ConnectionTimeout = 30,
            AllowUserVariables = true,
            UseCompression = false,
        };

        if (!string.IsNullOrEmpty(socket))
        {
            connectionStringBuilder.ConnectionProtocol = MySqlConnectionProtocol.UnixSocket;
            connectionStringBuilder.Server = socket;
        }
        else
        {
            connectionStringBuilder.ConnectionProtocol = MySqlConnectionProtocol.Tcp;
        }

        return connectionStringBuilder.ConnectionString;
    }

    private async Task<string> GetDatabaseSchemaAsync()
    {
        var schemaBuilder = new StringBuilder();
        var tables = new List<string>();

        await using var connection = new MySqlConnection(GetDbConnectionString());
        await connection.OpenAsync();

        // 1. Get all table names
        await using (var command = new MySqlCommand("SHOW TABLES;", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }
        Log($"Found {tables.Count} tables.");

        // 2. Get the DDL (Data Definition Language) for each table
        var degreeOfParallelism = Environment.ProcessorCount;
        Log($"Using {degreeOfParallelism} parallel tasks.");

        var tableSchemas = new string[tables.Count];

        await Parallel.ForEachAsync(
            tables.Select((table, index) => (table, index)),
            new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism },
            async (item, cancellationToken) =>
            {
                var (table, index) = item;

                // Create a new connection for each task (connections are not thread-safe)
                await using var taskConnection = new MySqlConnection(GetDbConnectionString());
                await taskConnection.OpenAsync(cancellationToken);

                Log($"Loading table information for: {table} (Task {Task.CurrentId})");

                await using var command = new MySqlCommand($"SHOW CREATE TABLE `{table}`;", taskConnection);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    // Store the result at the same index to maintain order
                    tableSchemas[index] = $"{reader.GetString(1)};\n\n";
                }
            }
        );

        // Build the final schema string in order
        foreach (var schema in tableSchemas)
        {
            if (schema != null)
            {
                schemaBuilder.Append(schema);
            }
        }


        // foreach (var table in tables)
        // {
        //     Log($"Loading table information for: {table}");
        //     await using var command = new MySqlCommand($"SHOW CREATE TABLE `{table}`;", connection);
        //     await using var reader = await command.ExecuteReaderAsync();
        //     if (await reader.ReadAsync())
        //     {
        //         // The second column contains the actual CREATE TABLE statement
        //         schemaBuilder.AppendLine(reader.GetString(1));
        //         schemaBuilder.AppendLine(";"); // Append the terminator
        //         schemaBuilder.AppendLine();
        //     }
        // }

        return schemaBuilder.ToString();
    }

    // private string Dump()
    // {
    //     Log($"Running mysqldump for {Name}");
    //     var args = GetAuthArgs();
    //     args.Add("--no-data");
    //     args.Add("--compact");
    //     args.Add(Name);

    //     var psi = new ProcessStartInfo
    //     {
    //         FileName = "mysqldump",
    //         RedirectStandardOutput = true,
    //         RedirectStandardError = true,
    //         UseShellExecute = false,
    //         CreateNoWindow = true
    //     };

    //     foreach (var a in args)
    //         psi.ArgumentList.Add(a);

    //     using var process = Process.Start(psi);
    //     if (process == null)
    //         throw new Exception("Failed to start mysqldump.");

    //     var outputTask = process.StandardOutput.ReadToEndAsync();
    //     var errorTask = process.StandardError.ReadToEndAsync();

    //     process.OutputDataReceived += (sender, e) => Log("out: " + e.Data);
    //     process.ErrorDataReceived += (sender, e) => Log("error:" + e.Data);

    //     process.WaitForExit();

    //     Task.WaitAll([outputTask, errorTask]);

    //     string output = outputTask.Result;
    //     string error = errorTask.Result;

    //     if (process.ExitCode != 0)
    //         throw new Exception($"mysqldump failed: {error}");

    //     return output;
    // }

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
