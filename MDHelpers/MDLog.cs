using System;
using System.Collections.Generic;
using System.Diagnostics;
using Directory = Godot.Directory;
using GDError = Godot.Error;
using File = Godot.File;
using RandomNumberGenerator = Godot.RandomNumberGenerator;

namespace MD
{
    public enum MDLogLevel
    {
        Force, // Always logs
        Trace,
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }

    public struct MDLogProperties
    {
        // Minimum LogLevel required to write this log to file
        public MDLogLevel FileLogLevel { get; set; }

        // Minimum LogLevel required to write this log to the console
        public MDLogLevel ConsoleLogLevel { get; set; }

        public MDLogProperties(MDLogLevel LogLevel)
        {
            FileLogLevel = LogLevel;
            ConsoleLogLevel = LogLevel;
        }

        public MDLogProperties(MDLogLevel InFileLogLevel, MDLogLevel InConsoleFileLogLevel)
        {
            FileLogLevel = InFileLogLevel;
            ConsoleLogLevel = InConsoleFileLogLevel;
        }
    }

    /// <summary>
    /// Provides methods of logging different categories at different levels.
    /// If no log properties are added for a category, logs for that category will show in console and be written to file.
    /// </summary>
    public static class MDLog
    {
        private const string EXT_LOG = ".log";
        private const string LOG_CAT = "Log";

        private static string FullLogFilePath;
        private static File LogFile;
        private static Dictionary<string, MDLogProperties> LogProperties;

        /// <summary>
        /// Initialize internal data and set the directory to store log files
        /// </summary>
        /// <param name="LogDir">The log directory to use</param>
        public static void Initialize(string LogDir)
        {
            LogProperties = new Dictionary<string, MDLogProperties>();
            InitLogFile(LogDir);
            MDCommands.RegisterCommandAttributes(typeof(MDLog));
        }

        /// <summary>
        /// Logs the message (supports formatting) in accordance with the LogProperties set for the specified log category
        /// </summary>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="LogLevel">The log level</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void Log(string CategoryName, MDLogLevel LogLevel, string Message, params object[] Args)
        {
            // TODO - Get calling method's name automatically: https://stackoverflow.com/a/5443690
            bool LogFile = true;
            bool LogConsole = true;
            MDConfiguration Config = MDStatics.GetGameInstance().GetConfiguration();

            if (Config.HasValue(MDConfiguration.ConfigurationSections.Logging, CategoryName))
            {
                MDLogLevel ConfigLogLevel = Config.GetEnum<MDLogLevel>(MDConfiguration.ConfigurationSections.Logging, CategoryName, MDLogLevel.Trace);
                LogFile = ConfigLogLevel <= LogLevel || LogLevel == MDLogLevel.Force;
                LogConsole = ConfigLogLevel <= LogLevel || LogLevel == MDLogLevel.Force;
            }
            else if (LogProperties.ContainsKey(CategoryName))
            {
                MDLogProperties LogProps = LogProperties[CategoryName];
                LogFile = LogProps.FileLogLevel <= LogLevel || LogLevel == MDLogLevel.Force;
                LogConsole = LogProps.ConsoleLogLevel <= LogLevel || LogLevel == MDLogLevel.Force;
            }
            else
            {
                MDLogLevel DefaultLogLevel = Config.GetEnum<MDLogLevel>(MDConfiguration.ConfigurationSections.Logging, MDConfiguration.DEFAULT_LOG_LEVEL, MDLogLevel.Trace);
                LogFile = DefaultLogLevel <= LogLevel || LogLevel == MDLogLevel.Force;
                LogConsole = DefaultLogLevel <= LogLevel || LogLevel == MDLogLevel.Force;
            }

            if (LogFile || LogConsole)
            {
                int PeerID = MDStatics.GetPeerId();
                MDNetMode NetMode = MDStatics.GetNetMode();
                string ClientID = "PEER " + PeerID;
                if (NetMode == MDNetMode.Standalone)
                {
                    ClientID = "STANDALONE";
                }
                else if (NetMode == MDNetMode.Server)
                {
                    ClientID = "SERVER";
                }

                string FullMessage = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "]" +
                                     "[" + (Godot.Engine.GetIdleFrames() % 1000).ToString("D3") + "]" +
                                     "[" + ClientID + "] " +
                                     "[" + CategoryName + "::" + LogLevel + "] " +
                                     string.Format(Message, Args);

                if (LogFile)
                {
                    LogToFile(FullMessage);
                }

                if (LogConsole)
                {
                    LogToConsole(FullMessage);
                }
            }

            if (LogLevel == MDLogLevel.Fatal)
            {
                DebugBreak();
            }
        }

        /// <summary>
        /// Calls Log with level == force
        /// </summary>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void Force(string CategoryName, string Message, params object[] Args)
        {
            Log(CategoryName, MDLogLevel.Force, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == fatal
        /// </summary>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void Fatal(string CategoryName, string Message, params object[] Args)
        {
            Log(CategoryName, MDLogLevel.Fatal, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == error
        /// </summary>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void Error(string CategoryName, string Message, params object[] Args)
        {
            Log(CategoryName, MDLogLevel.Error, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == warn
        /// </summary>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void Warn(string CategoryName, string Message, params object[] Args)
        {
            Log(CategoryName, MDLogLevel.Warn, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == info
        /// </summary>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void Info(string CategoryName, string Message, params object[] Args)
        {
            Log(CategoryName, MDLogLevel.Info, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == debug
        /// </summary>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void Debug(string CategoryName, string Message, params object[] Args)
        {
            Log(CategoryName, MDLogLevel.Debug, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == trace
        /// </summary>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void Trace(string CategoryName, string Message, params object[] Args)
        {
            Log(CategoryName, MDLogLevel.Trace, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == force
        /// </summary>
        /// <param name="Condition">The condition</param>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void CForce(bool Condition, string CategoryName, string Message, params object[] Args)
        {
            CLog(Condition, CategoryName, MDLogLevel.Force, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == fatal
        /// </summary>
        /// <param name="Condition">The condition</param>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void CFatal(bool Condition, string CategoryName, string Message, params object[] Args)
        {
            CLog(Condition, CategoryName, MDLogLevel.Fatal, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == error
        /// </summary>
        /// <param name="Condition">The condition</param>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void CError(bool Condition, string CategoryName, string Message, params object[] Args)
        {
            CLog(Condition, CategoryName, MDLogLevel.Error, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == warn
        /// </summary>
        /// <param name="Condition">The condition</param>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void CWarn(bool Condition, string CategoryName, string Message, params object[] Args)
        {
            CLog(Condition, CategoryName, MDLogLevel.Warn, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == info
        /// </summary>
        /// <param name="Condition">The condition</param>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void CInfo(bool Condition, string CategoryName, string Message, params object[] Args)
        {
            CLog(Condition, CategoryName, MDLogLevel.Info, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == debug
        /// </summary>
        /// <param name="Condition">The condition</param>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void CDebug(bool Condition, string CategoryName, string Message, params object[] Args)
        {
            CLog(Condition, CategoryName, MDLogLevel.Debug, Message, Args);
        }

        /// <summary>
        /// Calls Log with level == trace
        /// </summary>
        /// <param name="Condition">The condition</param>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void CTrace(bool Condition, string CategoryName, string Message, params object[] Args)
        {
            CLog(Condition, CategoryName, MDLogLevel.Trace, Message, Args);
        }

        /// <summary>
        /// Sames as Log() except it only logs if Condition == true
        /// </summary>
        /// <param name="Condition">The condition</param>
        /// <param name="CategoryName">The category to log in</param>
        /// <param name="LogLevel">The log level</param>
        /// <param name="Message">The message</param>
        /// <param name="Args">Arguments</param>
        public static void CLog(bool Condition, string CategoryName, MDLogLevel LogLevel, string Message,
            params object[] Args)
        {
            if (Condition)
            {
                Log(CategoryName, LogLevel, Message, Args);
            }
        }

        /// <summary>
        /// Adds log category properties to be referenced when making logs of that type
        /// </summary>
        /// <param name="CategoryName">The category name</param>
        /// <param name="LogProps">The logging properties</param>
        public static void AddLogCategoryProperties(string CategoryName, MDLogProperties LogProps)
        {
            LogProperties[CategoryName] = LogProps;
        }

        /// <summary>
        /// Sets log level with a command
        /// </summary>
        /// <param name="CategoryName">The category to set</param>
        /// <param name="LogLevel">The log level</param>
        [MDCommand]
        public static void SetLogLevel(string CategoryName, MDLogLevel LogLevel)
        {
            LogProperties[CategoryName] = new MDLogProperties(LogLevel);
        }

        private static bool ShouldLogToGDPrint()
        {
            return MDStatics.GetGameInstance().GetConfiguration().GetBool(MDConfiguration.ConfigurationSections.GameInstance, MDConfiguration.LOG_CONSOLE_TO_GD_PRINT, true);
        }

        // Prints the message to the console
        private static void LogToConsole(string Message)
        {

            if (ShouldLogToGDPrint())
            {
                Godot.GD.Print(Message);
            }
            else
            {
                Console.WriteLine(Message);
            }
        }

        // Writes the message to the log file
        private static void LogToFile(string Message)
        {
            OpenLogFile();
            LogFile.StoreLine(Message);
            LogFile.Close();
        }

        // Creates our time-stamped log file and opens it for writing
        private static void InitLogFile(string LogDir)
        {
            RandomNumberGenerator rnd = new RandomNumberGenerator();
            rnd.Randomize();
            FullLogFilePath = $"{LogDir}{DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss")}_{rnd.RandiRange(10000,99999).ToString()}{EXT_LOG}";
            if (CreateLogDirectoryIfNotExists(LogDir))
            {
                LogFile = new File();
                LogFile.Open(FullLogFilePath, File.ModeFlags.Write);
                Log(LOG_CAT, MDLogLevel.Info, $"Created log file {FullLogFilePath}");
                LogFile.Close();
            }
            else
            {
                Console.WriteLine("Failed to create log directory.");
            }
        }

        // Opens the log file for writing
        private static void OpenLogFile()
        {
            LogFile.Open(FullLogFilePath, File.ModeFlags.ReadWrite);
            LogFile.SeekEnd();
        }

        // Ensures the directory for log files exists
        private static bool CreateLogDirectoryIfNotExists(string FileDir)
        {
            Directory dir = new Directory();
            if (!dir.DirExists(FileDir))
            {
                return dir.MakeDirRecursive(FileDir) == GDError.Ok;
            }

            return true;
        }

        // Breaks the debugger
        [Conditional("DEBUG")]
        private static void DebugBreak()
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
    }
}