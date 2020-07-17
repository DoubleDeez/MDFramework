using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MD
{
    internal class OnScreenDebugInfo
    {
        public OnScreenInfoFunction InfoFunction;

        public Color Color;

        public OnScreenDebugInfo(OnScreenInfoFunction InfoFunction, Color Color)
        {
            this.InfoFunction = InfoFunction;
            this.Color = Color;
        }

        public OnScreenDebugInfo(OnScreenInfoFunction InfoFunction) : this(InfoFunction, Colors.White)
        {
        }
    }
    public delegate string OnScreenInfoFunction();

    /// <summary>
    /// On screen debugger
    /// </summary>
    [MDAutoRegister]
    public class MDOnScreenDebug : Control
    {
        private const string LOG_CAT = "LogOnScreenDebug";

        private RichTextLabel DisplayLabel;

        private static Dictionary<string, OnScreenDebugInfo> DebugInfoList = new Dictionary<string, OnScreenDebugInfo>();

        private static bool AddedBasicInfo = false;

        public override void _Ready()
        {
            base._Ready();

            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Trace));

            this.SetAnchor(0, 0, 1, 1);
            this.SetMargin(10, 10, 0, 0);
            MouseFilter = MouseFilterEnum.Ignore;

            CreateControls();

            if (this.GetGameInstance().IsOnScreenDebugAddBasicInformation() && !AddedBasicInfo)
            {
                AddedBasicInfo = true;
                AddBasicInfo();
            }
        }

        public override void _Process(float delta)
        {
            UpdateLabel();
        }

        /// <summary>
        /// Adds some basic information on creation, can be toggled in config
        /// </summary>
        public void AddBasicInfo()
        {
            AddOnScreenDebugInfo("FPS", () => Engine.GetFramesPerSecond().ToString(CultureInfo.InvariantCulture), Colors.Red);
            AddOnScreenDebugInfo("Static Memory", () => MDStatics.HumanReadableSize(OS.GetStaticMemoryUsage()),
                Colors.Red);
            AddOnScreenDebugInfo("Network Active: ", () => MDStatics.IsNetworkActive().ToString(), Colors.Red);
            AddOnScreenDebugInfo("PeerId: ", () => MDStatics.GetPeerId().ToString(), Colors.Red);
        }

        private void UpdateLabel()
        {
            DisplayLabel.Clear();
            DisplayLabel.PushTable(2);
            AddText("Name", Colors.White);
            AddText("Value", Colors.White);

            foreach (string key in DebugInfoList.Keys)
            {
                string text = "";
                try
                {
                    text = DebugInfoList[key].InfoFunction.Invoke();
                    AddText(key, DebugInfoList[key].Color);
                    AddText(text, DebugInfoList[key].Color);
                }
                catch (Exception ex)
                {
                    // Something went wrong
                    MDLog.Debug(LOG_CAT, ex.ToString());
                }
            }

            DisplayLabel.Pop();
        }

        private void AddText(string Text, Color Color)
        {
            DisplayLabel.PushCell();
            DisplayLabel.PushColor(Color);
            DisplayLabel.AddText(Text);
            DisplayLabel.Pop();
            DisplayLabel.Pop();
        }

        /// <summary>Adds some info to print on the screen</summary>
        /// <param name="Name">The name to display, should be unique.</param>
        /// <param name="InfoFunction">Function that returns a string to display on the screen.</param>
        public static void AddOnScreenDebugInfo(string Name, OnScreenInfoFunction InfoFunction)
        {
            AddOnScreenDebugInfo(Name, InfoFunction, Colors.White);
        }

        /// <summary>Adds some info to print on the screen</summary>
        /// <param name="Name">The name to display, should be unique.</param>
        /// <param name="InfoFunction">Function that returns a string to display on the screen.</param>
        /// <param name="Color">Function that returns a string to display on the screen.</param>
        public static void AddOnScreenDebugInfo(string Name, OnScreenInfoFunction InfoFunction, Color Color)
        {
            if (DebugInfoList.ContainsKey(Name))
            {
                DebugInfoList[Name].InfoFunction = InfoFunction;
                DebugInfoList[Name].Color = Color;
            }
            else
            {
                DebugInfoList.Add(Name, new OnScreenDebugInfo(InfoFunction, Color));
            }
        }

        /// <summary>
        /// Removes the information from the on screen debug
        /// </summary>
        /// <param name="name">The name to remove</param>
        /// <returns>True if we removed it, false if not</returns>
        public static bool RemoveOnScreenDebugInfo(string name)
        {
            if (DebugInfoList.ContainsKey(name))
            {
                return DebugInfoList.Remove(name);;
            }

            return false;
        }

        /// <summary>
        /// Closes the onscreen debug
        /// </summary>
        public void Close()
        {
            this.RemoveAndFree();
        }

        // Creates the UI control for the debug screen
        private void CreateControls()
        {
            // RichTextLabel
            {
                DisplayLabel = new RichTextLabel();
                DisplayLabel.Name = nameof(DisplayLabel);
                DisplayLabel.Text = "";
                DisplayLabel.MouseFilter = MouseFilterEnum.Ignore;
                DisplayLabel.RectMinSize = GetViewport().GetVisibleRect().Size;
                AddChild(DisplayLabel);
            }
        }
    }
}