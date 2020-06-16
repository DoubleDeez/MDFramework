using Godot;
using System;
using System.Collections.Generic;

[MDAutoRegister]
public class MDOnScreenDebug : Control
{
    private const string LOG_CAT = "LogOnScreenDebug";

    public static readonly float UPDATE_INTERVAL = 0.3f;
    public delegate String OnScreenInfoFunction();

    private Label DisplayLabel;

    private static Dictionary<String, OnScreenInfoFunction> DebugInfoList = new Dictionary<string, OnScreenInfoFunction>();

    private static bool AddedBasicInfo = false;

    protected float _updateCooldown = 0f;

    public override void _Ready()
    {
        base._Ready();

        MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Trace));
        
        this.SetAnchor(0, 0, 1, 1);
        this.SetMargin(10, 10, 0, 0);
        this.MouseFilter = MouseFilterEnum.Ignore;

        CreateControls();

        if (this.GetGameInstance().IsOnScreenDebugAddBasicInformation() && !AddedBasicInfo)
        {
            AddedBasicInfo = true;
            AddBasicInfo();
        }
    }

    public override void _Process(float delta)
    {
        _updateCooldown -= delta;
        if (_updateCooldown <= 0f)
        {
            _updateCooldown += UPDATE_INTERVAL;
            //UpdateLabel();
        }
        UpdateLabel();
    }

    ///<Summary> Adds some basic information on creation, can be toggled</summary>
    public void AddBasicInfo()
    {
        AddOnScreenDebugInfo("FPS", () => Engine.GetFramesPerSecond().ToString());
        AddOnScreenDebugInfo("Static Memory", () => MDStatics.HumanReadableSize(OS.GetStaticMemoryUsage()));
        AddOnScreenDebugInfo("Network Active: ", () => MDStatics.IsNetworkActive().ToString());
        AddOnScreenDebugInfo("PeerId: ", () => MDStatics.GetPeerId().ToString());
    }

    protected void UpdateLabel()
    {
        String fullText = "";
        foreach (String key in DebugInfoList.Keys)
        {
            try
            {
                String text = key + ": ";
                text += DebugInfoList[key].Invoke();
                fullText += text + "\n";
            }
            catch (Exception ex)
            {
                // Something went wrong
                MDLog.Debug(LOG_CAT, ex.ToString());
            }
        }

        DisplayLabel.Text = fullText;
    }

    ///<summary>Adds some info to print on the screen</summary>
    ///<param name="name">The name to display, should be unique.</param>
    ///<param name="function">Function that returns a string to display on the screen.</param>
    public static void AddOnScreenDebugInfo(String name, OnScreenInfoFunction function)
    {
        if (DebugInfoList.ContainsKey(name))
        {
            DebugInfoList[name] = function;
        }
        else
        {
            DebugInfoList.Add(name, function);
        }
    }

    ///<summary>Removes the information from the on screen debug</summary>
    public static bool RemoveOnScreenDebugInfo(String name)
    {
        if (DebugInfoList.ContainsKey(name))
        {
            DebugInfoList.Remove(name);
            return true;
        }

        return false;
    }

    public void Close()
    {
        this.RemoveAndFree();
    }

    // Creates the UI control that accepts text input
    private void CreateControls()
    {
        // Label
        {
            DisplayLabel = new Label();
            DisplayLabel.Name = nameof(DisplayLabel);
            DisplayLabel.Text = "fheuhfuhe";
            DisplayLabel.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(DisplayLabel);
        }
    }
}
