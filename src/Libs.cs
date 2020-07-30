using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Management;
using System.Text.RegularExpressions;

public delegate string FieldSetter(string value);

public static class Libs
{
    public static readonly string BinDir = AppDomain.CurrentDomain.BaseDirectory;

    public static readonly string BinPath = Assembly.GetExecutingAssembly().Location;

    public static string GetCwd() => Path.GetFullPath(".") + "\\";

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

    public static void Abort(string msg)
    {
        Console.WriteLine(msg);
        Environment.Exit(1);
    }

    public static void Exit(string msg)
    {
        Console.WriteLine(msg);
        Environment.Exit(0);
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

    public static void Timeout(Action f, int seconds, string startMsg, string timeoutMsg, string okMsg)
    {
        Console.Write($"{startMsg}...");
        Console.Out.Flush();

        var flag = new bool[1] { false };
        NewThread(() => Waiting(flag, (seconds + 1) * 1000, timeoutMsg));
        f();
        flag[0] = true;

        Console.Write($" [√]");
        Console.Out.Flush();
        Thread.Sleep(500);
        Console.WriteLine($"\r{okMsg}{"".PadRight(startMsg.Length + 10 + seconds)}");
    }

    private static void Waiting(bool[] flag, int milliseconds, string timeoutMsg)
    {
        Thread.Sleep(2000);
        if (flag[0])
        {
            return;
        }

        for (int t = 2000; t < milliseconds; t += 1500)
        {
            Thread.Sleep(1500);
            if (flag[0])
            {
                return;
            }

            Console.Write(".");
            Console.Out.Flush();
        }

        if (flag[0])
        {
            return;
        }

        Console.WriteLine($"\r\n{timeoutMsg}");
        Environment.Exit(1);
    }

    public static void WriteLineToFile(string file, string s, bool append = false)
    {
        using (var sw = new StreamWriter(file, append))
        {
            sw.WriteLine(s);
            sw.Flush();
        }
    }

    public static void MonitorFile(string filename)
    {
        long prev = 0;
        while (true)
        {
            Thread.Sleep(200);

            var lastWrite = LastWriteTime(filename);
            if (lastWrite == 0 || lastWrite == prev)
            {
                continue;
            }

            try
            {
                using (var sr = new StreamReader(filename))
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

    public static void ReplaceStringInFile(string file, string oldStr, string newStr)
    {
        string content;

        using (var sr = new StreamReader(file))
        {
            content = sr.ReadToEnd();
        }

        content = content.Replace(oldStr, newStr);

        using (var sw = new StreamWriter(file))
        {
            sw.Write(content);
        }
    }

    public static string ReadConfig(string file, Dictionary<string, Func<string, string>> setterDict)
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
            return e.Message;
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

        return errs.Count == 0 ? null : "\r\n    " + string.Join("\r\n    ", errs);
    }

    public static void CopyDir(string src, string dest, string ignoreExt)
    {
        var srcDir = new DirectoryInfo(src);
        if (!srcDir.Exists)
        {
            Abort($"Source directory {src} does not exist");
        }

        var destDir = new DirectoryInfo(dest);
        if (destDir.Exists)
        {
            Abort($"Destination directory {dest} already exists");
        }

        try
        {
            destDir.Create();
        }
        catch (Exception ex)
        {
            Abort(ex.Message);
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

    public static float GetTreeMemory(this Process proc)
    {
        var tree = proc.GetTree();
        var instanceNames = GetInstanceNames();
        float mem = 0.0f;

        foreach (var p in tree)
        {
            if (p.HasExited)
            {
                continue;
            }

            mem += proc.GetMemory(instanceNames);
        }

        return mem;
    }

    public static float GetMemory(this Process proc, string[] instanceNames)
    {
        var cate = "Process";
        var counter = "Working Set - Private";
        var name = proc.GetInstanceName(instanceNames);

        if (name == null)
        {
            return 0;
        }

        using (var p = new PerformanceCounter(cate, counter, name, true))
        {
            return p.NextValue() / 1024;
        }
    }

    public static string[] GetInstanceNames()
    {
        return new PerformanceCounterCategory("Process").GetInstanceNames();
    }

    public static string GetInstanceName(this Process proc, string[] instanceNames)
    {
        foreach (var name in instanceNames)
        {
            if (!name.StartsWith(proc.ProcessName))
            {
                continue;
            }

            using (var pfc = new PerformanceCounter("Process", "ID Process", name, true))
            {
                if (proc.Id == (int) pfc.RawValue)
                {
                    return name;
                }
            }
        }

        return null;
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

    public static string ToPrettyTable(string[,] mat)
    {
        var m = mat.GetLength(0);
        var n = mat.GetLength(1);
        var w = new int[n + 1];
        var buf = new string[n + 1];
        var result = new string[2 * m + 1];

        w[0] = (m - 1).ToString().Length;
        for (var j = 0; j < n; j++)
        {
            w[j + 1] = 1;
            for (var i = 0; i < m; i++)
            {
                w[j + 1] = Math.Max(w[j + 1], mat[i, j].Length);
            }
        }

        for (var j = 0; j <= n; j++)
        {
            buf[j] = "".PadRight(w[j], '-');
        }

        result[0] = $"+-{string.Join("-+-", buf)}-+";

        for (var i = 0; i < m; i++)
        {
            var si = (i == 0) ? "" : i.ToString();
            buf[0] = si.PadRight(w[0], ' ');

            for (var j = 0; j < n; j++)
            {
                buf[j + 1] = mat[i, j].PadRight(w[j + 1], ' ');
            }

            result[2 * i + 1] = $"| {string.Join(" | ", buf)} |";
            result[2 * i + 2] = result[0];
        }

        return string.Join("\r\n", result);
    }

    public static bool InsertInto<T>(List<T> list, T el, Func<T, T, bool> isDepend)
    {
        int n = list.Count, i, j;

        for (i = n - 1; i >= 0; i--)
        {
            if (isDepend(el, list[i]))
            {
                break;
            }
        }

        if (i == -1)
        {
            list.Insert(0, el);
            return true;
        }

        for (j = 0; j < n; j++)
        {
            if (isDepend(list[j], el))
            {
                break;
            }
        }

        if (j == n)
        {
            list.Add(el);
            return true;
        }

        if (i >= j)
        {
            return false;
        }

        list.Insert(j, el);
        return true;
    }

    public static bool MatchRegex(string input, string regex)
    {
        var rx = new Regex($"^{regex}$", RegexOptions.Compiled);
        return rx.IsMatch(input);
    }

    public static bool IsDigit(this string input)
    {
        return MatchRegex(input, @"\d+");
    }
}