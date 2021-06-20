using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

public class Worker
{
    private readonly Conf Conf;

    private readonly Action<string, string> AddLog;

    private readonly bool RedirectMode;

    private readonly ProcessStartInfo Psi;

    private readonly object ProcLock = new object();

    private readonly object OutLock = new object();

    private Process Proc = null;

    public Worker(Action<string, string> logAdder = null, bool popup = false)
    {
        AddLog = logAdder ?? Print;

        Conf = new Conf(Abort);

        Psi = new ProcessStartInfo
        {
            FileName = Conf.WorkerFileName,
            Arguments = Conf.WorkerArguments,
            WorkingDirectory = Conf.WorkingDir
        };

        if (popup)
        {
            RedirectMode = false;
            Psi.UseShellExecute = true;
            return;
        }

        RedirectMode = true;
        Psi.UseShellExecute = false;
        Psi.RedirectStandardOutput = true;
        Psi.RedirectStandardError = true;
        Psi.RedirectStandardInput = true;
        Psi.StandardErrorEncoding = Conf.WorkerEncodingObj;
        Psi.StandardOutputEncoding = Conf.WorkerEncodingObj;

        foreach (var item in Conf.Environments)
        {
            Psi.EnvironmentVariables[item.Key] = item.Value;
        }
    }

    private Thread thDelete = null;

    public void Start()
    {
        if (Conf.MaxLogFilesNum != 0 && thDelete == null)
        {
            thDelete = new Thread(DeleteLoop)
            {
                IsBackground = true
            };
            thDelete.Start();
        }

        if (Proc == null && Conf.LastLineFile != null)
        {
            Libs.WriteLineToFile(Conf.LastLineFile, "", false);
        }

        Proc = new Process
        {
            StartInfo = Psi
        };

        if (RedirectMode)
        {
            Proc.OutputDataReceived += OutPut;
            Proc.ErrorDataReceived += OutPut;
        }

        Proc.Exited += OnExit;
        Proc.EnableRaisingEvents = true;

        try
        {
            Proc.Start();
        }
        catch (Exception ex)
        {
            Abort($"Failed to create Worker `{Conf.Worker}` in `{Conf.WorkingDir}`:\r\n{ex}");
        }

        Info($"Created Worker `{Conf.Worker}` in `{Conf.WorkingDir}`");

        if (RedirectMode)
        {
            Proc.BeginErrorReadLine();
            Proc.BeginOutputReadLine();
        }

        if (Conf.WorkerMemoryLimit != -1)
        {
            Libs.NewThread(MonitorMemory);
        }
    }

    private void MonitorMemory()
    {
        Info("Start Memory Monitor Loop");

        while (true)
        {
            Thread.Sleep(Consts.MONITOR_INTERVAL_MINUTES * 60 * 1000);

            lock (ProcLock)
            {
                if (Proc == null)
                {
                    break;
                }

                var mem = Proc.GetTreeMemory() / 1024;
                if (mem < Conf.WorkerMemoryLimit)
                {
                    continue;
                }

                Warn($"Worker's memory({mem:N3}MB) exceeds {Conf.WorkerMemoryLimit}MB");
                Proc.KillTree(Info);
                break;
            }
        }
    }

    public void Stop()
    {
        lock (ProcLock)
        {
            var proc = Proc;
            Proc = null;

            if (RedirectMode && Conf.WaitSecondsForWorkerToExit > 0 && NotifyToExit(proc))
            {
                return;
            }

            proc.KillTree(Info);
        }
    }

    private bool NotifyToExit(Process proc)
    {
        try
        {
            proc.StandardInput.Write("exit\r\n");
            proc.StandardInput.Flush();
            Info("Notified Worker to exit");
        }
        catch (Exception ex)
        {
            Error($"Failed to notify Worker to exit:\r\n{ex}");
            return false;
        }

        if (!proc.WaitForExit(Conf.WaitSecondsForWorkerToExit * 1000))
        {
            Info("Worker refused to exit");
            return false;
        }

        Info($"Worker exited with code {proc.ExitCode}");
        return true;
    }

    private void OnExit(object sender, EventArgs e)
    {
        lock (ProcLock)
        {
            if (Proc == null)
            {
                return;
            }

            Warn($"The worker exited without be notified, "
                + $"it will be re-created after {Consts.RESTART_WAIT_SECONDS} seconds");

            Thread.Sleep(Consts.RESTART_WAIT_SECONDS * 1000);
            Start();
            Thread.Sleep(1000);
        }
    }

    private void OutPut(object sender, DataReceivedEventArgs ev)
    {
        var data = ev.GetData();
        if (data == null)
        {
            return;
        }

        lock (OutLock)
        {
            WriteOutput(data);
        }
    }

    private static void Print(string level, string s)
    {
        Console.WriteLine($"[svc.{level.ToLower()}] {s}");
    }

    private void Info(string msg)
    {
        AddLog("INFO", msg);
    }

    private void Warn(string msg)
    {
        AddLog("WARN", msg);
    }

    private void Error(string msg)
    {
        AddLog("ERROR", msg);
    }

    private void Abort(string msg)
    {
        AddLog("CRITICAL", msg);
        Environment.Exit(1);
    }

    private void WriteOutput(string data)
    {
        if (AddLog == Print)
        {
            Console.WriteLine(data);
            return;
        }

        if (Conf.OutFileDir == null)
        {
            return;
        }

        var outFile = Path.Combine(Conf.OutFileDir, $"{DateTime.Now:yyyy-MM-dd}.log");
        try
        {
            Libs.WriteLineToFile(outFile, data, true);
        }
        catch (Exception ex)
        {
            Error($"Failed to write Worker's output to `{outFile}`: {ex.Message}");
        }

        try
        {
            Libs.WriteLineToFile(Conf.LastLineFile, data, false);
        }
        catch (Exception e)
        {
            Error($"Failed to write Worker's output to `{Conf.LastLineFile}`: {e.Message}");
        }
    }

    private void DeleteLoop()
    {
        // 2 hours
        var m = 2 * 60 * 60 * 1000;
        while (true)
        {
            DeleteOldLogFiles();
            Thread.Sleep(m);
        }
    }

    private void DeleteOldLogFiles()
    {
        var files = Libs.GetFiles(Conf.OutFileDir, @"^\d{4}-\d{2}-\d{2}\.log$");

        for (int i = 0, n = files.Count - Conf.MaxLogFilesNum; i < n; i++)
        {
            var file = files[i];
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                Error($"Failed to delete file `{file.FullName}`: {ex.Message}");
                continue;
            }

            Info($"Delete file `{file.FullName}`");
        }
    }
}