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

    public long GetMicroSeconds()
    {
        return Timer.ElapsedTicks / 10;
    }

    public void Dispose()
    {
        Timer.Stop();
        if (MDArguments.HasArg("logprofile"))
        {
            MDLog.Info(LOG_CAT, "Profiling [{0}] took {1}us", ProfileName, GetMicroSeconds());
        }
    }

    private const string LOG_ARG = "logprofile";
    private const string LOG_CAT = "LogProfiler";
    private Stopwatch Timer = new Stopwatch();
    private string ProfileName;
}