using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MD
{
    public class MDProfiler : IDisposable
    {
        private const string LOG_ARG = "logprofile";
        private const string LOG_CAT = "LogProfiler";

        private static HashSet<string> _enabledProfiles;

        private string ProfileName;
        private string LowerProfileName;
        private Stopwatch Timer = new Stopwatch();

        public static void Initialize()
        {
            _enabledProfiles = new HashSet<string>();
            MDCommands.RegisterCommandAttributes(typeof(MDProfiler));
        }

        [MDCommand]
        public static void EnableProfile(string ProfileName)
        {
            string LowerProfileName = ProfileName.ToLower();
            if (_enabledProfiles.Contains(LowerProfileName) == false)
            {
                _enabledProfiles.Add(LowerProfileName);
            }
        }

        [MDCommand]
        public static void DisableProfile(string ProfileName)
        {
            string LowerProfileName = ProfileName.ToLower();
            if (_enabledProfiles.Contains(LowerProfileName))
            {
                _enabledProfiles.Remove(LowerProfileName);
            }
        }

        [MDCommand]
        public static void ToggleProfile(string ProfileName)
        {
            string LowerProfileName = ProfileName.ToLower();
            if (_enabledProfiles.Contains(LowerProfileName))
            {
                _enabledProfiles.Remove(LowerProfileName);
            }
            else
            {
                _enabledProfiles.Add(LowerProfileName);
            }
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
            if (_enabledProfiles.Contains(LowerProfileName) || MDArguments.HasArg(LOG_ARG))
            {
                MDLog.Info(LOG_CAT, $"Profiling [{ProfileName}] took {GetMicroSeconds()} us");
            }
        }
    }
}