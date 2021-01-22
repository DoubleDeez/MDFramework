using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Directory = Godot.Directory;
using File = Godot.File;
using GDError = Godot.Error;
using static Godot.StringExtensions;

namespace MD
{
    /// <summary>
    /// Class that allows functions to be registered as commands to be accessed via an MDConsole instance
    /// </summary>
    public static class MDCommands
    {
        // Small struct containing the info to call a command
        private struct CommandInfo
        {
            public string HelpText;

            // TODO - Make weak
            public object Instance;

            public MethodInfo Method;

            public object[] DefaultArgs;
        }

        private const string LOG_CAT = "MDCommands";
        private const string HISTORY_FILE = "CommandHistory";
        private const string HISTORY_DIR = "user://cmd/";

        private static Dictionary<string, CommandInfo> _commandMap;

        /// <summary>
        /// Registers all the methods marked with an MDCommand
        /// </summary>
        /// <param name="Instance">The object to register for</param>
        public static void RegisterCommandAttributes(object Instance)
        {
            RegisterCommandAttributes(Instance.GetType(), Instance);
        }

        /// <summary>
        /// Unregisters all the methods marked with an MDCommand
        /// </summary>
        /// <param name="Instance">The object to unregister for</param>
        public static void UnregisterCommandAttributes(object Instance)
        {
            UnregisterCommandAttributes(Instance.GetType(), Instance);
        }

        /// <summary>
        /// Registers all the methods marked with an MDCommand of the given type
        /// </summary>
        /// <param name="ObjType">The type</param>
        /// <param name="Instance">The object to register for</param>
        public static void RegisterCommandAttributes(Type ObjType, object Instance = null)
        {
            if (MDStatics.IsInGodotNamespace(ObjType))
            {
                return;
            }
            IList<MethodInfo> Methods = ObjType.GetMethodInfos();
            foreach (MethodInfo Method in Methods)
            {
                MDCommand CmdAttr = MDReflectionCache.GetCustomAttribute<MDCommand>(Method) as MDCommand;
                if (CmdAttr != null)
                {
                    RegisterCommand(Instance, Method, CmdAttr.DefaultArgs);
                }
            }
        }

        /// <summary>
        /// Unregisters all the methods marked with an MDCommand of the given type
        /// </summary>
        /// <param name="ObjType">The type</param>
        /// <param name="Instance">The object to unregister for</param>
        public static void UnregisterCommandAttributes(Type ObjType, object Instance = null)
        {
            if (MDStatics.IsInGodotNamespace(ObjType))
            {
                return;
            }
            IList<MethodInfo> Methods = ObjType.GetMethodInfos();
            foreach (MethodInfo Method in Methods)
            {
                MDCommand CmdAttr = MDReflectionCache.GetCustomAttribute<MDCommand>(Method) as MDCommand;
                if (CmdAttr != null)
                {
                    UnregisterCommand(Instance, Method);
                }
            }
        }

        /// <summary>
        /// Register a method as a command
        /// </summary>
        /// <param name="Instance">The instance to register for</param>
        /// <param name="Method">The method</param>
        /// <param name="DefaultParams">Default parameters</param>
        public static void RegisterCommand(object Instance, MethodInfo Method, object[] DefaultParams = null)
        {
            if (Method == null)
            {
                return;
            }

            string HelpText = Method.Name;
            foreach (ParameterInfo ParamInfo in Method.GetParameters())
            {
                string ParamString = ParamInfo.ToString();
                int DotIndex = ParamString.LastIndexOf(".", StringComparison.Ordinal);
                if (DotIndex >= 0)
                {
                    ParamString = ParamString.Substring(DotIndex + 1);
                }

                HelpText += " [" + ParamString + "]";
            }

            RegisterCommand(Instance, Method, HelpText, DefaultParams);
        }

        /// <summary>
        /// Register a command, with custom help text that can be displayed in the console
        /// </summary>
        /// <param name="Instance">The instance to register for</param>
        /// <param name="Method">The method to register</param>
        /// <param name="HelpText">Help text to show in console</param>
        /// <param name="DefaultParams">Default parameters</param>
        public static void RegisterCommand(object Instance, MethodInfo Method, string HelpText,
            object[] DefaultParams = null)
        {
            if (Method == null)
            {
                return;
            }

            if (_commandMap == null)
            {
                _commandMap = new Dictionary<string, CommandInfo>();
            }

            string MethodName = Method.Name.ToLower();
            if (_commandMap.ContainsKey(MethodName))
            {
                MDLog.Warn(LOG_CAT, $"Command with name [{Method.Name}] is already registered, it will be replaced");
            }

            CommandInfo NewCommand;
            NewCommand.HelpText = HelpText;
            NewCommand.Instance = Instance;
            NewCommand.Method = Method;
            NewCommand.DefaultArgs = DefaultParams;

            _commandMap[MethodName] = NewCommand;
        }

        /// <summary>
        /// Unregister a command
        /// </summary>
        /// <param name="Instance">The instance to unregister for</param>
        /// <param name="Method">The method to unregister</param>
        public static void UnregisterCommand(object Instance, MethodInfo Method)
        {
            if (Method == null)
            {
                return;
            }

            if (_commandMap == null)
            {
                _commandMap = new Dictionary<string, CommandInfo>();
                return;
            }

            string MethodName = Method.Name.ToLower();
            if (!_commandMap.ContainsKey(MethodName))
            {
                return;
            }

            CommandInfo Command = _commandMap[MethodName];
            if (Command.Instance == Instance)
            {
                _commandMap.Remove(MethodName);
            }
        }

        /// <summary>
        /// Call a registered command via its name
        /// </summary>
        /// <param name="Command">The command to call</param>
        /// <returns>True if executed, false if not</returns>
        public static bool InvokeCommand(string Command)
        {
            string[] Args = Command.Split(" ", false);
            if (Args.Length == 0)
            {
                // empty string
                return false;
            }

            MDLog.Info(LOG_CAT, Command);
            AddCommandToHistory(Command);

            string CmdName = Args[0].ToLower();
            if (!_commandMap.ContainsKey(CmdName))
            {
                MDLog.Error(LOG_CAT, $"Command not found: [{Command}]");
                return false;
            }

            CommandInfo CmdInfo = _commandMap[CmdName];
            ParameterInfo[] Params = CmdInfo.Method.GetParameters();
            object[] ParamArray;

            // Should we use the default args?
            if (Params.Length > 0 && Args.Length == 1 && CmdInfo.DefaultArgs.Length == Params.Length)
            {
                ParamArray = CmdInfo.DefaultArgs;
            }
            else
            {
                if (Args.Length - 1 != Params.Length)
                {
                    // Wrong number of arguments
                    return false;
                }

                ParamArray = new object[Args.Length - 1];
                Array.Copy(Args, 1, ParamArray, 0, ParamArray.Length);
            }

            // Convert the strings to the appropriate type
            List<object> CmdParams = new List<object>();
            int ArgIndex = 0;
            foreach (ParameterInfo ParamInfo in Params)
            {
                if (ParamInfo.ParameterType.IsEnum)
                {
                    object Param = Enum.Parse(ParamInfo.ParameterType, ParamArray[ArgIndex++] as string, true);
                    CmdParams.Add(Param);
                }
                else
                {
                    object Param = Convert.ChangeType(ParamArray[ArgIndex++], ParamInfo.ParameterType);
                    CmdParams.Add(Param);
                }
            }

            CmdInfo.Method.Invoke(CmdInfo.Instance, CmdParams.ToArray());

            return true;
        }

        /// <summary>
        /// Get history of commands executed
        /// </summary>
        /// <returns>List of commands executed</returns>
        public static List<string> GetCommandHistory()
        {
            File CmdFile = GetHistoryFile();
            if (CmdFile == null)
            {
                return null;
            }

            string HistoryText = CmdFile.GetAsText();
            CmdFile.Close();

            List<string> CommandHistory = new List<string>(HistoryText.Split('\n'));
            CommandHistory = CommandHistory.Where(Command => !string.IsNullOrWhiteSpace(Command)).ToList();
            CommandHistory.Reverse();
            return CommandHistory;
        }

        private static void AddCommandToHistory(string Command)
        {
            List<string> History = GetCommandHistory();
            if (History.Count > 0 && History[0].Equals(Command))
            {
                // Don't store the last as last command
                return;
            }

            File CmdFile = GetHistoryFile();
            if (CmdFile == null)
            {
                return;
            }

            CmdFile.SeekEnd();
            CmdFile.StoreLine(Command);
            CmdFile.Close();
        }

        /// <summary>
        /// Get the list of valid commands
        /// </summary>
        /// <returns>The list of commands</returns>
        public static List<string> GetCommandList()
        {
            return _commandMap.Keys.ToList();
        }

        /// <summary>
        /// Get the help text of a command
        /// </summary>
        /// <param name="Command">The command to get the help text for</param>
        /// <returns>The help text if found</returns>
        public static string GetHelpText(string Command)
        {
            return _commandMap.ContainsKey(Command) ? _commandMap[Command].HelpText : "";
        }

        private static File GetHistoryFile()
        {
            if (CreateHistoryDirectoryIfNotExists(HISTORY_DIR))
            {
                string FullFilePath = HISTORY_DIR + HISTORY_FILE;
                File CmdFile = new File();
                if (!CmdFile.FileExists(FullFilePath))
                {
                    CmdFile.Open(FullFilePath, File.ModeFlags.Write);
                    CmdFile.Close();
                }

                CmdFile.Open(FullFilePath, File.ModeFlags.ReadWrite);
                return CmdFile;
            }

            MDLog.Error(LOG_CAT, "Failed to create command history directory.");
            return null;
        }

        private static bool CreateHistoryDirectoryIfNotExists(string FileDir)
        {
            Directory Dir = new Directory();
            if (!Dir.DirExists(FileDir))
            {
                return Dir.MakeDirRecursive(FileDir) == GDError.Ok;
            }

            return true;
        }
    }
}