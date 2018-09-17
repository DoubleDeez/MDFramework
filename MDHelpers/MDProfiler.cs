using System;
using System.Diagnostics;

public class MDProfiler : IDisposable
{
    private MDProfiler()
    {
    }

    public MDProfiler(string InProfileName)
    {
        ProfileName = InProfileName;
        Timer.Start();
    }

    public void Dispose()
    {
        Timer.Stop();
        MDLog.Info(LOG_CAT, "Profiling [{0}] took {1}us", ProfileName, Timer.ElapsedTicks / 10);
    }

    private const string LOG_CAT = "LogProfiler";
    private Stopwatch Timer = new Stopwatch();
    private string ProfileName;
}