using System;
using System.IO;
using System.ServiceProcess;

public class Program
{
    private const string VERSION = "Easy Service: v1.0.7";

    private const string USAGE =
        "Usage:\r\n" +
        "    svc version|-v|--version\r\n" +
        "    svc create $project-name\r\n" +
        "    svc check|status [$project-directory]\r\n" +
        "    svc test-worker [$project-directory]\r\n" +
        "    svc install [$project-directory]\r\n" +
        "    svc stop|start|remove [$project-directory|$service-name|$service-index|all]\r\n" +
        "    svc restart|log [$project-directory|$service-name|$service-index]\r\n" +
        "    svc list|ls\r\n" +
        "\r\n" +
        "Note: $project-directory must contain '\\' or '/'\r\n" +
        "\r\n" +
        "Documentation:\r\n" +
        "    https://github.com/pandolia/easy-service";

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            SimpleService.RunService();
            return 0;
        }

        var op = args[0];
        var opr = $"|{op}|";
        var argc = args.Length - 1;
        var arg1 = argc == 1 ? args[1] : null;

        if (op == "version" || op == "--version" || op == "-v")
        {
            if (argc != 0)
            {
                Libs.Abort(USAGE);
            }

            Console.WriteLine(VERSION);
            return 0;
        }

        if (op == "create")
        {
            if (argc != 1)
            {
                Libs.Abort(USAGE);
            }

            CreateProject(arg1);
            return 0;
        }

        if (op == "list" || op == "ls")
        {
            if (argc != 0)
            {
                Libs.Abort(USAGE);
            }

            SvcUtils.ListAllEasyServices();
            return 0;
        }

        var commands = "|check|status|test-worker|install|stop|start|remove|restart|log|";

        if (!commands.Contains(opr) || argc > 1)
        {
            Libs.Abort(USAGE);
        }

        if (arg1 != null && (arg1.Contains('/') || arg1.Contains('\\')))
        {
            if (!Directory.Exists(arg1))
            {
                Libs.Abort($"Directory \"{arg1}\" not exists");
            }

            Libs.SetCwd(arg1);
            arg1 = null;
        }

        if (op == "test-worker")
        {
            if (arg1 != null && arg1 != "--popup")
            {
                Libs.Abort($"Directory argument \"{arg1}\" should contain '/' or '\\'");
            }

            TestWorker(arg1 == "--popup");
            return 0;
        }

        if ("|check|status|install|".Contains(opr) && arg1 != null)
        {
            Libs.Abort($"Directory argument \"{arg1}\" should contain '/' or '\\'");
        }

        if ("|restart|log|".Contains(opr) && arg1 == "all")
        {
            Libs.Abort(USAGE);
        }

        if (arg1 == null)
        {
            ManageOneByConfFile(op);
        }
        else if (arg1 != "all")
        {
            ManageOneBySvrIdentity(op, arg1);
        }
        else
        {
            SvcUtils.ManageAll(op);
        }

        return 0;
    }

    private static void ManageOneByConfFile(string op)
    {
        var conf = new Conf();
        var serviceName = conf.ServiceName;
        var sc = SvcUtils.GetServiceController(serviceName);
        var status = (sc == null) ? "not installed" : sc.Status.ToString().ToLower();

        if (op == "check" || op == "status")
        {
            conf.ShowConfig();
            Console.WriteLine($"\r\nService status: {status}");
            return;
        }

        if (op == "install")
        {
            if (sc != null)
            {
                Libs.Abort($"Service \"{serviceName}\" is already installed!");
            }

            SvcUtils.InstallService(conf);
            return;
        }

        if (op == "log")
        {
            LoggingSvc(conf, status);
            return;
        }

        // op: start|stop|restart|remove

        if (sc == null)
        {
            Libs.Abort($"Service \"{serviceName}\" is not installed!");
        }

        sc.Operate(op);
    }

    private static void ManageOneBySvrIdentity(string op, string arg1)
    {
        var info = SvcUtils.GetEasyService(arg1);
        var sc = info.Sc;

        info.Cd();

        if (op == "log")
        {
            var conf = new Conf();
            var status = sc.Status.ToString().ToLower();
            LoggingSvc(conf, status);
            return;
        }

        // op = start|stop|restart|remove
        sc.Operate(op);
    }

    private static void CreateProject(string arg1)
    {
        var err = Conf.CheckServiceName(arg1);
        if (err != null)
        {
            Libs.Abort($"[svc.critical] Bad project name `{arg1}`: {err}");
        }

        Libs.CopyDir($"{Libs.BinDir}..\\samples\\csharp-version", arg1, ".log");
        Libs.ReplaceStringInFile($"{arg1}\\svc.conf", "easy-service", arg1);
        Console.WriteLine($"Create an Easy-Service project in {arg1}");
    }

    private static void TestWorker(bool popup)
    {
        var worker = new Worker(null, popup);
        worker.Start();
        Console.ReadLine();
        worker.Stop();
    }

    private static void LoggingSvc(Conf conf, string status)
    {
        if (status != "running")
        {
            Libs.Abort($"Service \"{conf.ServiceName}\" is not running");
        }

        if (conf.OutFileDir == null)
        {
            Libs.Abort("Error: OutFileDir must not be $NULL");
        }

        Libs.NewThread(() => Libs.MonitorFile(conf.LastLineFile));
        Console.ReadLine();
    }
}

class SimpleService : ServiceBase
{
    public static void RunService()
    {
        Run(new SimpleService());
    }

    private Worker Worker = null;

    protected override void OnStart(string[] args)
    {
        SvcUtils.SetCwdInSvcBin();
        Worker = new Worker(SvcUtils.AddLog);
        Worker.Start();
    }

    protected override void OnStop()
    {
        Worker.Stop();
    }
}