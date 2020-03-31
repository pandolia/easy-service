using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Management;

public delegate string FieldSetter(string value);

public static class Libs
{
    public static readonly string BinDir = AppDomain.CurrentDomain.BaseDirectory;

    public static readonly string BinPath = Assembly.GetExecutingAssembly().Location;

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

    public static int Exec(string file, string args = null, string dir = null)
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

    public static string ReadConfig(string file, Dictionary<string, FieldSetter> setterDict)
    {
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
            return $"Falied to read config: {e.Message}";
        }

        var errs = new List<string>();

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
                errs.Add($"Invalid configuration key `{key}`");
                continue;
            }

            var value = (i == -1) ? "" : s.Substring(i + 1).TrimStart();
            var valueErr = setterDict[sKey](value);
            if (valueErr != null)
            {
                errs.Add($"Bad {key} `{value}`, {valueErr}");
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

        return errs.Count == 0 ? null : string.Join("\n", errs);
    }

    public static string CopyDir(string src, string dest, string ignoreExt)
    {
        var srcDir = new DirectoryInfo(src);
        if (!srcDir.Exists)
        {
            return $"Source directory {src} does not exist";
        }

        var destDir = new DirectoryInfo(dest);
        if (destDir.Exists)
        {
            return $"Destination directory {dest} already exists";
        }

        try
        {
            destDir.Create();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        var srcPath = srcDir.FullName;
        var destPath = destDir.FullName;

        foreach (var dir in Directory.EnumerateDirectories(srcPath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(srcPath, destPath));
        }


        foreach (var file in Directory.EnumerateFiles(srcPath, "*", SearchOption.AllDirectories))
        {
            var tarFile = file.Replace(srcPath, destPath);
            if (tarFile.EndsWith(ignoreExt))
            {
                continue;
            }

            File.Copy(file, tarFile);
        }

        return null;
    }

    public static bool Contains<T>(this IEnumerable<T> list, T e)
    {
        foreach (T el in list)
        {
            if (el.Equals(e))
            {
                return true;
            }
        }

        return false;
    }

    public static Thread NewThread(Action action)
    {
        var th = new Thread(() => action());
        th.IsBackground = true;
        th.Start();
        return th;
    }

    public static long LastWriteTime(string filename)
    {
        var fileInfo = new FileInfo(filename);
        return fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0;
    }

    public static void KillTree(this Process root)
    {
        var query = $"Select * From Win32_Process Where ParentProcessID={root.Id}";
        var searcher = new ManagementObjectSearcher(query);
        var moc = searcher.Get();

        foreach (ManagementObject mo in moc)
        {
            var id = Convert.ToInt32(mo["ProcessID"]);
            var p = Process.GetProcessById(id);
            if (p == null)
            {
                continue;
            }

            p.KillTree();
        }

        if (root.HasExited)
        {
            return;
        }

        root.Kill();

        Thread.Sleep(500);
    }
}