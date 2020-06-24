using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

public partial class Program
{
    private const string VERSION = "Easy Service: v1.0.2";

    private const string COMMANDS = "create|check|status|test-worker|install|start|stop|restart|remove|log";

    private static readonly string USAGE = $"Usage: svc version|{COMMANDS} [$project_name]";

    private static bool testMode = false;

    private static readonly Conf conf = new Conf();

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ServiceBase.Run(new SimpleService());
            return 0;
        }

        string op = args[0];

        if (op == "version" || op == "--version" || op == "-v")
        {
            Libs.Print(VERSION);
            return 0;
        }

        if (!COMMANDS.Split('|').Contains(op) || (op == "create" && args.Length < 2))
        {
            Libs.Print(USAGE);
            return 1;
        }

        if (op == "create")
        {
            return CreateProject(args[1]);
        }

        var err = conf.Read();
        if (err != null)
        {
            Libs.Print($"Configuration Error:\n{err}");
            return 1;
        }

        if (op == "test-worker")
        {
            return TestWorker();
        }

        var sc = SvcUtils.GetServiceController(conf.ServiceName);
        var status = (sc == null) ? "not installed" : sc.Status.ToString().ToLower();

        if (op == "check" || op == "status")
        {
            conf.ShowConfig();
            Libs.Print($"\nService status: {status}");
            return 0;
        }

        if (op == "log")
        {
            if (status != "running")
            {
                Libs.Print($"Service \"{conf.ServiceName}\" is not running");
                return 1;
            }
            Libs.NewThread(LoggingSvc);
            Console.ReadLine();
            return 0;
        }

        if (op == "install")
        {
            if (sc != null)
            {
                Libs.Print($"Service \"{conf.ServiceName}\" is already installed!");
                return 1;
            }

            return InstallService(conf);
        }

        if (sc == null)
        {
            Libs.Print($"Service \"{conf.ServiceName}\" is not installed!");
            return 1;
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

        // op == "remove"
        return RemoveService(sc);
    }

    private static int CreateProject(string name)
    {
        var err = Libs.CopyDir($"{Libs.BinDir}..\\samples\\csharp-version", name, ".log");
        if (err != null)
        {
            Libs.Print(err);
            return 1;
        }

        Libs.Print($"Create an Easy-Service project in {name}");
        return 0;
    }

    private static int TestWorker()
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

    private static int InstallService(Conf conf)
    {
        ServiceController sc;

        if ((sc = SvcUtils.CreateSvc(conf)) == null)
        {
            Libs.Print($"Failed to install Service \"{conf.ServiceName}\"");
            return 1;
        }

        var ss = $"Installed Service \"{conf.ServiceName}\"";
        Libs.Print(ss);
        Log(ss, false);
        Log("");

        var err = SvcUtils.SetSvcDescription(conf);
        if (err != null)
        {
            err = $"Failed to set description for Service \"{conf.ServiceName}\": {err}";
            Libs.Print(err);
            Libs.Print("Please run `svc remove` to remove the Service");
            Log(err);
            return 1;
        }

        return StartService(sc);
    }

    private static int StartService(ServiceController sc)
    {
        if (sc.Status == ServiceControllerStatus.Running)
        {
            Libs.Print($"Service \"{sc.ServiceName}\" is already started");
            return 1;
        }

        Console.Write($"Starting...");
        Libs.NewThread(Waiting);
        sc.StartSvc();
        Libs.Print($"\nStarted Service \"{sc.ServiceName}\"");
        return 0;
    }

    private static void Waiting()
    {
        Thread.Sleep(2000);
        for (int i = 0; i < 6; i++)
        {
            Thread.Sleep(1500);
            Console.Write(".");
            Console.Out.Flush();
        }

        Console.Write($"\nFailed to start the service, "
            + $"please refer to svc.log or {Libs.BinDir}?.error.txt to see what happened\n");
        Environment.Exit(1);
    }

    private static int StopService(ServiceController sc)
    {
        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            Libs.Print($"Service \"{sc.ServiceName}\" is already stopped");
            return 1;
        }

        sc.StopSvc();
        Libs.Print($"Stopped Service \"{sc.ServiceName}\"");
        return 0;
    }

    private static int RemoveService(ServiceController sc)
    {
        StopService(sc);
        if (!SvcUtils.DeleteSvc(conf.ServiceName))
        {
            Libs.Print($"Failed to remove Service \"{conf.ServiceName}\"");
            return 1;
        }

        var ss = $"Removed Service \"{conf.ServiceName}\"";
        Log("");
        Log(ss);
        Libs.Print(ss);
        return 0;
    }

    private static void LoggingSvc()
    {
        long prev = 0;
        while (true)
        {
            Thread.Sleep(200);

            var lastWrite = Libs.LastWriteTime(conf.LastLineFile);

            if (lastWrite == 0 || lastWrite == prev)
            {
                continue;
            }

            try
            {
                using (var sr = new StreamReader(conf.LastLineFile))
                {
                    Console.Write(sr.ReadToEnd());
                }
            }
            catch (Exception)
            {
                continue;
            }

            Console.Out.Flush();
            prev = lastWrite;
        }
    }
}

public partial class Program
{
    public static void Log(string s, bool append = true)
    {
        if (testMode)
        {
            if (s.StartsWith("[ERROR]") || s.StartsWith("[WARN]"))
            {
                Libs.Print(s);
            }

            return;
        }

        s = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {s}";

        try
        {
            Libs.WriteLineToFile(Conf.LOG_FILE, s, append);
        }
        catch (Exception e)
        {
            Libs.Print(e.Message);
        }
    }
}

partial class Program
{
    private static ProcessStartInfo psi = null;

    private static object procLock = null;

    private static Process proc = null;

    public static void OnStart()
    {
        if (!testMode)
        {
            ReadSvcConfigInSvcBin();
        }

        psi = new ProcessStartInfo
        {
            FileName = conf.WorkerFileName,
            Arguments = conf.WorkerArguments,
            WorkingDirectory = conf.WorkingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = !testMode,
            StandardErrorEncoding = conf.WorkerEncodingObj,
            StandardOutputEncoding = conf.WorkerEncodingObj
        };

        procLock = new object();

        proc = StartWorker();

        if (testMode)
        {
            return;
        }

        if (proc == null)
        {
            Environment.Exit(1);
        }

        Log($"[INFO] Started Service \"{conf.ServiceName}\"");
    }

    private static void ReadSvcConfigInSvcBin()
    {
        var mngObj = SvcUtils.GetServiceManagementObjectInSvcBin();
        
        try
        {
            var svcDescription = mngObj["Description"].ToString();
            var i = svcDescription.LastIndexOf('<') + 1;
            var n = svcDescription.Length - 1 - i;
            var cwd = svcDescription.Substring(i, n);
            Libs.SetCwd(cwd);
        }
        catch (Exception ex)
        {
            Libs.Abort($"Can not set cwd from service's description, {ex.Message}");
        }

        var err = conf.Read();
        if (err != null)
        {
            Log($"Configuration error, {err}");
            Environment.Exit(1);
        }
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

            Log($"[INFO] Created Worker `{conf.Worker}` in `{conf.WorkingDir}`");
            return p;
        }
        catch (Exception e)
        {
            Log($"[ERROR] Failed to create Worker `{conf.Worker}` in `{conf.WorkingDir}`\n{e}");
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

            Log($"[INFO] Recreate Worker after {Conf.RESTART_WAIT_SECONDS} seconds");
        }

        Thread.Sleep(Conf.RESTART_WAIT_SECONDS * 1000);

        lock (procLock)
        {
            if (proc == null)
            {
                return;
            }

            proc = StartWorker();
        }
    }

    public static void OnStop()
    {
        lock (procLock)
        {
            var p = proc;
            proc = null;
            StopWorker(p);
            Log($"[INFO] Stopped Service \"{conf.ServiceName}\"");
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
                p.KillTree();
                Log("[WARN] Worker has been killed");
            }
            catch (Exception ee)
            {
                Log($"[ERROR] Failed to kill Worker, {ee.Message}");
            }
            return;
        }

        if (!p.WaitForExit(conf.WaitSecondsForWorkerToExit * 1000))
        {
            Log("[WARN] Worker refused to exit");
            try
            {
                p.KillTree();
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
        var outFile = Path.Combine(conf.OutFileDir, $"{DateTime.Now:yyyy-MM-dd}.log");

        lock (outLock)
        {
            if (testMode)
            {
                Libs.Print(s);
                return;
            }

            try
            {
                Libs.WriteLineToFile(outFile, s, true);
            }
            catch (Exception e)
            {
                Log($"[ERROR] Failed to write Worker's output to `{outFile}`, {e.Message}");
            }

            try
            {
                Libs.WriteLineToFile(conf.LastLineFile, s, false);
            }
            catch (Exception e)
            {
                Log($"[ERROR] Failed to write Worker's output to `{conf.LastLineFile}`, {e.Message}");
            }
        }
    }
}

class SimpleService : ServiceBase
{
    protected override void OnStart(string[] args)
    {
        try
        {
            Program.OnStart();
        }
        catch (Exception ex)
        {
            Libs.Abort($"Failed in Program.OnStart:\r\n{ex}");
        }
    }

    protected override void OnStop()
    {
        try
        {
            Program.OnStop();
        }
        catch (Exception ex)
        {
            Program.Log($"An error happened in Program.OnStop:\r\n{ex}");
            Environment.Exit(1);
        }
    }
}