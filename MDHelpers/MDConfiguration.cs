using Godot;
using System;
using System.Reflection;

namespace MD
{
    /// <summary>
    /// Handles reading of the configuration for the MDFramework
    /// </summary>
    public class MDConfiguration : Node
    {
        // GAME INSTANCE
        public const string LOG_DIRECTORY = "LogDirectory";
        public const string CONSOLE_KEY = "ConsoleKey";
        public const string ON_SCREEN_DEBUG_KEY = "OnScreenDebugKey";
        public const string CONSOLE_ENABLED = "ConsoleEnabled";
        public const string ON_SCREEN_DEBUG_ENABLED = "OnScreenDebugEnabled";
        public const string ON_SCREEN_DEBUG_ADD_BASIC_INFO = "OnScreenDebugAddBasicInfo";
        public const string USE_UPNP = "UseUPNP";
        public const string REQUIRE_AUTO_REGISTER = "RequireAutoRegister";
        public const string GAME_SYNCHRONIZER_ENABLED = "GameSynchronizerEnabled";
        public const string USE_SCENE_BUFFER = "UseSceneBuffer";
        public const string GAME_CLOCK_ACTIVE = "GameClockActive";
        public const string GAME_SESSION_TYPE = "GameSessionType";
        public const string GAME_SYNCHRONIZER_TYPE = "GameSynchronizerType";
        public const string GAME_CLOCK_TYPE = "GameClockType";
        public const string REPLICATOR_TYPE = "ReplicatorType";
        public const string PLAYER_INFO_TYPE = "PlayerInfoType";
        public const string LOG_CONSOLE_TO_GD_PRINT = "LogConsoleToGDPrint";

        // GAME SYNCHRONIZER
        public const string PING_INTERVAL = "PingInterval";
        public const string UNPAUSE_COUNTDOWN_DURATION = "UnpauseCountdownDuration";
        public const string PINGS_TO_KEEP_FOR_AVERAGE = "PingsToKeepForAverage";
        public const string INITIAL_MEASUREMENT_COUNT = "InitialMeasurementCount";
        public const string INITIAL_MEASUREMENT_COUNT_BEFORE_RESUME = "InitialMeasurementCountBeforeResume";
        public const string PAUSE_ON_JOIN = "PauseOnJoin";
        public const string DELAY_REPLICATION_UNTIL_ALL_NODES_SYNCHED = "DelayReplicatorUntilAllNodesSynched";
        public const string ACTIVE_PING_ENABLED = "ActivePingEnabled";

        // GAME CLOCK
        public const string MINIMUM_OFFSET = "MinimumOffset";
        public const string OFFSET_BUFFER = "OffsetBuffer";
        public const string MAX_TICK_DESYNCH = "MaxTickDesynch";

        // REPLICATOR
        public const string FRAME_INTERVAL = "FrameInterval";

        public enum ConfigurationSections
        {
            GameInstance,
            Replicator,
            GameSynchronizer,
            GameClock
        }

        ConfigFile Configuration = new ConfigFile();

        public override void _Ready()
        {
            
        }

        /// <summary>
        /// Load our configuration
        /// </summary>
        public void LoadConfiguration()
        {
            Error err = Configuration.Load(GetConfigFilePath());
        }

        /// <summary>
        /// Get a string value from the configuration
        /// </summary>
        /// <param name="Category">The category</param>
        /// <param name="Key">The key</param>
        /// <param name="Default">The default value</param>
        /// <returns>The value if found, default if not</returns>
        public string GetString(ConfigurationSections Category, string Key, string Default)
        {
            return Configuration.GetValue(Category.ToString(), Key, Default).ToString();
        }

        /// <summary>
        /// Get a int value from the configuration
        /// </summary>
        /// <param name="Category">The category</param>
        /// <param name="Key">The key</param>
        /// <param name="Default">The default value</param>
        /// <returns>The value if found, default if not</returns>
        public int GetInt(ConfigurationSections Category, string Key, int Default)
        {
            object value = Configuration.GetValue(Category.ToString(), Key, Default);
            if (value.GetType() == typeof(Int32))
            {
                return (int)value;
            }
            else
            {
                return Int32.Parse(value.ToString());
            }
        }

        /// <summary>
        /// Get a bool value from the configuration
        /// </summary>
        /// <param name="Category">The category</param>
        /// <param name="Key">The key</param>
        /// <param name="Default">The default value</param>
        /// <returns>The value if found, default if not</returns>
        public bool GetBool(ConfigurationSections Category, string Key, bool Default)
        {
            object value = Configuration.GetValue(Category.ToString(), Key, Default);
            if (value.GetType() == typeof(Boolean))
            {
                return (bool)value;
            }
            else
            {
                return Boolean.Parse(value.ToString());
            }
        }

        /// <summary>
        /// Get a type value from the configuration
        /// </summary>
        /// <param name="Category">The category</param>
        /// <param name="Key">The key</param>
        /// <param name="Default">The default value</param>
        /// <returns>The value if found, default if not</returns>
        public Type GetType(ConfigurationSections Category, string Key, Type Default)
        {
            object value = Configuration.GetValue(Category.ToString(), Key, null);
            if (value == null)
            {
                return null;
            }
            Type returnType = Type.GetType(value.ToString());
            if (returnType != null)
            {
                return returnType;
            }
            return Default;
        }

        /// <summary>
        /// Get the path of the configuration file
        /// </summary>
        /// <returns>The path to the file</returns>
        protected virtual String GetConfigFilePath()
        {
            // Attempt to load custom export config
            String configPath = FindFile("CustomMDConfigExport.ini");
            if (configPath == null || configPath == "")
            {
                // Attempt to load custom debug config
                configPath = FindFile("CustomMDConfigDebug.ini");
            }

            // If any custom config was found use it
            if (configPath != null && configPath != "")
            {
                return configPath;
            }

            // Use internal default configs
            #if DEBUG
                return FindFile("MDConfigDebug.ini");
            #else
                return FindFile("MDConfigExport.ini");
            #endif
        }

        /// <summary>
        /// Finds the given file inside the project
        /// </summary>
        /// <param name="name">Name of the file to find</param>
        /// <returns>The path to the file</returns>
        protected String FindFile(string name)
        {
            return FindFileInt(name, "res://");
        }

        private String FindFileInt(string name, string path)
        {
            Directory dir = new Directory();
            dir.Open(path);
            dir.ListDirBegin(true, true);
            while (true)
            {
                String filePath = dir.GetNext();
                if (filePath == "")
                {
                    break;
                } 
                else if (filePath.EndsWith(name))
                {
                    return path + filePath;
                }
                else if (dir.CurrentIsDir())
                {
                    String fileName = FindFileInt(name, path + filePath + "/");
                    if (fileName != "")
                    {
                        return fileName;
                    }
                }
            }
            dir.ListDirEnd();

            return "";
        }

    }
}