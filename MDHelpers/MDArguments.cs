using System;
using System.Collections.Generic;
using static Godot.StringExtensions;

/*
 * MDArguments
 *
 * Static class that parses the arguments passed into the game executable to be exposed to game code.
 */
public static class MDArguments
{
    private const string ARG_PREFIX = "-";
    private const string LOG_CAT = "MDArgs";

    // Does this argument exist?
    public static bool HasArg(string ArgKey)
    {
        return Args.ContainsKey(ArgKey);
    }

    // Empty string if not found
    public static string GetArg(string ArgKey)
    {
        return HasArg(ArgKey) ? Args[ArgKey] : "";
    }

    // -1 if not found
    public static int GetArgInt(string ArgKey)
    {
        string ArgValue = GetArg(ArgKey);
        return ArgValue.IsValidInteger() ? ArgValue.ToInt() : -1;
    }

    // -1.0f if not found
    public static float GetArgFloat(string ArgKey)
    {
        string ArgValue = GetArg(ArgKey);
        return ArgValue.IsValidFloat() ? ArgValue.ToFloat() : -1.0f;
    }

    /* Simple argument parser that generates a dictionary of arguments passed to the game application to their values.
     * Expects arguments to being with ARG_PREFIX value.
     */
    public static void PopulateArgs()
    {
        if (Args != null)
        {
            // We already parsed args
            return;
        }

        string[] ArgArray = System.Environment.GetCommandLineArgs();
        MDLog.Log(LOG_CAT, MDLogLevel.Info, "Populating Arguments: " + String.Join(" ", ArgArray));
    
        Args = new Dictionary<string, string>();
        for (int i = 0; i < ArgArray.Length; ++i)
        {
            string ThisArg = ArgArray[i].ToLower();
            if (!ThisArg.BeginsWith(ARG_PREFIX))
            {
                // TODO - Log non prefix arg or change to support non-prefixed?
                continue;
            }

            string NoPrefixArg = ThisArg.Substring(ARG_PREFIX.Length);

            // Does this arg have a value?
            int EqualIndex = NoPrefixArg.IndexOf('=');
            if (EqualIndex > 0)
            {
                string ArgKey = NoPrefixArg.Substring(0, EqualIndex);
                string ArgVal = NoPrefixArg.Substring(EqualIndex + 1);
                Args.Add(ArgKey, ArgVal);
            }
            else
            {
                Args.Add(NoPrefixArg, "");
            }
        }
    }

    // Maps arguments to their values in PopulateArgs()
    private static Dictionary<string, string> Args;
}