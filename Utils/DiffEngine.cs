using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using MySqlDiffSharp.Models;
using static MySqlDiffSharp.Utils.ConsoleUtils;

namespace MySqlDiffSharp.Utils;

public static partial class DiffEngine
{
    public static string Compare(Database origin, Database target, AppConfig config)
    {
        var sb = new StringBuilder();

        // Tables in target not in origin (Drops)
        if (!config.KeepOldTables)
        {
            foreach (var table in target.Tables.Keys.Except(origin.Tables.Keys))
            {
                sb.AppendLine($"DROP TABLE `{table}`;");
            }
        }

        // Tables in origin not in target (Creates)
        foreach (var table in origin.Tables.Keys.Except(target.Tables.Keys))
        {
            sb.AppendLine(origin.Tables[table].OriginalDef + ";");
        }

        // Tables in both (Alters)
        foreach (var tableName in target.Tables.Keys.Intersect(origin.Tables.Keys))
        {
            var t1 = target.Tables[tableName];
            var t2 = origin.Tables[tableName];
            var tableAlters = new List<string>();

            // Fields
            foreach (var f in t1.Fields.Keys.Except(t2.Fields.Keys))
            {
                tableAlters.Add($"DROP COLUMN `{f}`");
            }
            foreach (var f in t2.Fields.Keys.Except(t1.Fields.Keys))
            {
                tableAlters.Add($"ADD COLUMN `{f}` {t2.Fields[f]}");
            }
            foreach (var f in t1.Fields.Keys.Intersect(t2.Fields.Keys))
            {
                if (!NormalizeDef(t1.Fields[f]).Equals(NormalizeDef(t2.Fields[f]), StringComparison.OrdinalIgnoreCase))
                {
                    tableAlters.Add($"CHANGE COLUMN `{f}` `{f}` {t2.Fields[f]}");
                }
            }

            // Indices
            foreach (var idx in t1.Indices.Keys.Except(t2.Indices.Keys))
            {
                tableAlters.Add($"DROP INDEX `{idx}`");
            }
            foreach (var idx in t2.Indices.Keys.Except(t1.Indices.Keys))
            {
                tableAlters.Add($"ADD {t2.Indices[idx]}");
            }
            foreach (var idx in t1.Indices.Keys.Intersect(t2.Indices.Keys))
            {
                if (!NormalizeDef(t1.Indices[idx]).Equals(NormalizeDef(t2.Indices[idx]), StringComparison.OrdinalIgnoreCase))
                {
                    tableAlters.Add($"DROP INDEX `{idx}`");
                    tableAlters.Add($"ADD {t2.Indices[idx]}");
                }
            }

            // Primary Key
            if (t1.PrimaryKey != t2.PrimaryKey)
            {
                if (t1.PrimaryKey != null)
                    tableAlters.Add("DROP PRIMARY KEY");

                if (t2.PrimaryKey != null)
                    tableAlters.Add($"ADD {t2.PrimaryKey}");
            }

            // Options (Engine, Charset, etc.)
            if (NormalizeOptions(t1.Options) != NormalizeOptions(t2.Options) && t2.Options is not null)
                tableAlters.Add(t2.Options);

            if (tableAlters.Count != 0)
            {
                sb.AppendLine($"ALTER TABLE `{tableName}`");
                for (int i = 0; i < tableAlters.Count; i++)
                {
                    sb.Append("  " + tableAlters[i]);
                    sb.AppendLine(i < tableAlters.Count - 1 ? "," : ";");
                }
            }
        }

        return sb.ToString();
    }

    private static string NormalizeDef(string def)
    {
        if (string.IsNullOrEmpty(def))
            return "";

        def = NormalizedDefRegex().Replace(def, " ").Trim();
        return def.ToUpperInvariant();
    }

    private static string NormalizeOptions(string? options)
    {
        if (string.IsNullOrEmpty(options))
            return "";

        options = AutoIncrementRegex().Replace(options, "");
        options = OptionsRegex().Replace(options, " ").Trim();
        return options.ToUpperInvariant();
    }

    public static void ApplyDiff(Database target, string diffs)
    {
        if (target.SourceType != "db")
            throw new ArgumentException($"\nCannot apply changes: {target.Name} is not a database.");

        Log("Applying changes...");
        // var argsList = target.GetAuthArgs();
        // argsList.Add(target.Name);

        // try
        // {
        //     var psi = new ProcessStartInfo
        //     {
        //         FileName = "mysql",
        //         RedirectStandardInput = true,
        //         RedirectStandardOutput = true,
        //         RedirectStandardError = true,
        //         UseShellExecute = false,
        //         CreateNoWindow = true
        //     };

        //     foreach (var arg in argsList)
        //         psi.ArgumentList.Add(arg);

        //     using var process = Process.Start(psi);
        //     if (process is null)
        //         throw new Exception("Failed to start mysql process.");

        //     process.StandardInput.Write(diffs);
        //     process.StandardInput.Close();

        //     process.WaitForExit();
        //     string err = process.StandardError.ReadToEnd();
        //     if (process.ExitCode != 0)
        //         throw new Exception($"Command 'mysql' exited with code {process.ExitCode}\n{err}");
        // }
        // catch (Exception ex)
        // {
        //     throw new Exception($"Failed to apply changes: {ex.Message}");
        // }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex NormalizedDefRegex();
    [GeneratedRegex(@"AUTO_INCREMENT=\d+\s*", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex AutoIncrementRegex();
    [GeneratedRegex(@"\s+")]
    private static partial Regex OptionsRegex();
}