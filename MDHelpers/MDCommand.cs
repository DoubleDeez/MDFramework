using System;
using System.Collections.Generic;
using System.Reflection;
using static Godot.StringExtensions;

/*
 * MDCommand
 *
 * Class allow functions to be registered as commands to be accessed via an MDConsole instance
 */
public static class MDCommand
{
    private const string LOG_CAT = "MDCommand";

    // Registers all the methods marked with an MDCommandAttribute
    public static void RegisterCommandAttributes(object Instance)
    {
        MethodInfo[] Methods = Instance.GetType().GetMethods();
        foreach (MethodInfo Method in Methods)
        {
            MDCommandAttribute CmdAttr = Method.GetCustomAttribute(typeof(MDCommandAttribute)) as MDCommandAttribute;
            if (CmdAttr != null)
            {
                RegisterCommand(Instance, Method);
            }
        }
    }

    // Register a method as a command
    public static void RegisterCommand(object Instance, MethodInfo Method)
    {
        if (Method == null)
        {
            return;
        }

        string HelpText = Method.Name;
        foreach (ParameterInfo ParamInfo in Method.GetParameters())
        {
            string ParamString = ParamInfo.ToString();
            int DotIndex = ParamString.LastIndexOf(".");
            if (DotIndex >= 0)
            {
                ParamString = ParamString.Substring(DotIndex + 1);
            }
            HelpText += " [" + ParamString + "]";
        }
        
        RegisterCommand(Instance, Method, HelpText);
    }

    // Register a command, with custom help text that can be displayed in the console
    public static void RegisterCommand(object Instance, MethodInfo Method, string HelpText)
    {
        if (Method == null)
        {
            return;
        }

        if (CommandMap == null)
        {
            CommandMap = new Dictionary<string, CommandInfo>();
        }

        string MethodName = Method.Name.ToLower();
        if (CommandMap.ContainsKey(MethodName))
        {
            MDLog.Log(LOG_CAT, MDLogLevel.Warn, "Command with name [{0}] is already registered, it will be replaced", Method.Name);
        }

        CommandInfo NewCommand;
        NewCommand.HelpText = HelpText;
        NewCommand.Instance = Instance;
        NewCommand.Method = Method;

        CommandMap[MethodName] = NewCommand;
    }

    // Call a registered command via its name
    public static bool InvokeCommand(string Command)
    {
        string[] Args = Command.Split(" ", false);
        if (Args.Length == 0)
        {
            // empty string
            return false;
        }

        string CmdName = Args[0].ToLower();
        if (!CommandMap.ContainsKey(CmdName))
        {
            // Command not registered
            return false;
        }

        CommandInfo CmdInfo = CommandMap[CmdName];
        ParameterInfo[] Params = CmdInfo.Method.GetParameters();
        if (Args.Length - 1 != Params.Length)
        {
            // Wrong number of arguments
            return false;
        }

        List<object> CmdParams = new List<object>();
        int ArgIndex = 1;
        foreach (ParameterInfo ParamInfo in Params)
        {
            object Param = Convert.ChangeType(Args[ArgIndex++], ParamInfo.ParameterType);
            CmdParams.Add(Param);
        }

        CmdInfo.Method.Invoke(CmdInfo.Instance, CmdParams.ToArray());

        return true;
    }

    private static Dictionary<string, CommandInfo> CommandMap;

    // Small struct containing the info to call a command
    struct CommandInfo
    {
        public string HelpText;

        public object Instance;

        public MethodInfo Method;
    }
}