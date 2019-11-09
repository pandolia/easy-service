using System;
using System.Threading;

class Program
{
    public static bool running;

    public static void Log(string s)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {s}");
    }

    public static void Loop()
    {
        Log("Started SampleWorker(c# version)");
        while (running)
        {
            Log("Running");
            Thread.Sleep(1000);
        }
        Log("Stopped SampleWorker(c# version)");
    }

    public static void Main()
    {
        var th = new Thread(Loop);

        running = true;
        th.Start();

        var msg = Console.ReadLine();
        Log($"Received message \"{msg}\" from the Monitor");

        running = false;
        th.Join();
    }
}