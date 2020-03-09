using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

public partial class Program
{
    private const string VERSION = "Easy Service: v1.0.1";

    private const string COMMANDS = "create|check|status|test-worker|install|start|stop|restart|remove";

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

        sc.StartSvc();
        Libs.Print($"Started Service \"{sc.ServiceName}\"");
        return 0;
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
}

public partial class Program
{
    private static void Log(string s, bool append = true)
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
            var err = ReadSvcConfigInSvcBin();
            if (err != null)
            {
                err = $"Failed to read configuration for Service \"{conf.ServiceName}\": {err}";
                Log($"[ERROR] {err}");
                throw new Exception(err);
            }
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
            throw new Exception($"Failed to start worker for Service \"{conf.ServiceName}\"");
        }

        Log($"[INFO] Started Service \"{conf.ServiceName}\"");
    }

    private static string ReadSvcConfigInSvcBin()
    {
        Libs.SetCwd(Libs.BinDir);

        var mngObj = SvcUtils.GetServiceManagementObjectInSvcBin();
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
            Libs.SetCwd(cwd);
        }
        catch (Exception ex)
        {
            return $"can not set cwd from service's description, {ex.Message}";
        }

        var err = conf.Read();
        if (err != null)
        {
            return $"configuration error, {err}";
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
                p.Kill();
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
        Program.OnStart();
    }

    protected override void OnStop()
    {
        Program.OnStop();
    }
}