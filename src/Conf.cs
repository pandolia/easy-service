using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class Conf
{
    public string ServiceName { get; private set; } = null;

    public string DisplayName { get; private set; } = null;

    public string Description { get; private set; } = null;

    public string Dependencies { get; private set; } = null;

    public string Worker { get; private set; } = null;

    public string WorkingDir { get; private set; } = null;

    public string OutFileDir { get; private set; } = null;

    public int WaitSecondsForWorkerToExit { get; private set; } = 0;

    public int MaxLogFilesNum { get; private set; } = 0;

    public string WorkerEncoding { get; private set; } = null;

    public int WorkerMemoryLimit { get; private set; } = -1;

    public string Domain { get; private set; } = null;

    public string User { get; private set; } = null;

    public string Password { get; private set; } = null;

    public string WorkerFileName { get; private set; } = null;

    public string WorkerArguments { get; private set; } = null;

    public Encoding WorkerEncodingObj { get; private set; } = null;

    public readonly Dictionary<string, string> Environments = new Dictionary<string, string>();

    public Conf(Action<string> abort = null)
    {
        var setterDict = new Dictionary<string, Func<string, string>>
        {
            { "setServiceName", SetServiceName },
            { "setDisplayName", SetDisplayName },
            { "setDescription", SetDescription },
            { "setDependencies", SetDependencies },
            { "setWorker", SetWorker },
            { "setEnvironments", SetEnvironments },
            { "setWorkingDir", SetWorkingDir },
            { "setOutFileDir", SetOutFileDir },
            { "setWaitSecondsForWorkerToExit", SetWaitSecondsForWorkerToExit },
            { "setMaxLogFilesNum", SetMaxLogFilesNum },
            { "setWorkerEncoding", SetWorkerEncoding },
            { "setWorkerMemoryLimit", SetWorkerMemoryLimit },
            { "setDomain", SetDomain },
            { "setUser", SetUser },
            { "setPassword", SetPassword }
        };

        var err = Libs.ReadConfig(Consts.CONF_FILE, setterDict);
        if (err != null)
        {
            abort = abort ?? Libs.Abort;
            abort($"Configuration Error: {err}");
        }

        ResolvePaths();

        if (DisplayName == null || DisplayName.Length == 0)
        {
            DisplayName = ServiceName;
        }
    }

    private void ResolvePaths()
    {
        WorkingDir = Path.GetFullPath(WorkingDir);
        OutFileDir = OutFileDir != null ? Path.GetFullPath(OutFileDir) : null;

        var suffixes = new string[] { "", ".exe", ".bat" };
        foreach (var suffix in suffixes)
        {
            if (suffix.Length > 0 && WorkerFileName.EndsWith(suffix))
            {
                continue;
            }

            var realName = Path.Combine(WorkingDir, WorkerFileName + suffix);
            if (File.Exists(realName))
            {
                WorkerFileName = realName;
                break;
            }
        }
    }

    public void ShowConfig()
    {
        var maxLogFilesNumString = (MaxLogFilesNum == 0) ? "" : $"{MaxLogFilesNum}";

        Console.WriteLine($"ServiceName: {ServiceName}");
        Console.WriteLine($"DisplayName: {DisplayName}"); 
        Console.WriteLine($"Description: {Description}");
        Console.WriteLine($"Dependencies: {Dependencies}");
        Console.WriteLine($"Worker: {Worker}");
        Console.WriteLine($"Worker's fileName: {WorkerFileName}");
        Console.WriteLine($"Worker's arguments: {WorkerArguments}");
        Console.WriteLine($"Worker's enrinonments: {Environments.ToPrettyString()}");
        Console.WriteLine($"WorkingDir: {WorkingDir}");
        Console.WriteLine($"OutFileDir: {OutFileDir}");
        Console.WriteLine($"WaitSecondsForWorkerToExit: {WaitSecondsForWorkerToExit}");
        Console.WriteLine($"MaxLogFilesNum: {maxLogFilesNumString} (WARN: This property is deprecated since v1.0.11)");
        Console.WriteLine($"WorkerEncoding: {WorkerEncoding}");
        Console.WriteLine($"WorkerMemoryLimit: {WorkerMemoryLimit}");
        Console.WriteLine($"Domain: {Domain}");
        Console.WriteLine($"User: {User}");
        Console.WriteLine($"Password: {Password}");
    }

    public static string CheckServiceName(string name)
    {
        if (name.Length == 0)
        {
            return "empty";
        }

        if (name.Contains('"'))
        {
            return "contains '\"'";
        }

        if (name.Contains('\\') || name.Contains('/'))
        {
            return "contains '\\' or '/'";
        }

        if (name.Contains('[') || name.Contains(']'))
        {
            return "contains '[' or ']'";
        }

        if (name.IsDigit())
        {
            return "is digit";
        }

        return null;
    }

    private string SetServiceName(string value)
    {
        ServiceName = value;
        return CheckServiceName(value);
    }

    private string SetDisplayName(string value)
    {
        DisplayName = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    private string SetDescription(string value)
    {
        Description = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    private string SetDependencies(string value)
    {
        Dependencies = value.Replace(',', '/');

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    private string SetWorker(string value)
    {
        Worker = value;

        if (Worker.Length == 0)
        {
            return "emtpy";
        }

        if (Worker[0] == '"')
        {
            var i = Worker.IndexOf('"', 1);
            if (i == -1)
            {
                return "bad format";
            }

            WorkerFileName = Worker.Substring(1, i - 1).Trim();
            if (WorkerFileName.Length == 0)
            {
                return "bad format";
            }

            WorkerArguments = Worker.Substring(i + 1).TrimStart();
            return null;
        }

        var ii = Worker.IndexOf(' ');
        if (ii == -1)
        {
            WorkerFileName = Worker;
            WorkerArguments = "";
            return null;
        }

        WorkerFileName = Worker.Substring(0, ii);
        WorkerArguments = Worker.Substring(ii + 1).TrimStart();
        return null;
    }

    private string SetEnvironments(string value)
    {
        foreach (var seg0 in value.Split(','))
        {
            var seg = seg0.Trim();
            var n = seg.Length;

            if (n == 0)
            {
                continue;
            }

            var i = seg.IndexOf('=');

            if (i == -1 || i == 0)
            {
                return "bad format";
            }

            var key = seg.Substring(0, i).TrimEnd();
            var val = (i == n - 1) ? "" : seg.Substring(i + 1, n - i - 1).TrimStart();

            Environments[key] = val;
        }

        return null;
    }

    private string SetWorkingDir(string value)
    {
        WorkingDir = value;
        if (!Directory.Exists(WorkingDir))
        {
            return "directory not exists";
        }

        return null;
    }

    private string SetOutFileDir(string value)
    {
        if (value == "$NULL")
        {
            OutFileDir = null;
            return null;
        }

        if (!Directory.Exists(value))
        {
            return "directory not exists";
        }

        if (!value.EndsWith("\\"))
        {
            value += "\\";
        }

        OutFileDir = value;

        return null;
    }

    private string SetWaitSecondsForWorkerToExit(string value)
    {
        if (value == "")
        {
            WaitSecondsForWorkerToExit = 0;
            return null;
        }

        if (!int.TryParse(value, out int val) || val < 0 || val > 300)
        {
            return "should be a number between 0 ~ 300";
        }

        WaitSecondsForWorkerToExit = val;
        return null;
    }

    private string SetMaxLogFilesNum(string value)
    {
        if (value == "")
        {
            MaxLogFilesNum = 0;
            return null;
        }

        if (!int.TryParse(value, out int val) || val < 2)
        {
            return "should be a number greater than or equals 2";
        }

        MaxLogFilesNum = val;
        return null;
    }

    private string SetWorkerEncoding(string value)
    {
        WorkerEncoding = value;

        value = value.ToLower();

        if (value == "" || value == "null")
        {
            WorkerEncoding = "null";
            WorkerEncodingObj = null;
            return null;
        }

        if (value.StartsWith("utf") && !value.StartsWith("utf-"))
        {
            value = "utf-" + value.Substring(3);
        }

        try
        {
            WorkerEncodingObj = Encoding.GetEncoding(value);
        }
        catch (Exception e)
        {
            return e.Message;
        }

        return null;
    }

    private string SetWorkerMemoryLimit(string value)
    {
        if (value == "")
        {
            WorkerMemoryLimit = -1;
            return null;
        }

        if (!int.TryParse(value, out int val) || val < 1 || val > 5000000)
        {
            return "should be a number between 1 ~ 5000000";
        }

        WorkerMemoryLimit = val;
        return null;
    }

    private string SetDomain(string value)
    {
        Domain = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    private string SetUser(string value)
    {
        User = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    private string SetPassword(string value)
    {
        Password = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }
}