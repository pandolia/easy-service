using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;

public partial class Program
{
    private const string VERSION = "Easy Service: v1.0.3";

    private const string COMMANDS = "create|check|status|test-worker|install|stop|start|restart|remove|log";

    private const string USAGE = 
        "Usage: svc version|-v|--version\r\n" +
        "       svc create $project_name\r\n" +
        "       svc check|status\r\n" +
        "       svc test-worker\r\n" +
        "       svc install|stop|start|restart|remove|log";

    private static Conf Conf = null;

    private static string Op = null;

    private static string Arg1 = null;

    private static ServiceController Sc = null;

    private static string Status = null;

    private static void Abort(string msg)
    {
        Console.WriteLine(msg);
        Environment.Exit(1);
    }

    private static void Exit(string msg)
    {
        Console.WriteLine(msg);
        Environment.Exit(0);
    }

    private static void CheckStage0(string[] args)
    {
        Op = args[0];

        if (args.Length == 2)
        {
            Arg1 = args[1];
        }

        if (Op == "version" || Op == "--version" || Op == "-v")
        {
            Exit(VERSION);
        }

        if (!COMMANDS.Split('|').Contains(Op)
            || (Op == "create" && args.Length != 2)
            || (Op == "test-worker" && args.Length > 2)
            || (Op != "create" && Op != "test-worker" && args.Length != 1))
        {
            Abort(USAGE);
        }
    }

    private static void CheckStage1()
    {
        if (Op == "create")
        {
            CreateProject();
        }

        Conf = new Conf(true);

        if (Op == "test-worker")
        {
            TestWorker();
        }

        Sc = SvcUtils.GetServiceController(Conf.ServiceName);
        Status = (Sc == null) ? "not installed" : Sc.Status.ToString().ToLower();

        if (Op == "check" || Op == "status")
        {
            Conf.ShowConfig();
            Exit($"\r\nService status: {Status}");
        }

        if (Op == "install" && Sc != null)
        {
            Abort($"Service \"{Conf.ServiceName}\" is already installed!");
        }

        if (Op != "install" && Sc == null)
        {
            Abort($"Service \"{Conf.ServiceName}\" is not installed!");
        }

        if (Op == "log" && Status != "running")
        {
            Abort($"Service \"{Conf.ServiceName}\" is not running");
        }
    }

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ServiceBase.Run(new SimpleService());
            return 0;
        }

        CheckStage0(args);

        CheckStage1();

        if (Op == "install")
        {
            InstallService();
        }

        if (Op == "start")
        {
            StartService();
        }

        if (Op == "stop")
        {
            StopService();
        }

        if (Op == "restart")
        {
            RestartService();
        }

        if (Op == "remove")
        {
            RemoveService();
        }

        // Op == "log"
        LoggingSvc();

        return 0;
    }

    private static void CreateProject()
    {
        var err = Libs.CopyDir($"{Libs.BinDir}..\\samples\\csharp-version", Arg1, ".log");
        if (err != null)
        {
            Abort(err);
        }

        Exit($"Create an Easy-Service project in {Arg1}");
    }

    private static void TestWorker()
    {
        var worker = new Worker(Conf, Arg1 != "--popup");
        worker.Start();
        Console.ReadLine();
        worker.Stop();
        Environment.Exit(0);
    }

    private static void InstallService()
    {
        if ((Sc = SvcUtils.CreateSvc(Conf)) == null)
        {
            Abort($"Failed to install Service \"{Conf.ServiceName}\"");
        }

        var ss = $"Installed Service \"{Conf.ServiceName}\"";
        Console.WriteLine(ss);
        Conf.LogFile("INFO", ss, false);
        Conf.LogFile("INFO", "");

        var err = SvcUtils.SetSvcDescription(Conf);
        if (err != null)
        {
            err = $"Failed to set description for Service \"{Conf.ServiceName}\": {err}";
            Conf.LogFile("ERROR", err);
            Abort($"{err}\r\nPlease run `svc remove` to remove the Service");
        }

        StartService();
    }

    private static void StartService()
    {
        if (Sc.Status == ServiceControllerStatus.Running)
        {
            Abort($"Service \"{Sc.ServiceName}\" is already started");
        }

        Console.Write($"Starting...");
        Libs.NewThread(() => Waiting("start"));
        Sc.StartSvc();
        Exit($"\r\nStarted Service \"{Sc.ServiceName}\"");
    }

    private static void Waiting(string action)
    {
        Thread.Sleep(2000);
        for (int i = 0; i < 6; i++)
        {
            Thread.Sleep(1500);
            Console.Write(".");
            Console.Out.Flush();
        }

        Abort($"\r\nFailed to {action} the service, "
            + $"please refer to svc.log or {Libs.BinDir}*.error.txt to see what happened");
    }

    private static int stopService()
    {
        if (Sc.Status == ServiceControllerStatus.Stopped)
        {
            Console.WriteLine($"Service \"{Sc.ServiceName}\" is already stopped");
            return 1;
        }

        Console.Write($"Stopping...");
        Libs.NewThread(() => Waiting("stop"));
        Sc.StopSvc();
        Console.WriteLine($"\r\nStopped Service \"{Sc.ServiceName}\"");
        return 0;
    }

    private static void StopService()
    {
        Environment.Exit(stopService());
    }

    private static void RestartService()
    {
        stopService();
        StartService();
    }

    private static void RemoveService()
    {
        stopService();

        if (!SvcUtils.DeleteSvc(Conf.ServiceName))
        {
            Abort($"Failed to remove Service \"{Conf.ServiceName}\"");
        }

        var ss = $"Removed Service \"{Conf.ServiceName}\"";
        Conf.LogFile("INFO", "");
        Conf.LogFile("INFO", ss);
        Exit(ss);
    }

    private static void loggingSvc()
    {
        long prev = 0;
        while (true)
        {
            Thread.Sleep(200);

            var lastWrite = Libs.LastWriteTime(Conf.LastLineFile);

            if (lastWrite == 0 || lastWrite == prev)
            {
                continue;
            }

            try
            {
                using (var sr = new StreamReader(Conf.LastLineFile))
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

    private static void LoggingSvc()
    {
        if (Conf.OutFileDir == null)
        {
            Abort("Error: OutFileDir must not be $NULL");
        }

        Libs.NewThread(loggingSvc);
        Console.ReadLine();
    }
}

class SimpleService : ServiceBase
{
    private Conf Conf = null;

    private Worker Worker = null;

    protected override void OnStart(string[] args)
    {
        try
        {
            var svcDescription = SvcUtils.GetServiceDescriptionInSvcBin();
            var i = svcDescription.LastIndexOf('<') + 1;
            var n = svcDescription.Length - 1 - i;
            var cwd = svcDescription.Substring(i, n);
            Libs.SetCwd(cwd);
        }
        catch (Exception ex)
        {
            Libs.Dump($"Failed to set cwd from service's description:\r\n{ex}");
            Environment.Exit(1);
        }

        Conf = new Conf(false);

        try
        {
            Worker = new Worker(Conf, true);
            Worker.Start();
        }
        catch (Exception ex)
        {
            Conf.Abort($"Failed to start the worker:\r\n{ex}");
        }
    }

    protected override void OnStop()
    {
        try
        {
            Worker.Stop();
        }
        catch (Exception ex)
        {
            Conf.Abort($"Failed to stop the worker:\r\n{ex}");
        }
    }
}