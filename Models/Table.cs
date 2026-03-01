using System.Text.RegularExpressions;

namespace SqlDiffSharp.Models;

public partial class Table
{
    public string? Name { get; set; }
    public string OriginalDef { get; set; }
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Indices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PrimaryKey { get; set; }
    public string? Options { get; set; }

    public Table(string def)
    {
        OriginalDef = def.Trim();
        Parse();
    }

    private void Parse()
    {
        var lines = OriginalDef.Split('\n');
        if (lines.Length == 0)
            return;

        // Match CREATE TABLE Name (
        var match = CreateTableRegex().Match(lines[0]);
        if (!match.Success)
            return;

        Name = match.Groups[1].Value.Trim();

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            // Remove trailing comma for parsing
            if (line.EndsWith(',')) line = line[..^1].Trim();

            // End of table definition
            var endMatch = EndOfTableRegex().Match(line);
            if (endMatch.Success)
            {
                Options = endMatch.Groups[1].Value.Trim();
                break;
            }

            // Indices
            var keyMatch = KeyIndexesRegex().Match(line);
            if (keyMatch.Success)
            {
                string keyType = keyMatch.Groups[1].Value.ToUpperInvariant();
                string keyDef = keyMatch.Groups[2].Value.Trim();

                if (keyType == "PRIMARY KEY")
                {
                    PrimaryKey = $"PRIMARY KEY {keyDef}".Trim();
                }
                else
                {
                    // Extract index name
                    var idxNameMatch = IndexNameSelectRegex().Match(keyDef);
                    if (idxNameMatch.Success)
                    {
                        string idxName = idxNameMatch.Groups[1].Value;
                        Indices[idxName] = $"{keyType} `{idxName}` {idxNameMatch.Groups[2].Value}".Trim();
                    }
                }
                continue;
            }

            // Fields
            var fieldMatch = FieldSelectRegex().Match(line);
            if (fieldMatch.Success)
            {
                string fieldName = fieldMatch.Groups[1].Value;
                string fieldDef = fieldMatch.Groups[2].Value;
                Fields[fieldName] = fieldDef;
            }
        }
    }

    [GeneratedRegex(@"^(\S+)\s+(.*)")]
    private static partial Regex FieldSelectRegex();
    [GeneratedRegex(@"^(\S+)\s*(.*)")]
    private static partial Regex IndexNameSelectRegex();
    [GeneratedRegex(@"^(PRIMARY\s+KEY|UNIQUE\s+KEY|FULLTEXT\s+KEY|KEY)\s*(.*)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex KeyIndexesRegex();
    [GeneratedRegex(@"^\)\s*(.*?);?$")]
    private static partial Regex EndOfTableRegex();
    [GeneratedRegex(@"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(\S+)\s*\(", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CreateTableRegex();
}
