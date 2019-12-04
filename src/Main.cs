using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.ServiceProcess;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;

delegate string FieldSetter(string value);

partial class Program
{
    private const string LOG_FILE = "svc.log";
    private const string CONF_FILE = "svc.conf";
    private static string lastLineFile = "lastline.log";
}

partial class Program
{
    public static string BinDir = AppDomain.CurrentDomain.BaseDirectory;

    public static string BinPath = Assembly.GetExecutingAssembly().Location;

    public static void SetCwd(string d)
    {
        Directory.SetCurrentDirectory(d);
    }

    public static void Print(object s)
    {
        Console.WriteLine(s);
    }

    public static void WriteLineToFile(string file, string s, bool append = false)
    {
        using (var sw = new StreamWriter(file, append))
        {
            sw.WriteLine(s);
            sw.Flush();
        }
    }

    public static ServiceController GetService(string name)
    {
        var scList = ServiceController.GetServices();
        foreach (var sc in scList)
        {
            if (sc.ServiceName == name)
            {
                return sc;
            }
        }
        return null;
    }

    public static int Exec(string file, string args, string dir = null)
    {
        try
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = file;
                p.StartInfo.Arguments = args;
                p.StartInfo.WorkingDirectory = dir;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    Print(p.StandardOutput.ReadToEnd());
                }
                return p.ExitCode;
            }
        }
        catch (Exception e)
        {
            Print(e.Message);
            return 1;
        }
    }

    private static List<string> ReadConfig(string file, Dictionary<string, FieldSetter> setterDict)
    {
        var errs = new List<string>();
        string text;
        try
        {
            using (var sr = new StreamReader(file))
            {
                text = sr.ReadToEnd();
            }
        }
        catch (Exception e)
        {
            errs.Add($"Falied to read config from `{CONF_FILE}`, {e.Message}");
            return errs;
        }

        var items = new Dictionary<string, string>();

        foreach (var line in text.Split('\n'))
        {
            var s = line.Replace('\t', ' ').Trim();
            if (s.Length == 0 || s[0] == '#')
            {
                continue;
            }
            var i = s.IndexOf(':');
            var key = (i == -1) ? s : s.Substring(0, i).TrimEnd();
            var sKey = "set" + key;
            if (!setterDict.ContainsKey(sKey))
            {
                errs.Add($"Invalid configuration key: `{key}`");
                continue;
            }

            var value = (i == -1) ? "" : s.Substring(i + 1).TrimStart();
            var valueErr = setterDict[sKey](value);
            if (valueErr != null)
            {
                errs.Add($"Wrong {key}: `{value}`, {valueErr}");
            }

            items[key] = value;
        }

        foreach (var sKey in setterDict.Keys)
        {
            var key = sKey.Substring(3);
            if (items.ContainsKey(key))
            {
                continue;
            }
            var valueErr = setterDict[sKey]("");
            if (valueErr != null)
            {
                errs.Add($"{key} must be provided!");
            }
        }

        return errs;
    }
}

partial class Program
{
    private static string serviceName = null;
    private static string worker = null;
    private static string workingDir = null;
    private static string outFileDir = null;
    private static string workerEncoding = null;
    private static string domain = null;
    private static string user = null;
    private static string password = null;
    private static string description = null;
    private static string displayName = null;

    private static readonly int waitSeconds = 10;
    private static readonly int restartWaitSeconds = 5;

    private static string fileName = null;
    private static string arguments = null;
    private static Encoding workerEncodingObj = null;

    private static Dictionary<string, FieldSetter> setterDict = new Dictionary<string, FieldSetter>
    {
        { "setServiceName", setServiceName },
        { "setWorker", setWorker },
        { "setWorkingDir", setWorkingDir },
        { "setOutFileDir", setOutFileDir },
        { "setWorkerEncoding", setWorkerEncoding },
        { "setDomain", setDomain },
        { "setUser", setUser },
        { "setPassword", setPassword },
        { "setDescription", setDescription },
        { "setDisplayName", setDisplayName }
    };

    private static string removeOuterQuote(string value)
    {
        if (value[0] == '"' || value[0] == '\'')
        {
            char quote = value[0];
            var i = value.IndexOf(quote, 1);
            if (i == value.Length - 1)
            {
                return value.Substring(1, value.Length - 2);
            }
        }
        return value;
    }

    private static string checkValid(string value)
    {
        char[] invalidFileNameChars =  Path.GetInvalidFileNameChars();

        foreach (char c in value)  
        {
            if (Array.IndexOf(invalidFileNameChars, c) >= 0)
            {
                return "contain invalid word ' \" | ~ # % & * { } \' : < > ? / + '";
            }
        }

        return null;
    }

    private static string setServiceName(string value)
    {
        serviceName = removeOuterQuote(value.Trim());

        if (serviceName.Length == 0)
        {
            return "empty";
        }

        return checkValid(serviceName);
    }

    private static string setWorker(string value)
    {
        worker = value;

        if (worker.Length == 0)
        {
            return "emtpy";
        }

        if (worker[0] == '"')
        {
            var i = worker.IndexOf('"', 1);
            if (i == -1)
            {
                return "bad format";
            }
            fileName = worker.Substring(1, i - 1).Trim();
            if (fileName.Length == 0)
            {
                return "bad format";
            }
            arguments = worker.Substring(i + 1).TrimStart();
            return checkValid(fileName);
        }

        var ii = worker.IndexOf(' ');
        if (ii == -1)
        {
            fileName = worker;
            arguments = "";
            return checkValid(fileName);
        }

        fileName = worker.Substring(0, ii);
        arguments = worker.Substring(ii + 1).TrimStart();
        return checkValid(fileName);
    }

    private static string setWorkingDir(string value)
    {
        workingDir = value;
        if (!Directory.Exists(workingDir))
        {
            return "directory not exists";
        }
        return null;
    }

    private static string setOutFileDir(string value)
    {
        outFileDir = value;
        if (!Directory.Exists(outFileDir))
        {
            return "directory not exists";
        }
        return null;
    }

    private static string setWorkerEncoding(string value)
    {
        workerEncoding = value;
        value = value.ToLower();
        if (value == "" || value == "null")
        {
            workerEncoding = "null";
            workerEncodingObj = null;
            return null;
        }
        if (value.StartsWith("utf") && !value.StartsWith("utf-"))
        {
            value = "utf-" + value.Substring(3);
        }
        try
        {
            workerEncodingObj = Encoding.GetEncoding(value);
        }
        catch (Exception e)
        {
            return e.Message;
        }
        return null;
    }

    private static string setDomain(string value)
    {
        domain = value;
        return null;
    }

    private static string setUser(string value)
    {
        user = value;
        return null;
    }

    private static string setPassword(string value)
    {
        password = value;
        return null;
    }

    private static string setDescription(string value)
    {
        description = removeOuterQuote(value.Trim());
        return checkValid(description);
    }

    private static string setDisplayName(string value)
    {
        displayName = removeOuterQuote(value.Trim());
        return checkValid(displayName);
    }

    private static string readConfig()
    {
        var errs = ReadConfig(CONF_FILE, setterDict);
        if (errs.Count > 0)
        {
            return string.Join("\n", errs);
        }

        workingDir = Path.GetFullPath(workingDir);
        outFileDir = Path.GetFullPath(outFileDir);
        lastLineFile = Path.Combine(outFileDir, lastLineFile);

        var suffixes = new string[] { "", ".exe", ".bat" };
        foreach (var suffix in suffixes)
        {
            if (suffix.Length > 0 && fileName.EndsWith(suffix))
            {
                continue;
            }
            var realName = Path.Combine(workingDir, fileName + suffix);
            if (File.Exists(realName))
            {
                fileName = realName;
                break;
            }
        }

        return null;
    }

    private static void showConfig()
    {
        Print($"ServiceName: {serviceName}");
        Print($"DisplayName: {displayName}");
        Print($"Description: {description}");
        Print($"Worker: {worker}");
        Print($"Worker's fileName: {fileName}");
        Print($"Worker's arguments: {arguments}");
        Print($"WorkingDir: {workingDir}");
        Print($"OutFileDir: {outFileDir}");
        Print($"WorkerEncoding: {workerEncoding}");
        Print($"Domain: {domain}");
        Print($"User: {user}");
        Print($"Password: {password}");
    }
}

partial class Program
{
    private static string op = null;

    public static int Main(string[] args)
    {
        SetCwd(BinDir);

        var err = readConfig();
        if (err != null)
        {
            log($"Configuration Error!\n{err}", false);
            Print(err);
            return 1;
        }

        string name = $"\"{serviceName}\"";
        string serviceDescription = $"\"{description}\"";
        var sc = GetService(serviceName);
        var status = (sc == null) ? "not installed" : sc.Status.ToString().ToLower();
        op = args.Length > 0 ? args[0] : op;

        if (op == "check")
        {
            showConfig();
            Print($"\nService status: {status}");
            return 0;
        }

        if (op == "test-worker")
        {
            onStart();
            if (proc == null)
            {
                return 1;
            }
            proc.WaitForExit();
            return proc.ExitCode;
        }

        if (op == "install")
        {
            if (sc != null)
            {
                Print($"Service {name} is already installed!");
                return 1;
            }
            var _args = $"create {name} binPath= \"{BinPath}\" start= auto "
                + $"DisplayName= \"{displayName}\"";
            if (user.Length > 0)
            {
                var obj = (domain.Length > 0) ? $"{domain}\\{user}" : user;
                _args = $"{_args} obj= \"{obj}\" password= \"{password}\"";
            }
            Exec("sc", _args);
            var _argsDescription = $"description {name} {serviceDescription}";
            Exec("sc", _argsDescription);
            if ((sc = GetService(serviceName)) == null)
            {
                Print($"Failed to install Service {name}");
                return 1;
            }
            var ss = $"Installed Service {name}";
            Print(ss);
            log(ss, false);
            log("");
            startService(sc, name);
            return 0;
        }

        if (sc == null && (op == "remove" || op == "start" || op == "stop" || op == "restart"))
        {
            Print($"Service {name} is not installed!");
            return 1;
        }

        if (op == "remove")
        {
            stopService(sc, name);
            Exec("sc", $"delete {name}");
            if (GetService(serviceName) != null)
            {
                Print($"Failed to remove Service {name}");
                return 1;
            }
            var ss = $"Remove Service {name}";
            log("");
            log(ss);
            Print(ss);
            return 0;
        }

        if (op == "start")
        {
            return startService(sc, name);
        }

        if (op == "stop")
        {
            return stopService(sc, name);
        }

        if (op == "restart")
        {
            stopService(sc, name);
            return startService(sc, name);
        }

        if (op != null || status != "startpending")
        {
            Print("Usage: svc check|test-worker|install|start|stop|restart|remove");
            return 1;
        }

        run();
        return 0;
    }

    private static void log(string s, bool append = true)
    {
        if (op == "test-worker")
        {
            if (s.StartsWith("[ERROR]") || s.StartsWith("[WARN]"))
            {
                Print(s);
            }
            return;
        }
        try
        {
            s = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {s}";
            WriteLineToFile(LOG_FILE, s, append);
        }
        catch (Exception e)
        {
            Print(e.Message);
        }
    }

    private static int startService(ServiceController sc, string name)
    {
        if (sc.Status == ServiceControllerStatus.Running)
        {
            Print($"Service {name} is already started");
            return 1;
        }
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running);
        sc.Refresh();
        Print($"Started Service {name}");
        return 0;
    }

    private static int stopService(ServiceController sc, string name)
    {
        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            Print($"Service {name} is already stopped");
            return 1;
        }
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped);
        sc.Refresh();
        Print($"Stopped Service {name}");
        return 0;
    }
}

partial class Program
{
    private static ProcessStartInfo psi = null;
    private static object procLock = null;
    private static Process proc = null;

    private static void run()
    {
        var service = new SimpleService(onStart, onStop);
        ServiceBase.Run(service);
    }

    private static void onStart()
    {
        log($"[INFO] Starting service \"{serviceName}\"...");
        psi = new ProcessStartInfo();
        psi.FileName = fileName;
        psi.Arguments = arguments;
        psi.WorkingDirectory = workingDir;
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.RedirectStandardInput = (op != "test-worker");
        psi.StandardErrorEncoding = workerEncodingObj;
        psi.StandardOutputEncoding = workerEncodingObj;
        psi.StandardErrorEncoding = workerEncodingObj;
        procLock = new object();
        proc = startWorker();
        log($"[INFO] Started Service \"{serviceName}\"");
    }

    private static Process startWorker()
    {
        Process p = null;
        try
        {
            p = new Process();
            p.StartInfo = psi;
            p.OutputDataReceived += outPut;
            p.ErrorDataReceived += outPut;
            if (op != "test-worker")
            {
                p.Exited += onWorkerExitWithoutBeNotified;
                p.EnableRaisingEvents = true;
            }
            p.Start();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            log($"[INFO] Created Worker `{worker}` in `{workingDir}`");
            return p;
        }
        catch (Exception e)
        {
            log($"[ERROR] Failed to create Worker `{worker}` in `{workingDir}`\n{e}");
            if (p != null)
            {
                p.Close();
            }
            return null;
        }
    }

    private static void onWorkerExitWithoutBeNotified(object sender, EventArgs e)
    {
        lock (procLock)
        {
            if (proc == null)
            {
                return;
            }
            var code = proc.ExitCode;
            log($"[WARN] Worker exited with out be notified (code {code})");
            if (code == 0)
            {
                proc = null;
                return;
            }
            log($"[INFO] Recreate Worker after {restartWaitSeconds} seconds");
        }

        Thread.Sleep(restartWaitSeconds * 1000);

        lock (procLock)
        {
            if (proc == null)
            {
                return;
            }
            proc = startWorker();
        }
    }

    private static void onStop()
    {
        lock (procLock)
        {
            log($"[INFO] Stopping Service \"{serviceName}\"...");
            var p = proc;
            proc = null;
            stopWorker(p);
            log($"[INFO] Stopped Service \"{serviceName}\"");
        }
    }

    private static void stopWorker(Process p)
    {
        if (p == null)
        {
            return;
        }

        if (p.HasExited)
        {
            log($"[WARN] Worker exited with out be notified (code {p.ExitCode}).");
            return;
        }

        p.Exited -= onWorkerExitWithoutBeNotified;

        try
        {
            p.StandardInput.Write("exit\r\n");
            p.StandardInput.Flush();
            log("[INFO] Notified Worker to exit");
        }
        catch (Exception e)
        {
            log($"[ERROR] Failed to notify Worker to exit, {e.Message}");
            try
            {
                p.Kill();
                log("[WARN] Worker has been killed");
            }
            catch (Exception ee)
            {
                log($"[ERROR] Failed to kill Worker, {ee.Message}");
            }
            return;
        }

        if (!p.WaitForExit(waitSeconds * 1000))
        {
            log("[WARN] Worker refused to exit");
            try
            {
                p.Kill();
                log("[WARN] Worker has been killed");
            }
            catch (Exception ee)
            {
                log($"[ERROR] Failed to kill Worker, {ee.Message}");
            }
            return;
        }

        log($"[INFO] Worker exited with code {p.ExitCode}");
    }

    private static object outLock = new object();

    private static void outPut(object sender, DataReceivedEventArgs ev)
    {
        string s = ev.Data;

        if (s == null)
        {
            return;
        }

        int n = s.Length;
        if (n > 0 && s[n - 1] == '\0')
        {
            if (n == 1)
            {
                return;
            }
            s = s.Substring(0, n - 1);
        }

        s = s.TrimEnd();
        var outFile = Path.Combine(outFileDir, $"{DateTime.Now:yyyy-MM-dd}.log");

        lock (outLock)
        {
            if (op == "test-worker")
            {
                Print(s);
                return;
            }
            try
            {
                WriteLineToFile(outFile, s, true);
            }
            catch (Exception e)
            {
                log($"[ERROR] Failed to write Worker's output to `{outFile}`, {e.Message}");
            }
            try
            {
                WriteLineToFile(lastLineFile, s, false);
            }
            catch (Exception e)
            {
                log($"[ERROR] Failed to write Worker's output to `{lastLineFile}`, {e.Message}");
            }
        }
    }
}

class SimpleService : ServiceBase
{
    private Action onStart;
    private Action onStop;

    public SimpleService(Action onStart, Action onStop)
    {
        this.onStart = onStart;
        this.onStop = onStop;
    }

    protected override void OnStart(string[] args)
    {
        onStart();
    }

    protected override void OnStop()
    {
        onStop();
    }
}