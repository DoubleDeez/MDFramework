using Godot;
using System;
using System.Reflection;

namespace MD
{
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

        public void LoadConfiguration()
        {
            Error err = Configuration.Load(GetConfigFilePath());
        }

        public string GetString(ConfigurationSections Category, string Key, string Default)
        {
            return Configuration.GetValue(Category.ToString(), Key, Default).ToString();
        }

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

        protected virtual String GetConfigFilePath()
        {
            #if DEBUG
                return FindFile("MDConfigDebug.ini");
            #else
                return FindFile("MDConfigExport.ini");
            #endif
        }

        protected String FindFile(string name)
        {
            return FindFileInt(name, "res://");
        }

        protected String FindFileInt(string name, string path)
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