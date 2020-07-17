using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MD
{
    /// <summary>
    /// Profiler implementation
    /// </summary>
    public class MDProfiler : IDisposable
    {
        private const string LOG_ARG = "logprofile";
        private const string LOG_CAT = "LogProfiler";

        private static HashSet<string> _enabledProfiles;

        private string ProfileName;
        private string LowerProfileName;
        private Stopwatch Timer = new Stopwatch();

        public MDProfiler(string InProfileName)
        {
            ProfileName = InProfileName;
            LowerProfileName = ProfileName.ToLower();
            Timer.Start();
        }

        /// <summary>
        /// Initialize the profiler
        /// </summary>
        public static void Initialize()
        {
            _enabledProfiles = new HashSet<string>();
            MDCommands.RegisterCommandAttributes(typeof(MDProfiler));
        }

        /// <summary>
        /// Enables a profile
        /// </summary>
        /// <param name="ProfileName">The name of the profile</param>
        [MDCommand]
        public static void EnableProfile(string ProfileName)
        {
            string LowerProfileName = ProfileName.ToLower();
            if (_enabledProfiles.Contains(LowerProfileName) == false)
            {
                _enabledProfiles.Add(LowerProfileName);
            }
        }

        /// <summary>
        /// Disables the profile
        /// </summary>
        /// <param name="ProfileName">Name of the profile</param>
        [MDCommand]
        public static void DisableProfile(string ProfileName)
        {
            string LowerProfileName = ProfileName.ToLower();
            if (_enabledProfiles.Contains(LowerProfileName))
            {
                _enabledProfiles.Remove(LowerProfileName);
            }
        }

        /// <summary>
        /// Toggles a profile between enabled/disabled
        /// </summary>
        /// <param name="ProfileName">Name of the profile</param>
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

        /// <summary>
        /// Get micro seconds since profiler was created
        /// </summary>
        /// <returns>Microseconds since profiler was created</returns>
        public long GetMicroSeconds()
        {
            return Timer.ElapsedTicks / 10;
        }

        /// <summary>
        /// Disposes the profiler
        /// </summary>
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