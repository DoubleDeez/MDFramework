using Godot;
using System;
using System.Reflection;

namespace MD
{
    public class MDConfiguration : Node
    {
        public enum ConfiugrationSections
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

        public string GetString(ConfiugrationSections Category, string Key, string Default)
        {
            return Configuration.GetValue(Category.ToString(), Key, Default).ToString();
        }

        public int GetInt(ConfiugrationSections Category, string Key, int Default)
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

        public bool GetBool(ConfiugrationSections Category, string Key, bool Default)
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

        public Type GetType(ConfiugrationSections Category, string Key, Type Default)
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