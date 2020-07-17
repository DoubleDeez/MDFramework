using Godot;
using System.Collections.Generic;

namespace MD
{
    /// <summary>
    /// Class that allows the user to enter console commands that have been registered with MDCommand
    /// </summary>
    public class MDConsole : Control
    {
        private const int HISTORY_DISPLAY_COUNT = 10;

        private bool IsDisplayingHistory = false;
        private bool IsDisplayingHelp = false;
        private int CmdHistoryIndex = -1;
        private int CmdHelpIndex = -1;
        private string StoredCommand = "";

        private List<string> CommandHistory;
        private List<string> FilteredCommands;
        private List<string> CommandList;
        private LineEdit ConsoleInput;
        private VBoxContainer ConsoleBox;
        private VBoxContainer HistoryHelpBox;
        private PanelContainer HistoryHelpContainer;

        public override void _Ready()
        {
            base._Ready();

            this.SetAnchor(0, 0, 1, 1);
            this.SetMargin(0, 0, 0, 0);

            CommandHistory = MDCommands.GetCommandHistory();
            CommandList = MDCommands.GetCommandList();

            CreateConsoleControls();

            SetProcessInput(true);
        }

        public override void _Input(InputEvent InEvent)
        {
            if (!(InEvent is InputEventKey EventKey)
                || !EventKey.Pressed || EventKey.Echo)
            {
                return;
            }

            if (EventKey.Scancode == (int) KeyList.Up)
            {
                if (IsDisplayingHelp && ConsoleInput.Text.Empty() == false)
                {
                    NavigateHelp(1);
                }
                else
                {
                    NavigateHistory(1);
                }

                this.SetInputHandled();
            }
            else if (EventKey.Scancode == (int) KeyList.Down)
            {
                if (IsDisplayingHelp && ConsoleInput.Text.Empty() == false)
                {
                    NavigateHelp(-1);
                }
                else
                {
                    NavigateHistory(-1);
                }

                this.SetInputHandled();
            }
            else if (EventKey.Scancode == (int) KeyList.Tab)
            {
                if (IsDisplayingHelp)
                {
                    HandleTabPressed();
                    this.SetInputHandled();
                }
            }
        }

        /// <summary>
        /// Closes and frees the console prompt
        /// </summary>
        public void Close()
        {
            this.RemoveAndFree();
        }

        // Navigates up/down the command history
        private void NavigateHistory(int Direction)
        {
            IsDisplayingHelp = false;

            int HistoryCount = CommandHistory.Count;
            if (HistoryCount == 0)
            {
                return;
            }

            if (CmdHistoryIndex == -1)
            {
                StoredCommand = ConsoleInput.Text;
            }

            CmdHistoryIndex = Mathf.Clamp(CmdHistoryIndex + Direction, -1, HistoryCount - 1);

            if (CmdHistoryIndex == -1)
            {
                SetConsoleText(StoredCommand);
                HistoryHelpContainer.Visible = false;
                IsDisplayingHistory = false;
                OnCommandChanged(StoredCommand);
            }
            else
            {
                SetConsoleText(CommandHistory[CmdHistoryIndex]);
                HistoryHelpContainer.Visible = true;
                IsDisplayingHistory = true;
                int HistoryCountToShow = Mathf.Min(HISTORY_DISPLAY_COUNT, HistoryCount - CmdHistoryIndex - 1);
                List<string> HistoryList = CommandHistory.GetRange(CmdHistoryIndex + 1, HistoryCountToShow);
                PopulateHistoryHelp(HistoryList, false);
            }
        }

        // Navigates up/down the command help
        private void NavigateHelp(int Direction)
        {
            int HelpCount = FilteredCommands.Count;
            if (HelpCount == 0)
            {
                return;
            }

            if (CmdHelpIndex == -1)
            {
                StoredCommand = ConsoleInput.Text;
            }

            CmdHelpIndex = Mathf.Clamp(CmdHelpIndex + Direction, -1, HelpCount - 1);

            if (CmdHelpIndex == -1)
            {
                SetConsoleText(StoredCommand);
            }
            else
            {
                SetConsoleText(FilteredCommands[CmdHelpIndex]);
            }
        }

        // Autofills the first suggested command
        private void HandleTabPressed()
        {
            if (FilteredCommands.Count > 0)
            {
                SetConsoleText(FilteredCommands[0]);
                OnCommandChanged(ConsoleInput.Text);
            }
        }

        // Creates the UI control that accepts text input
        private void CreateConsoleControls()
        {
            // ConsoleBox
            {
                ConsoleBox = new VBoxContainer();
                ConsoleBox.Name = nameof(ConsoleBox);
                ConsoleBox.SetAnchor(0, 0, 1, 1);
                ConsoleBox.Alignment = BoxContainer.AlignMode.End;
                AddChild(ConsoleBox);
            }

            // HistoryHelpContainer
            {
                HistoryHelpContainer = new PanelContainer();
                HistoryHelpContainer.Name = nameof(HistoryHelpContainer);
                ConsoleBox.AddChild(HistoryHelpContainer);
                HistoryHelpContainer.Visible = false;
            }

            // HistoryHelpBox
            {
                HistoryHelpBox = new VBoxContainer();
                HistoryHelpBox.Name = nameof(HistoryHelpBox);
                HistoryHelpContainer.AddChild(HistoryHelpBox);
            }

            // ConsoleInput
            {
                ConsoleInput = new LineEdit();
                ConsoleInput.Name = nameof(ConsoleInput);
                ConsoleBox.AddChild(ConsoleInput);

                ConsoleInput.ContextMenuEnabled = false;
                ConsoleInput.Connect("text_entered", this, nameof(OnCommandEntered));
                ConsoleInput.Connect("text_changed", this, nameof(OnCommandChanged));
                ConsoleInput.GrabFocus();
            }
        }

        // Called when the user hits enter on the ConsoleInput
        private void OnCommandEntered(string Command)
        {
            if (Command.ToLower().StartsWith("servercommand"))
            {
                this.GetGameSession().ServerCommand(Command.Substring(Command.IndexOf(' ') + 1));
            }
            else
            {
                MDCommands.InvokeCommand(Command);
            }

            Close();
        }

        // Called when the text in the console box is changed
        private void OnCommandChanged(string Command)
        {
            if (IsDisplayingHistory)
            {
                IsDisplayingHistory = false;
            }

            string CommandName = Command.Empty() ? Command : Command.Split(" ")[0].ToLower();
            if (CommandName.Empty())
            {
                IsDisplayingHelp = false;
                CmdHelpIndex = -1;
                HistoryHelpContainer.Visible = false;
                return;
            }

            FilteredCommands = CommandList.FindAll(s => s.ToLower().BeginsWith(CommandName));
            if (FilteredCommands.Count == 0)
            {
                IsDisplayingHelp = false;
                CmdHelpIndex = -1;
                HistoryHelpContainer.Visible = false;
                return;
            }

            if (IsDisplayingHelp == false)
            {
                CmdHelpIndex = -1;
                IsDisplayingHelp = true;
            }

            HistoryHelpContainer.Visible = true;
            PopulateHistoryHelp(FilteredCommands, true);
        }

        private void PopulateHistoryHelp(List<string> StringList, bool UseHelpText)
        {
            int HelpLabelsCount = HistoryHelpBox.GetChildCount();
            int HelpCmdCount = StringList.Count;
            int i = 0;
            // Update existing controls
            for (; i < Mathf.Min(HelpLabelsCount, HelpCmdCount); ++i)
            {
                int StringIndex = HelpCmdCount - i - 1;
                Label HelpLabel = HistoryHelpBox.GetChild<Label>(i);
                HelpLabel.Text =
                    UseHelpText ? MDCommands.GetHelpText(StringList[StringIndex]) : StringList[StringIndex];
                HelpLabel.Visible = true;
            }

            // Create new ones as needed
            for (; i < HelpCmdCount; ++i)
            {
                int StringIndex = HelpCmdCount - i - 1;
                Label HelpLabel = new Label
                {
                    Text = UseHelpText ? MDCommands.GetHelpText(StringList[StringIndex]) : StringList[StringIndex]
                };
                HistoryHelpBox.AddChild(HelpLabel);
            }

            // Hide the extras
            for (; i < HelpLabelsCount; ++i)
            {
                Label HelpLabel = HistoryHelpBox.GetChild<Label>(i);
                HelpLabel.Visible = false;
            }
        }

        // Sets the text and moves the caret to the end
        private void SetConsoleText(string Command)
        {
            ConsoleInput.Text = Command;
            ConsoleInput.CaretPosition = ConsoleInput.Text.Length;
        }
    }
}