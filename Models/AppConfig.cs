namespace MySqlDiffSharp.Models;

public class AppConfig
{
    public bool ShowHelp { get; set; }
    public bool KeepOldTables { get; set; }
    public bool Apply { get; set; }
    public bool BatchApply { get; set; }
    public List<string> PositionalArgs { get; set; } = [];
    public Dictionary<string, string> AuthArgs { get; set; } = [];

    public AppConfig(string[] args)
    {
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.StartsWith('-'))
            {
                var parts = arg.Split('=', 2);
                string key = parts[0];

                string? val;
                if (parts.Length > 1) // if user used '='
                    val = parts[1];
                else if (i + 1 < args.Length && !args[i + 1].StartsWith('-')) // user did not use '='
                    val = args[++i];
                else
                    val = null;

                if (key == "--help")
                    ShowHelp = true;
                else if (key == "--keep-old-tables")
                    KeepOldTables = true;
                else if (key == "--apply")
                    Apply = true;
                else if (key == "--batch-apply")
                    BatchApply = true;
                else if (val is null)
                    throw new ArgumentNullException($"Argument {key} did not had a valid value set");
                else if (ParsePossibleAuthArg(key, val, "host", "-h", "--host"))
                    continue;
                else if (ParsePossibleAuthArg(key, val, "user", "-u", "--user"))
                    continue;
                else if (ParsePossibleAuthArg(key, val, "password", "-p", "--password"))
                    continue;
                else if (ParsePossibleAuthArg(key, val, "port", "-P", "--port"))
                    continue;
                else if (ParsePossibleAuthArg(key, val, "socket", "-S", "--socket"))
                    continue;
                else
                    throw new ArgumentException($"Argument {key} is unknown");
            }
            else
            {
                positional.Add(arg);
            }
        }

        PositionalArgs = positional;
    }

    private bool ParsePossibleAuthArg(string key, string val, string name, string shortKey, string longKey)
    {
        if (key == shortKey || key == longKey)
        {
            AuthArgs.Add(name, val);
            AuthArgs.Add(name + '1', val);
            AuthArgs.Add(name + '2', val);
            return true;
        }

        if (key == shortKey + '1' || key == longKey + '1')
        {
            if (AuthArgs.TryAdd(name + '1', val) is false)
                AuthArgs[name + '1'] = val;

            return true;
        }

        if (key == shortKey + '2' || key == longKey + '2')
        {
            if (AuthArgs.TryAdd(name + '2', val) is false)
                AuthArgs[name + '2'] = val;

            return true;
        }

        return false;
    }


    public string? GetAuth(string key, int dbNum)
    {
        if (AuthArgs.TryGetValue(key + dbNum, out var val) && val is not null)
            return val;

        if (AuthArgs.TryGetValue(key, out val))
            return val;

        return null;
    }
}