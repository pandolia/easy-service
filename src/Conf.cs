using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class Conf
{
    public const string CONF_FILE = "svc.conf";

    public const string LOG_FILE = "svc.log";

    public const string LAST_LINE_FILE = "lastline.log";

    public const int RESTART_WAIT_SECONDS = 5;

    public string ServiceName { get; private set; } = null;

    public string DisplayName { get; private set; } = null;

    public string Description { get; private set; } = null;

    public string Dependencies { get; private set; } = null;

    public string Worker { get; private set; } = null;

    public string WorkingDir { get; private set; } = null;

    public string OutFileDir { get; private set; } = null;

    public int WaitSecondsForWorkerToExit { get; private set; } = 10;

    public string WorkerEncoding { get; private set; } = null;

    public string Domain { get; private set; } = null;

    public string User { get; private set; } = null;

    public string Password { get; private set; } = null;

    public string LastLineFile { get; private set; } = null;

    public string WorkerFileName { get; private set; } = null;

    public string WorkerArguments { get; private set; } = null;

    public Encoding WorkerEncodingObj { get; private set; } = null;

    private readonly Dictionary<string, FieldSetter> setterDict;

    public Conf()
    {
        setterDict = new Dictionary<string, FieldSetter>
        {
            { "setServiceName", SetServiceName },
            { "setDisplayName", SetDisplayName },
            { "setDescription", SetDescription },
            { "setDependencies", SetDependencies },
            { "setWorker", SetWorker },
            { "setWorkingDir", SetWorkingDir },
            { "setOutFileDir", SetOutFileDir },
            { "setWaitSecondsForWorkerToExit", SetWaitSecondsForWorkerToExit },
            { "setWorkerEncoding", SetWorkerEncoding },
            { "setDomain", SetDomain },
            { "setUser", SetUser },
            { "setPassword", SetPassword }
        };
    }

    public string Read()
    {
        var err = Libs.ReadConfig(CONF_FILE, setterDict);
        if (err != null)
        {
            return err;
        }

        WorkingDir = Path.GetFullPath(WorkingDir);
        OutFileDir = Path.GetFullPath(OutFileDir);
        LastLineFile = Path.Combine(OutFileDir, LAST_LINE_FILE);

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

        return null;
    }

    public void ShowConfig()
    {
        Libs.Print($"ServiceName: {ServiceName}");
        Libs.Print($"DisplayName: {DisplayName}"); 
        Libs.Print($"Description: {Description}");
        Libs.Print($"Dependencies: {Dependencies}");
        Libs.Print($"Worker: {Worker}");
        Libs.Print($"Worker's fileName: {WorkerFileName}");
        Libs.Print($"Worker's arguments: {WorkerArguments}"); 
        Libs.Print($"WorkingDir: {WorkingDir}");
        Libs.Print($"OutFileDir: {OutFileDir}");
        Libs.Print($"WaitSecondsForWorkerToExit: {WaitSecondsForWorkerToExit}");
        Libs.Print($"WorkerEncoding: {WorkerEncoding}");
        Libs.Print($"Domain: {Domain}");
        Libs.Print($"User: {User}");
        Libs.Print($"Password: {Password}");
    }

    public string SetServiceName(string value)
    {
        ServiceName = value;

        if (ServiceName.Length == 0)
        {
            return "empty";
        }

        if (ServiceName.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    public string SetDisplayName(string value)
    {
        DisplayName = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }    

    public string SetDescription(string value)
    {
        Description = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    public string SetDependencies(string value)
    {
        Dependencies = value.Replace(',', '/');

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    public string SetWorker(string value)
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

    public string SetWorkingDir(string value)
    {
        WorkingDir = value;
        if (!Directory.Exists(WorkingDir))
        {
            return "directory not exists";
        }

        return null;
    }

    public string SetOutFileDir(string value)
    {
        OutFileDir = value;
        if (!Directory.Exists(OutFileDir))
        {
            return "directory not exists";
        }

        return null;
    }

    public string SetWaitSecondsForWorkerToExit(string value)
    {
        int val;

        if (!int.TryParse(value, out val) || val < 0 || val > 300)
        {
            return "should be a number between 0 ~ 300";
        }

        WaitSecondsForWorkerToExit = val;
        return null;
    }

    public string SetWorkerEncoding(string value)
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

    public string SetDomain(string value)
    {
        Domain = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    public string SetUser(string value)
    {
        User = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }

    public string SetPassword(string value)
    {
        Password = value;

        if (value.Contains("\""))
        {
            return "contains '\"'";
        }

        return null;
    }
}
