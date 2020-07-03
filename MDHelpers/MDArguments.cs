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

    // Maps arguments to their values in PopulateArgs()
    private static Dictionary<string, string> _args;

    // Does this argument exist?
    public static bool HasArg(string ArgKey)
    {
        return _args.ContainsKey(ArgKey);
    }

    // Empty string if not found
    public static string GetArg(string ArgKey)
    {
        return HasArg(ArgKey) ? _args[ArgKey] : "";
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
        if (_args != null)
        {
            // We already parsed args
            return;
        }

        string[] ArgArray = Environment.GetCommandLineArgs();
        MDLog.Log(LOG_CAT, MDLogLevel.Info, "Populating Arguments: " + string.Join(" ", ArgArray));

        _args = new Dictionary<string, string>();
        foreach (string Arg in ArgArray)
        {
            string ThisArg = Arg.ToLower();
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
                _args.Add(ArgKey, ArgVal);
            }
            else
            {
                _args.Add(NoPrefixArg, "");
            }
        }
    }
}