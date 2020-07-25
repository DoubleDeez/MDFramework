using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

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
        public const string SHOULD_SHOW_BUFFER_SIZE = "ShouldShowBufferSize";

        /// <summary>
        /// First key string is the section, inner dictionary key is the key
        /// </summary>
        private Dictionary<string, Dictionary<string, object>> Configuration = new Dictionary<string, Dictionary<string, object>>();

        // LOGGING
        public const string DEFAULT_LOG_LEVEL = "Default";

        public enum ConfigurationSections
        {
            GameInstance,
            Replicator,
            GameSynchronizer,
            GameClock,
            Logging
        }

        public override void _Ready()
        {
            
        }

        /// <summary>
        /// Load our configuration
        /// </summary>
        public void LoadConfiguration()
        {
            // Load internal config first
            #if DEBUG
                LoadConfiguration("MDConfigDebug.ini");
                LoadConfiguration("CustomMDConfigDebug.ini");
            #else
                LoadConfiguration("MDConfigExport.ini");
                LoadConfiguration("CustomMDConfigExport.ini");
            #endif
        }

        /// <summary>
        /// Finds and loads the file with the given name. 
        /// This will overwrite any existing section+keys with the same value already in our configuration.
        /// </summary>
        /// <param name="name">The name of the file to load</param>
        /// <returns>True if it could be found and loaded, false if not</returns>
        public bool LoadConfiguration(string name)
        {
            String path = FindFile(name);
            if (path == "")
            {
                return false;
            }

            ConfigFile conFile = new ConfigFile();
            conFile.Load(path);
            foreach (string section in conFile.GetSections())
            {
                foreach (string key in conFile.GetSectionKeys(section))
                {
                    if (!Configuration.ContainsKey(section))
                    {
                        // Add a new section
                        Configuration.Add(section, new Dictionary<string, object>());
                    }

                    if (!Configuration[section].ContainsKey(key))
                    {
                        // Add a new key
                        Configuration[section].Add(key, conFile.GetValue(section, key));
                    }
                    else
                    {
                        // Overwrite existing key
                        Configuration[section][key] = conFile.GetValue(section, key);
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Get the value from our configuration or default if we don't have it
        /// </summary>
        /// <param name="Category">The category to get</param>
        /// <param name="Key">The key to get</param>
        /// <param name="Default">The default value</param>
        /// <returns>If found the value or the default value if not found</returns>
        private object GetValue(string Category, string Key, object Default)
        {
            if (!Configuration.ContainsKey(Category))
            {
                return Default;
            }

            if (!Configuration[Category].ContainsKey(Key))
            {
                return Default;
            }

            return Configuration[Category][Key];
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
            return GetValue(Category.ToString(), Key, Default).ToString();
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
            object value = GetValue(Category.ToString(), Key, Default);
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
            object value = GetValue(Category.ToString(), Key, Default);
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
            object value = GetValue(Category.ToString(), Key, null);
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
        /// Get an enum value from the configuration
        /// </summary>
        /// <param name="Category">The category</param>
        /// <param name="Key">The key</param>
        /// <param name="Default">The default value</param>
        /// <typeparam name="T">The enum type</typeparam>
        /// <returns>The value if found, default if not</returns>
        public T GetEnum<T>(ConfigurationSections Category, string Key, T Default) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                return Default;
            }

            string ValueAsString = GetString(Category, Key, "");
            if (String.IsNullOrWhiteSpace(ValueAsString))
            {
                return Default;
            }

            foreach (T item in Enum.GetValues(typeof(T)))
            {
                if (item.ToString().ToLower().Equals(ValueAsString.Trim().ToLower()))
                {
                    return item;
                }
            }

            return Default;
        }

        /// <summary>
        /// Checks if the configuration has set a value for the given category and key
        /// </summary>
        /// <param name="Category">The category</param>
        /// <param name="Key">The key</param>
        /// <returns>True if the config has a value</returns>
        public bool HasValue(ConfigurationSections Category, string Key)
        {
            String CatKey = Category.ToString();
            return Configuration.ContainsKey(CatKey) && Configuration[CatKey].ContainsKey(Key);
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