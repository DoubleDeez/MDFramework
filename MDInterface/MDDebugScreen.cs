using Godot;
using System;
using System.Collections.Generic;

namespace MD
{
    /// <summary>
    /// On screen debugger
    /// </summary>
    [MDAutoRegister]
    public class MDDebugScreen : MDScreen
    {
        private const string LOG_CAT = "LogDebugScreen";

        private RichTextLabel DisplayLabel;


        public override void _Ready()
        {
            base._Ready();

            MDLog.AddLogCategoryProperties(LOG_CAT, new MDLogProperties(MDLogLevel.Info));

            MouseFilter = MouseFilterEnum.Ignore;

            CreateControls();
        }

        public override void _Process(float delta)
        {
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            DisplayLabel.Clear();
            DisplayLabel.PushTable(2);

            foreach (string Category in MDOnScreenDebug.DebugInfoCategoryMap.Keys)
            {
                if (MDOnScreenDebug.HiddenCategories.Contains(Category))
                {
                    continue;
                }

                AddCateogryTitle(Category);

                Dictionary<string, OnScreenDebugInfo> DebugInfoList = MDOnScreenDebug.DebugInfoCategoryMap[Category];

                foreach (string key in DebugInfoList.Keys)
                {
                    try
                    {
                        string text = DebugInfoList[key].InfoFunction.Invoke();
                        AddTextCell(key, DebugInfoList[key].Color);
                        AddTextCell(text, DebugInfoList[key].Color);
                    }
                    catch (Exception ex)
                    {
                        // Something went wrong
                        MDLog.Debug(LOG_CAT, ex.ToString());
                    }
                }

                AddEmptyLine();
            }

            DisplayLabel.Pop();
        }

        private void AddTextCell(string Text, Color Color)
        {
            DisplayLabel.PushCell();
            DisplayLabel.PushColor(Color);
            DisplayLabel.AddText(Text);
            DisplayLabel.Pop();
            DisplayLabel.Pop();
        }

        private void AddEmptyLine()
        {
            DisplayLabel.PushCell();
            DisplayLabel.Pop();
            DisplayLabel.PushCell();
            DisplayLabel.Pop();
        }

        private void AddCateogryTitle(string Category)
        {
            DisplayLabel.PushCell();
            DisplayLabel.PushColor(Colors.White);
            DisplayLabel.PushBold();
            DisplayLabel.PushUnderline();
            DisplayLabel.AddText(Category);
            DisplayLabel.Pop();
            DisplayLabel.Pop();
            DisplayLabel.Pop();
            DisplayLabel.Pop();

            DisplayLabel.PushCell();
            DisplayLabel.Pop();
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