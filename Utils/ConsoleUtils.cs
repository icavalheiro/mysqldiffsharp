namespace SqlDiffSharp.Utils;

public static class ConsoleUtils
{
    public static void Log(string message)
    {
        Console.WriteLine($"[DEBUG] {message}");
    }

    public static void PrintHelp()
    {
        Log("""
            mysqldiffsharp - compare MySQL database schemas
            Usage: mysqldiffsharp [options] source_database target_database

            Options:
            -h, --host=...      Database host
            -u, --user=...      Database user
            -p, --password=...  Database password
            -P, --port=...      Database port
            -S, --socket=...    Database socket
            (Append 1 or 2 to options to specify for db1 or db2, e.g., --host1=... where 1 is for source and 2 for target)
            
            --keep-old-tables   Don't output DROP TABLE commands
            --apply             Prompt to apply the differences
            --batch-apply       Apply the differences without prompting
            --help              Show this help
            """);
    }
}