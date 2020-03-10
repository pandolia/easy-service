using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Management;
using System;
using System.Threading;

public static class SvcUtils
{
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

    public static ManagementObject GetServiceManagementObjectInSvcBin()
    {
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

    public static ManagementObject GetServiceManagementObjectByName(string name)
    {
        using (var mngObj = new ManagementObject($"Win32_Service.Name='{name}'"))
        {
            mngObj.Get();
            return mngObj;
        }
    }

    public static ServiceController CreateSvc(Conf conf)
    {
        var scArgs = $"create \"{conf.ServiceName}\" binPath= \"{Libs.BinPath}\" start= auto "
            + $"DisplayName= \"{conf.DisplayName}\" depend= \"{conf.Dependencies}\"";

        if (conf.User.Length > 0)
        {
            var obj = (conf.Domain.Length > 0) ? $"{conf.Domain}\\{conf.User}" : conf.User;
            scArgs = $"{scArgs} obj= \"{obj}\" password= \"{conf.Password}\"";
        }

        Libs.Exec("sc", scArgs);

        return GetServiceController(conf.ServiceName);
    }

    public static string SetSvcDescription(Conf conf)
    {
        var cwd = Path.GetFullPath(".");
        var description = $"{conf.Description} @<{cwd}>";
        var scArgs = $"description \"{conf.ServiceName}\" \"{description}\"";

        Libs.Exec("sc", scArgs);
        try
        {
            var mngObj = GetServiceManagementObjectByName(conf.ServiceName);
            if (mngObj == null || mngObj["Description"].ToString() != description)
            {
                return "unknown";
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }

    public static bool DeleteSvc(string serviceName)
    {
        Libs.Exec("sc", $"delete \"{serviceName}\"");
        return GetServiceController(serviceName) == null;
    }

    public static void StartSvc(this ServiceController sc)
    {
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running);
        sc.Refresh();
    }

    public static void StopSvc(this ServiceController sc)
    {
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped);
        sc.Refresh();
    }
}
