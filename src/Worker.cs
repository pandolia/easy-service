using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

public class Worker
{
    private readonly Conf Conf;

    private readonly bool RedirectMode;

    private readonly ProcessStartInfo Psi;

    private Process Proc;

    private readonly object ProcLock = new object();

    private readonly object OutLock = new object();

    public Worker(Conf conf, bool redirectMode)
    {
        Conf = conf;
        RedirectMode = redirectMode;
        Psi = new ProcessStartInfo
        {
            FileName = Conf.WorkerFileName,
            Arguments = Conf.WorkerArguments,
            WorkingDirectory = Conf.WorkingDir,
            UseShellExecute = Conf.ManageMode
        };

        if (RedirectMode)
        {
            Psi.UseShellExecute = false;
            Psi.RedirectStandardOutput = true;
            Psi.RedirectStandardError = true;
            Psi.RedirectStandardInput = true;
            Psi.StandardErrorEncoding = Conf.WorkerEncodingObj;
            Psi.StandardOutputEncoding = Conf.WorkerEncodingObj;
        }

        foreach (var item in conf.Environments)
        {
            Psi.EnvironmentVariables[item.Key] = item.Value;
        }
    }

    public void Start()
    {
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
            Conf.Abort($"Failed to create Worker `{Conf.Worker}` in `{Conf.WorkingDir}`:\r\n{ex}");
        }

        Conf.Info($"Created Worker `{Conf.Worker}` in `{Conf.WorkingDir}`");

        if (RedirectMode)
        {
            Proc.BeginErrorReadLine();
            Proc.BeginOutputReadLine();
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

            proc.KillTree(Conf.Info);
        }
    }

    private bool NotifyToExit(Process proc)
    {
        try
        {
            proc.StandardInput.Write("exit\r\n");
            proc.StandardInput.Flush();
            Conf.Info("Notified Worker to exit");
        }
        catch (Exception ex)
        {
            Conf.Error($"Failed to notify Worker to exit:\r\n{ex}");
            return false;
        }

        if (!proc.WaitForExit(Conf.WaitSecondsForWorkerToExit * 1000))
        {
            Conf.Info("Worker refused to exit");
            return false;
        }

        Conf.Info($"Worker exited with code {proc.ExitCode}");
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

            Conf.Warn($"The worker exited without be notified, "
                + $"it will be re-created after {Conf.RESTART_WAIT_SECONDS} seconds");

            Thread.Sleep(Conf.RESTART_WAIT_SECONDS * 1000);

            Start();
        }
    }

    private void OutPut(object sender, DataReceivedEventArgs ev)
    {
        var s = ev.GetData();
        if (s == null)
        {
            return;
        }

        lock (OutLock)
        {
            Conf.WriteOutput(s);
        }
    }
}