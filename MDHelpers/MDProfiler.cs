using System;
using System.Collections.Generic;
using System.Diagnostics;

public class MDProfiler : IDisposable
{
    private static HashSet<string> EnabledProfiles;
    public static void Initialize()
    {
        EnabledProfiles = new HashSet<string>();
        MDCommands.RegisterCommandAttributes(typeof(MDProfiler));
    }

    [MDCommand]
    public static void EnableProfile(string ProfileName)
    {
        string LowerProfileName = ProfileName.ToLower();
        if (EnabledProfiles.Contains(LowerProfileName) == false)
        {
            EnabledProfiles.Add(LowerProfileName);
        }
    }

    [MDCommand]
    public static void DisableProfile(string ProfileName)
    {
        string LowerProfileName = ProfileName.ToLower();
        if (EnabledProfiles.Contains(LowerProfileName))
        {
            EnabledProfiles.Remove(LowerProfileName);
        }
    }

    [MDCommand]
    public static void ToggleProfile(string ProfileName)
    {
        string LowerProfileName = ProfileName.ToLower();
        if (EnabledProfiles.Contains(LowerProfileName))
        {
            EnabledProfiles.Remove(LowerProfileName);
        }
        else
        {
            EnabledProfiles.Add(LowerProfileName);
        }
    }

    private MDProfiler()
    {
    }

    public MDProfiler(string InProfileName)
    {
        ProfileName = InProfileName;
        LowerProfileName = ProfileName.ToLower();
        Timer.Start();
    }

    public long GetMicroSeconds()
    {
        return Timer.ElapsedTicks / 10;
    }

    public void Dispose()
    {
        Timer.Stop();
        if (EnabledProfiles.Contains(LowerProfileName) || MDArguments.HasArg(LOG_ARG))
        {
            MDLog.Info(LOG_CAT, "Profiling [{0}] took {1}us", ProfileName, GetMicroSeconds());
        }
    }

    private const string LOG_ARG = "logprofile";
    private const string LOG_CAT = "LogProfiler";
    private Stopwatch Timer = new Stopwatch();
    private string ProfileName;
    private string LowerProfileName;
}