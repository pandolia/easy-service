using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.ServiceProcess;

public static class SvcUtils
{
    public static void InstallService(Conf conf)
    {
        var depend = conf.Dependencies.AddUniq(Consts.NECESSARY_DEPENDENCY, '/');

        // add "[svc]" to DisplayName to avoid "ServiceName == DisplayName" (which cases error in win7)
        var scArgs = $"create \"{conf.ServiceName}\" binPath= \"{Libs.BinPath}\" start= auto "
            + $"DisplayName= \"[svc] {conf.DisplayName}\" depend= \"{depend}\"";

        if (conf.User.Length > 0)
        {
            var obj = (conf.Domain.Length > 0) ? $"{conf.Domain}\\{conf.User}" : conf.User;
            scArgs = $"{scArgs} obj= \"{obj}\" password= \"{conf.Password}\"";
        }

        Libs.Exec("sc", scArgs);

        var sc = GetServiceController(conf.ServiceName);
        if (sc == null)
        {
            Libs.Abort($"Failed to install Service \"{conf.ServiceName}\"");
        }

        var msg = $"Installed Service \"{conf.ServiceName}\"";
        Console.WriteLine(msg);
        AddLog("INFO", msg, false);
        AddLog("INFO", "");

        SetSvcDescription(conf);

        sc.StartService();
    }

    public static void Operate(this ServiceController sc, string op)
    {
        switch (op)
        {
            case "start":
                sc.StartService();
                break;

            case "stop":
                sc.StopService();
                break;

            case "restart":
                sc.RestartService();
                break;

            case "remove":
                sc.RemoveService();
                break;
        }
    }

    public static void RemoveService(this ServiceController sc)
    {
        var name = sc.ServiceName;

        sc.StopService();

        Libs.Exec("sc", $"delete \"{name}\"");

        if (GetServiceController(name) != null)
        {
            Libs.Abort($"Failed to remove Service \"{name}\"");
        }

        var msg = $"Removed Service \"{name}\"";
        AddLog("INFO", "");
        AddLog("INFO", msg);
        Console.WriteLine(msg);
    }

    public static void StartService(this ServiceController sc)
    {
        if (sc.Status == ServiceControllerStatus.Running)
        {
            Console.WriteLine($"Service \"{sc.ServiceName}\" is already started");
            return;
        }

        var startMsg = $"Starting service \"{sc.ServiceName}\"";

        var timeoutMsg = $"Failed to start the service, "
            + $"please refer to {Libs.GetCwd()}svc.log or "
            + $"{Libs.BinDir}*.error.txt to see what happened";

        var okMsg = $"Started Service \"{sc.ServiceName}\"";

        Libs.Timeout(sc.StartSvc, Consts.MAX_SERVICE_START_STOP_SECONDS, startMsg, timeoutMsg, okMsg);
    }

    public static void StopService(this ServiceController sc)
    {
        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            Console.WriteLine($"Service \"{sc.ServiceName}\" is already stopped");
            return;
        }

        var startMsg = $"Stopping service \"{sc.ServiceName}\"";

        var timeoutMsg = $"Failed to stop the service, "
            + $"please refer to {Libs.GetCwd()}svc.log to see what happened";

        var okMsg = $"Stopped Service \"{sc.ServiceName}\"";

        Libs.Timeout(sc.StopSvc, Consts.MAX_SERVICE_START_STOP_SECONDS, startMsg, timeoutMsg, okMsg);
    }

    public static void RestartService(this ServiceController sc)
    {
        sc.StopService();
        sc.StartService();
    }

    private static void StartSvc(this ServiceController sc)
    {
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running);
        sc.Refresh();
    }

    private static void StopSvc(this ServiceController sc)
    {
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped);
        sc.Refresh();
    }

    private static void SetSvcDescription(Conf conf)
    {
        var description = $"{conf.Description} @<{Libs.GetCwd()}>";
        var scArgs = $"description \"{conf.ServiceName}\" \"{description}\"";
        string err;

        Libs.Exec("sc", scArgs);
        try
        {
            var mngObj = GetServiceManagementObjectByName(conf.ServiceName);
            if (mngObj != null && mngObj["Description"].ToString() == description)
            {
                return;
            }

            err = "unknown reason";
        }
        catch (Exception ex)
        {
            err = ex.ToString();
        }

        err = $"Failed to set description for Service \"{conf.ServiceName}\": {err}";
        AddLog("ERROR", err);
        Libs.Abort($"{err}\r\nPlease run `svc remove` to remove the Service");
    }

    public static void SetCwdInSvcBin()
    {
        try
        {
            var description = GetServiceDescriptionInSvcBin();
            var cwd = GetCwdInDescription(description);
            Libs.SetCwd(cwd);
        }
        catch (Exception ex)
        {
            Libs.Dump($"Failed to set cwd in service's bin, {ex}");
            Environment.Exit(1);
        }
    }

    private static string GetCwdInDescription(string description)
    {
        var i = description.LastIndexOf('<') + 1;
        var n = description.Length - 1 - i;
        var cwd = description.Substring(i, n);
        return cwd;
    }

    public static ServiceController GetServiceController(string name)
    {
        var sc = new ServiceController(name);
        try
        {
            _ = sc.ServiceName;
        }
        catch (Exception)
        {
            sc = null;
        }

        return sc;
    }

    private static ManagementObject GetServiceManagementObjectByName(string name)
    {
        using (var mngObj = new ManagementObject($"Win32_Service.Name='{name}'"))
        {
            mngObj.Get();
            return mngObj;
        }
    }

    private static string GetServiceDescriptionInSvcBin()
    {
        int processId = Process.GetCurrentProcess().Id;
        string query = $"SELECT * FROM Win32_Service where ProcessId = {processId}";
        using (var searcher = new ManagementObjectSearcher(query))
        {
            foreach (ManagementObject queryObj in searcher.Get())
            {
                return queryObj["Description"].ToString();
            }
        }

        throw new Exception("Can't get the service management object");
    }

    public static void ListAllEasyServices()
    {
        ServiceInfo.PrintInfoList(GetEasyServices());
    }

    private static List<ServiceInfo> GetEasyServices()
    {
        Console.Write("Getting all EasyServices, please wait...");
        Console.Out.Flush();

        var hasCircle = false;
        var infoList = new List<ServiceInfo>();
        foreach (var sc in ServiceController.GetServices())
        {
            var name = sc.ServiceName;
            var mngObj = GetServiceManagementObjectByName(name);
            var path = mngObj["PathName"].ToString();
            if (path != Libs.BinPath)
            {
                continue;
            }

            var description = mngObj["Description"].ToString();
            var confDir = GetCwdInDescription(description);
            var info = new ServiceInfo(sc, confDir);

            if (!Libs.InsertInto(infoList, info, ServiceInfo.IsDepend))
            {
                Console.WriteLine("\r\n[ERROR] Circle dependencies detected");
                infoList.Add(info);
                hasCircle = true;
            }
        }

        if (!hasCircle)
        {
            Console.Write($"\r{"".PadRight(60)}\r");
            Console.Out.Flush();
        }

        if (infoList.Count == 0)
        {
            Libs.Abort($"No EasyService found");
        }

        return infoList;
    }

    public static ServiceInfo GetEasyService(string nameOrIndex)
    {
        var infoList = GetEasyServices();

        if (int.TryParse(nameOrIndex, out int i))
        {
            if (i < 1 || i > infoList.Count)
            {
                Libs.Abort($"Index \"{i}\" out of boundary 1~\"{infoList.Count}\"");
            }

            return infoList[i - 1];
        }

        foreach (var info in infoList)
        {
            if (info.Sc.ServiceName == nameOrIndex)
            {
                return info;
            }
        }

        Libs.Abort($"EasyService \"{nameOrIndex}\" not exists");
        return null;
    }

    // op: start|stop|remove
    public static void ManageAll(string op)
    {
        var infoList = GetEasyServices();

        if (op != "start")
        {
            infoList.Reverse();
        }

        ServiceInfo.PrintInfoList(infoList);

        foreach (var info in infoList)
        {
            info.Cd();
            info.Sc.Refresh();
            info.Sc.Operate(op);
        }

        if (op != "remove")
        {
            ServiceInfo.PrintInfoList(infoList);
        }
        else
        {
            Console.WriteLine("\r\nAll EasyServices are removed\r\n");
        }
    }

    public static string GetName(this ServiceController sc)
    {
        try
        {
            return sc.ServiceName;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static string GetDependenciesString(this ServiceController sc)
    {
        var dependencies = sc.ServicesDependedOn;
        var n = dependencies.Length;
        var buf = new string[n];
        var j = 0;

        for (var i = 0; i < n; i++)
        {
            var name = dependencies[i].GetName();
            if (name == Consts.NECESSARY_DEPENDENCY || name == null)
            {
                continue;
            }

            buf[j++] = name;
        }

        return string.Join(",", buf, 0, j);
    }

    private static readonly object LogLock = new object();

    private static void AddLog(string level, string s, bool append)
    {
        s = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {s}";

        lock (LogLock)
        {
            try
            {
                Libs.WriteLineToFile(Consts.LOG_FILE, s, append);
            }
            catch (Exception e)
            {
                Libs.Dump(e.Message);
            }
        }
    }

    public static void AddLog(string level, string s)
    {
        AddLog(level, s, true);
    }
}

public class ServiceInfo
{
    public readonly ServiceController Sc;

    public readonly string ConfDir;

    public ServiceInfo(ServiceController sc, string confDir)
    {
        Sc = sc;
        ConfDir = confDir;
    }

    public static bool IsDepend(ServiceInfo si1, ServiceInfo si2)
    {
        var name2 = si2.Sc.GetName();

        if (name2 == null)
        {
            return false;
        }

        foreach (var sc in si1.Sc.ServicesDependedOn)
        {
            var name = sc.GetName();

            if (name == null)
            {
                continue;
            }

            if (name2 == name)
            {
                return true;
            }
        }

        return false;
    }

    public static void PrintInfoList(List<ServiceInfo> infoList)
    {
        var m = infoList.Count;

        var mat = new string[m + 1, 4];

        mat[0, 0] = "Service";
        mat[0, 1] = "Status";
        mat[0, 2] = "Dependencies";
        mat[0, 3] = "Config Directory";

        for (var i = 0; i < m; i++)
        {
            var info = infoList[i];
            info.Sc.Refresh();
            mat[i + 1, 0] = info.Sc.ServiceName;
            mat[i + 1, 1] = info.Sc.Status.ToString().ToLower();
            mat[i + 1, 2] = info.Sc.GetDependenciesString();
            mat[i + 1, 3] = info.ConfDir;
        }

        Console.WriteLine($"\r\n{Libs.ToPrettyTable(mat)}\r\n");
    }

    public void Cd()
    {
        if (!Directory.Exists(ConfDir))
        {
            Libs.Abort($"Service directory \"{ConfDir}\" not exists");
        }

        Libs.SetCwd(ConfDir);
    }
}