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

    public static string AddUniq(this string s, string p, char c)
    {
        if (s == p || s == "")
        {
            return p;
        }

        if (p == "")
        {
            return s;
        }

        if (s.StartsWith(p + c) || s.EndsWith(c + p) || s.Contains(c + p + c))
        {
            return s;
        }

        return s + c + p;

    }

    public static void Dump(string s)
    {
        var pid = Process.GetCurrentProcess().Id;
        var filename = $"{BinDir}\\{DateTime.Now:yyyy-MM-dd-HH-mm-ss-ffff}-{pid}.error.txt";
        try
        {
            WriteLineToFile(filename, s);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
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
                    Console.WriteLine(p.StandardOutput.ReadToEnd());
                }
                return p.ExitCode;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
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
                errs.Add($"Bad {key} `{value}`: {valueErr}");
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

        return errs.Count == 0 ? null : "    " + string.Join("\r\n    ", errs);
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
        var th = new Thread(() => action())
        {
            IsBackground = true
        };
        th.Start();
        return th;
    }

    public static long LastWriteTime(string filename)
    {
        var fileInfo = new FileInfo(filename);
        return fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0;
    }

    public static string GetName(this Process proc)
    {
        if (proc.HasExited)
        {
            return $"Process-{proc.Id}";
        }

        return $"Process-{proc.ProcessName}-{proc.Id}";
    }

    private static void GetTree(this Process proc, List<Process> result)
    {
        result.Add(proc);

        var query = $"Select * From Win32_Process Where ParentProcessID={proc.Id}";
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

            p.GetTree(result);
        }
    }

    public static List<Process> GetTree(this Process proc)
    {
        var tree = new List<Process>();
        proc.GetTree(tree);
        return tree;
    }

    public static void KillTree(this Process proc, Action<string> print)
    {
        var tree = proc.GetTree();
        var n = tree.Count;
        var names = new string[n];

        for (var i = 0; i < n; i++)
        {
            names[i] = tree[i].GetName();
        }

        for (var i = 0; i < n; i++)
        {
            var p = tree[i];
            var name = names[i];

            if (p.HasExited)
            {
                print($"{name} has exited already");
                continue;
            }

            try
            {
                p.Kill();
                print($"Killed {name}");
            }
            catch (Exception ex)
            {
                print($"Failed to kill {name}: {ex.Message}");
            }

            if (i != n - 1)
            {
                Thread.Sleep(1000);
            }
        }
    }

    public static string GetData(this DataReceivedEventArgs ev)
    {
        string s = ev.Data;

        if (s == null)
        {
            return null;
        }

        int n = s.Length;
        if (n > 0 && s[n - 1] == '\0')
        {
            if (n == 1)
            {
                return null;
            }

            return s.Substring(0, n - 1);
        }

        return s;
    }

    public static string ToPrettyString<TVal>(this Dictionary<string, TVal> dict)
    {
        var n = dict.Count;
        var segs = new string[n];
        var i = 0;

        foreach (var item in dict)
        {
            segs[i++] = $"[{item.Key}]=[{item.Value}]";
        }

        return string.Join(", ", segs);
    }
}