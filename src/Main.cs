using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.ServiceProcess;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Management;

delegate string FieldSetter(string value);

partial class Program
{
    private const string LOG_FILE = "svc.log";
    private const string CONF_FILE = "svc.conf";
    private static string lastLineFile = "lastline.log";
}

partial class Program
{
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

    public static ServiceController GetServiceController(string name)
    {
        foreach (var sc in ServiceController.GetServices())
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

    private static readonly Dictionary<string, FieldSetter> setterDict
        = new Dictionary<string, FieldSetter>
    {
        { "setServiceName", SetServiceName },
        { "setWorker", SetWorker },
        { "setWorkingDir", SetWorkingDir },
        { "setOutFileDir", SetOutFileDir },
        { "setWorkerEncoding", SetWorkerEncoding },
        { "setDomain", SetDomain },
        { "setUser", SetUser },
        { "setPassword", SetPassword },
        { "setDescription", SetDescription },
        { "setDisplayName", SetDisplayName }
    };

    private static string SetServiceName(string value)
    {
        serviceName = value;

        if (serviceName.Length == 0)
        {
            return "empty";
        }

        if (serviceName.Contains("\""))
        {
            return "contains \"";
        }

        return null;
    }

    private static string SetWorker(string value)
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
            return null;
        }

        var ii = worker.IndexOf(' ');
        if (ii == -1)
        {
            fileName = worker;
            arguments = "";
            return null;
        }

        fileName = worker.Substring(0, ii);
        arguments = worker.Substring(ii + 1).TrimStart();
        return null;
    }

    private static string SetWorkingDir(string value)
    {
        workingDir = value;
        if (!Directory.Exists(workingDir))
        {
            return "directory not exists";
        }

        return null;
    }

    private static string SetOutFileDir(string value)
    {
        outFileDir = value;
        if (!Directory.Exists(outFileDir))
        {
            return "directory not exists";
        }

        return null;
    }

    private static string SetWorkerEncoding(string value)
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

    private static string SetDomain(string value)
    {
        domain = value;

        if (value.Contains("\""))
        {
            return "contains \"";
        }

        return null;
    }

    private static string SetUser(string value)
    {
        user = value;

        if (value.Contains("\""))
        {
            return "contains \"";
        }

        return null;
    }

    private static string SetPassword(string value)
    {
        password = value;

        if (value.Contains("\""))
        {
            return "contains \"";
        }

        return null;
    }

    private static string SetDescription(string value)
    {
        description = value;

        if (value.Contains("\""))
        {
            return "contains \"";
        }

        return null;
    }

    private static string SetDisplayName(string value)
    {
        displayName = value;

        if (value.Contains("\""))
        {
            return "contains \"";
        }

        return null;
    }

    private static string ReadSvcConfig()
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

    private static void ShowConfig()
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
    private static bool testMode = false;

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ServiceBase.Run(new SimpleService(OnStart, OnStop));
            return 0;
        }

        string op = args[0];

        if (op == "version" || op == "--version" || op == "-v")
        {
            Print("Easy Service: v1.0.2");
            return 0;
        }

        var err = ReadSvcConfig();
        if (err != null)
        {
            Log($"Configuration Error!\n{err}", false);
            Print(err);
            return 1;
        }

        var sc = GetServiceController(serviceName);
        var status = (sc == null) ? "not installed" : sc.Status.ToString().ToLower();

        if (op == "check" || op == "status")
        {
            ShowConfig();
            Print($"\nService status: {status}");
            return 0;
        }

        if (op == "test-worker")
        {
            testMode = true;
            OnStart();
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
                Print($"Service \"{serviceName}\" is already installed!");
                return 1;
            }

            var scArgs = $"create \"{serviceName}\" binPath= \"{BinPath}\" start= auto "
                + $"DisplayName= \"{displayName}\"";

            if (user.Length > 0)
            {
                var obj = (domain.Length > 0) ? $"{domain}\\{user}" : user;
                scArgs = $"{scArgs} obj= \"{obj}\" password= \"{password}\"";
            }

            string cwd = Path.GetFullPath(".");
            Exec("sc", scArgs);
            Exec("sc", $"description \"{serviceName}\" \"{description} @<{cwd}>\"");

            if ((sc = GetServiceController(serviceName)) == null)
            {
                Print($"Failed to install Service \"{serviceName}\"");
                return 1;
            }

            var ss = $"Installed Service \"{serviceName}\"";
            Print(ss);
            Log(ss, false);
            Log("");

            StartService(sc);
            return 0;
        }

        if (sc == null && (op == "remove" || op == "start" || op == "stop" || op == "restart"))
        {
            Print($"Service \"{serviceName}\" is not installed!");
            return 1;
        }

        if (op == "remove")
        {
            StopService(sc);
            Exec("sc", $"delete \"{serviceName}\"");
            if (GetServiceController(serviceName) != null)
            {
                Print($"Failed to remove Service \"{serviceName}\"");
                return 1;
            }

            var ss = $"Remove Service \"{serviceName}\"";
            Log("");
            Log(ss);
            Print(ss);
            return 0;
        }

        if (op == "start")
        {
            return StartService(sc);
        }

        if (op == "stop")
        {
            return StopService(sc);
        }

        if (op == "restart")
        {
            StopService(sc);
            return StartService(sc);
        }

        Print("Usage: svc check|test-worker|install|start|stop|restart|remove");
        return 1;
    }

    public static void Log(string s, bool append = true)
    {
        if (testMode)
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

    private static int StartService(ServiceController sc)
    {
        if (sc.Status == ServiceControllerStatus.Running)
        {
            Print($"Service \"{sc.ServiceName}\" is already started");
            return 1;
        }

        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running);
        sc.Refresh();
        Print($"Started Service \"{sc.ServiceName}\"");
        return 0;
    }

    private static int StopService(ServiceController sc)
    {
        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            Print($"Service \"{sc.ServiceName}\" is already stopped");
            return 1;
        }

        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped);
        sc.Refresh();
        Print($"Stopped Service \"{sc.ServiceName}\"");
        return 0;
    }
}

partial class Program
{
    private static ProcessStartInfo psi = null;
    private static object procLock = null;
    private static Process proc = null;

    private static void OnStart()
    {
        var err = ReadSvcConfigInSvcBin();
        if (err != null)
        {
            err = $"Failed to read configuration for service \"{serviceName}\": {err}";
            Log($"[ERROR] {err}");
            throw new Exception(err);
        }

        try
        {
            psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = !testMode,
                StandardErrorEncoding = workerEncodingObj,
                StandardOutputEncoding = workerEncodingObj
            };
        }
        catch (Exception ex)
        {
            err = $"Failed to create process start info for service \"{serviceName}\": {ex.Message}";
            Log($"[ERROR] {err}");
            throw new Exception(err);
        }

        procLock = new object();

        proc = StartWorker();
        if (proc == null)
        {
            err = $"Failed to start worker for service \"{serviceName}\"";
            Log($"[ERROR] {err}");
            throw new Exception(err);
        }

        Log($"[INFO] Started Service \"{serviceName}\"");
    }

    private static string ReadSvcConfigInSvcBin()
    {
        if (testMode)
        {
            return null;
        }

        SetCwd(Path.GetDirectoryName(BinPath));

        var mngObj = GetServiceManagementObject();
        if (mngObj == null)
        {
            return "can not get service management object";
        }        
        
        try
        {
            var svcDescription = mngObj["Description"].ToString();
            var i = svcDescription.LastIndexOf('<') + 1;
            var n = svcDescription.Length - 1 - i;
            var cwd = svcDescription.Substring(i, n);
            SetCwd(cwd);
        }
        catch (Exception ex)
        {
            return $"can not set cwd from service's description, {ex.Message}";
        }

        var err = ReadSvcConfig();
        if (err != null)
        {
            return $"configuration error, {err}";
        }

        return null;
    }

    private static ManagementObject GetServiceManagementObject()
    {
        // https://stackoverflow.com/questions/1841790/how-can-a-windows-service-determine-its-servicename

        int processId = Process.GetCurrentProcess().Id;
        string query = $"SELECT * FROM Win32_Service where ProcessId = {processId}";
        using (var searcher = new ManagementObjectSearcher(query))
        {
            foreach (ManagementObject queryObj in searcher.Get())
            {
                return queryObj;
            }
        }

        return null;
    }

    private static Process StartWorker()
    {
        Process p = null;
        try
        {
            p = new Process
            {
                StartInfo = psi
            };

            p.OutputDataReceived += OutPut;
            p.ErrorDataReceived += OutPut;

            if (!testMode)
            {
                p.Exited += OnWorkerExitWithoutBeNotified;
                p.EnableRaisingEvents = true;
            }

            p.Start();
            p.BeginErrorReadLine();
            p.BeginOutputReadLine();

            Log($"[INFO] Created Worker `{worker}` in `{workingDir}`");
            return p;
        }
        catch (Exception e)
        {
            Log($"[ERROR] Failed to create Worker `{worker}` in `{workingDir}`\n{e}");
            if (p != null)
            {
                p.Close();
            }

            return null;
        }
    }

    private static void OnWorkerExitWithoutBeNotified(object sender, EventArgs e)
    {
        lock (procLock)
        {
            if (proc == null)
            {
                return;
            }

            var code = proc.ExitCode;
            Log($"[WARN] Worker exited with out be notified (code {code})");
            if (code == 0)
            {
                proc = null;
                return;
            }

            Log($"[INFO] Recreate Worker after {restartWaitSeconds} seconds");
        }

        Thread.Sleep(restartWaitSeconds * 1000);

        lock (procLock)
        {
            if (proc == null)
            {
                return;
            }

            proc = StartWorker();
        }
    }

    private static void OnStop()
    {
        lock (procLock)
        {
            var p = proc;
            proc = null;
            StopWorker(p);
            Log($"[INFO] Stopped Service \"{serviceName}\"");
        }
    }

    private static void StopWorker(Process p)
    {
        if (p == null)
        {
            return;
        }

        if (p.HasExited)
        {
            Log($"[WARN] Worker exited with out be notified (code {p.ExitCode}).");
            return;
        }

        p.Exited -= OnWorkerExitWithoutBeNotified;

        try
        {
            p.StandardInput.Write("exit\r\n");
            p.StandardInput.Flush();
            Log("[INFO] Notified Worker to exit");
        }
        catch (Exception e)
        {
            Log($"[ERROR] Failed to notify Worker to exit, {e.Message}");
            try
            {
                p.Kill();
                Log("[WARN] Worker has been killed");
            }
            catch (Exception ee)
            {
                Log($"[ERROR] Failed to kill Worker, {ee.Message}");
            }
            return;
        }

        if (!p.WaitForExit(waitSeconds * 1000))
        {
            Log("[WARN] Worker refused to exit");
            try
            {
                p.Kill();
                Log("[WARN] Worker has been killed");
            }
            catch (Exception ee)
            {
                Log($"[ERROR] Failed to kill Worker, {ee.Message}");
            }
            return;
        }

        Log($"[INFO] Worker exited with code {p.ExitCode}");
    }

    private static readonly object outLock = new object();

    private static void OutPut(object sender, DataReceivedEventArgs ev)
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
            if (testMode)
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
                Log($"[ERROR] Failed to write Worker's output to `{outFile}`, {e.Message}");
            }
            try
            {
                WriteLineToFile(lastLineFile, s, false);
            }
            catch (Exception e)
            {
                Log($"[ERROR] Failed to write Worker's output to `{lastLineFile}`, {e.Message}");
            }
        }
    }
}

class SimpleService : ServiceBase
{
    private readonly Action onStart;
    private readonly Action onStop;

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