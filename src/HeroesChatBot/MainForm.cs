using HeroesChatBot.Models;
using HeroesChatBot.Services;

namespace HeroesChatBot;

public sealed class MainForm : Form
{
    private readonly SettingsStore _settingsStore = new();
    private readonly HeroesWindow _heroesWindow = new();
    private readonly System.Windows.Forms.Timer _schedulerTimer = new() { Interval = 1000 };

    private readonly ListBox _recipientsList = new();
    private readonly TextBox _userNameTextBox = new();
    private readonly NumericUpDown _intervalValue = new();
    private readonly ComboBox _intervalUnit = new();
    private readonly TextBox _messagesTextBox = new();
    private readonly Label _gameStatusLabel = new();
    private readonly Label _automationStatusLabel = new();
    private readonly Label _nextSendLabel = new();
    private readonly CheckBox _roomConfirmation = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Button _sendNowButton = new();
    private readonly TextBox _logTextBox = new();

    private AppSettings _settings = new();
    private RecipientProfile? _armedRecipient;
    private DateTimeOffset? _nextSendAt;
    private CancellationTokenSource? _automationCancellation;
    private bool _loadingEditor;
    private bool _sending;

    public MainForm()
    {
        Text = "Heroes Lobby Messenger";
        MinimumSize = new Size(920, 650);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        BuildInterface();
        LoadSettings();

        _schedulerTimer.Tick += SchedulerTimer_Tick;
        _schedulerTimer.Start();
        UpdateGameStatus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopAutomation("Application closing.");
        SaveEditorToSelectedRecipient(showErrors: false);
        TrySaveSettings();
        base.OnFormClosing(e);
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        Controls.Add(root);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 245,
            FixedPanel = FixedPanel.Panel1
        };
        root.Controls.Add(split, 0, 0);

        BuildRecipientList(split.Panel1);
        BuildRecipientEditor(split.Panel2);

        var separator = new Label
        {
            Dock = DockStyle.Top,
            BorderStyle = BorderStyle.Fixed3D,
            Height = 2,
            Margin = new Padding(0, 10, 0, 10)
        };
        root.Controls.Add(separator, 0, 1);

        BuildAutomationPanel(root);
    }

    private void BuildRecipientList(Control parent)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0, 0, 10, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        parent.Controls.Add(panel);

        panel.Controls.Add(new Label
        {
            Text = "Recipients",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        });

        _recipientsList.Dock = DockStyle.Fill;
        _recipientsList.IntegralHeight = false;
        _recipientsList.SelectedIndexChanged += RecipientsList_SelectedIndexChanged;
        panel.Controls.Add(_recipientsList);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 8, 0, 0)
        };
        var addButton = new Button { Text = "Add", AutoSize = true };
        var removeButton = new Button { Text = "Remove", AutoSize = true };
        addButton.Click += (_, _) => AddRecipient();
        removeButton.Click += (_, _) => RemoveSelectedRecipient();
        buttons.Controls.Add(addButton);
        buttons.Controls.Add(removeButton);
        panel.Controls.Add(buttons);
    }

    private void BuildRecipientEditor(Control parent)
    {
        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(10, 0, 0, 0)
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        parent.Controls.Add(editor);

        var heading = new Label
        {
            Text = "Recipient settings",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12)
        };
        editor.Controls.Add(heading, 0, 0);
        editor.SetColumnSpan(heading, 2);

        editor.Controls.Add(CreateFieldLabel("Lobby username"), 0, 1);
        _userNameTextBox.Dock = DockStyle.Fill;
        editor.Controls.Add(_userNameTextBox, 1, 1);

        editor.Controls.Add(CreateFieldLabel("Message interval"), 0, 2);
        var intervalPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = Padding.Empty
        };
        _intervalValue.Minimum = 1;
        _intervalValue.Maximum = 10080;
        _intervalValue.Value = 60;
        _intervalValue.Width = 100;
        _intervalUnit.DropDownStyle = ComboBoxStyle.DropDownList;
        _intervalUnit.Items.AddRange(["minutes", "hours"]);
        _intervalUnit.SelectedIndex = 0;
        intervalPanel.Controls.Add(_intervalValue);
        intervalPanel.Controls.Add(_intervalUnit);
        editor.Controls.Add(intervalPanel, 1, 2);

        var messageLabel = CreateFieldLabel("Messages");
        messageLabel.Margin = new Padding(0, 9, 12, 0);
        editor.Controls.Add(messageLabel, 0, 3);
        _messagesTextBox.Dock = DockStyle.Fill;
        _messagesTextBox.Multiline = true;
        _messagesTextBox.ScrollBars = ScrollBars.Vertical;
        _messagesTextBox.AcceptsReturn = true;
        _messagesTextBox.PlaceholderText = "One message per line. Messages rotate in order.";
        editor.Controls.Add(_messagesTextBox, 1, 3);

        var saveButton = new Button
        {
            Text = "Save recipient",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 8, 0, 0)
        };
        saveButton.Click += (_, _) =>
        {
            if (SaveEditorToSelectedRecipient(showErrors: true))
            {
                TrySaveSettings();
                Log("Recipient settings saved.");
            }
        };
        editor.Controls.Add(saveButton, 1, 4);
    }

    private void BuildAutomationPanel(TableLayoutPanel root)
    {
        var automation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5
        };
        automation.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        automation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        automation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        automation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        automation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        automation.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        automation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(automation, 0, 2);

        var heading = new Label
        {
            Text = "Current private room automation (MVP)",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };
        automation.Controls.Add(heading, 0, 0);
        automation.SetColumnSpan(heading, 2);

        automation.Controls.Add(CreateFieldLabel("Heroes III"), 0, 1);
        _gameStatusLabel.AutoSize = true;
        _gameStatusLabel.Margin = new Padding(0, 5, 0, 4);
        automation.Controls.Add(_gameStatusLabel, 1, 1);

        _roomConfirmation.Text =
            "I confirm the selected recipient's private chat is currently open in the lobby.";
        _roomConfirmation.AutoSize = true;
        _roomConfirmation.Margin = new Padding(0, 6, 0, 6);
        automation.Controls.Add(_roomConfirmation, 1, 2);

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = Padding.Empty
        };
        _startButton.Text = "Arm selected recipient";
        _startButton.AutoSize = true;
        _startButton.Click += (_, _) => StartAutomation();
        _stopButton.Text = "Stop";
        _stopButton.AutoSize = true;
        _stopButton.Enabled = false;
        _stopButton.Click += (_, _) => StopAutomation("Stopped by user.");
        _sendNowButton.Text = "Send next message now";
        _sendNowButton.AutoSize = true;
        _sendNowButton.Click += async (_, _) => await SendNowAsync();
        controls.Controls.Add(_startButton);
        controls.Controls.Add(_stopButton);
        controls.Controls.Add(_sendNowButton);
        automation.Controls.Add(controls, 1, 3);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0, 8, 0, 0)
        };
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _automationStatusLabel.Text = "Not armed.";
        _automationStatusLabel.AutoSize = true;
        _nextSendLabel.AutoSize = true;
        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Multiline = true;
        _logTextBox.ReadOnly = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        statusPanel.Controls.Add(_automationStatusLabel);
        statusPanel.Controls.Add(_nextSendLabel);
        statusPanel.Controls.Add(_logTextBox);
        automation.Controls.Add(statusPanel, 0, 4);
        automation.SetColumnSpan(statusPanel, 2);
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0, 5, 12, 6)
    };

    private void LoadSettings()
    {
        try
        {
            _settings = _settingsStore.Load();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Settings error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _settings = new AppSettings();
        }

        foreach (var recipient in _settings.Recipients)
        {
            _recipientsList.Items.Add(recipient);
        }

        if (_recipientsList.Items.Count > 0)
        {
            _recipientsList.SelectedIndex = 0;
        }
        else
        {
            AddRecipient();
        }
    }

    private void AddRecipient()
    {
        SaveEditorToSelectedRecipient(showErrors: false);
        var recipient = new RecipientProfile();
        _settings.Recipients.Add(recipient);
        _recipientsList.Items.Add(recipient);
        _recipientsList.SelectedItem = recipient;
        _userNameTextBox.Focus();
        TrySaveSettings();
    }

    private void RemoveSelectedRecipient()
    {
        if (_recipientsList.SelectedItem is not RecipientProfile recipient)
        {
            return;
        }

        if (_armedRecipient?.Id == recipient.Id)
        {
            StopAutomation("The armed recipient was removed.");
        }

        _settings.Recipients.Remove(recipient);
        _recipientsList.Items.Remove(recipient);
        TrySaveSettings();

        if (_recipientsList.Items.Count == 0)
        {
            AddRecipient();
        }
    }

    private void RecipientsList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingEditor)
        {
            return;
        }

        LoadSelectedRecipientIntoEditor();
    }

    private void LoadSelectedRecipientIntoEditor()
    {
        if (_recipientsList.SelectedItem is not RecipientProfile recipient)
        {
            return;
        }

        _loadingEditor = true;
        try
        {
            _userNameTextBox.Text = recipient.UserName;
            var totalMinutes = Math.Max(1, recipient.IntervalSeconds / 60);
            if (totalMinutes >= 60 && totalMinutes % 60 == 0)
            {
                _intervalValue.Value = Math.Clamp(totalMinutes / 60, 1, 10080);
                _intervalUnit.SelectedItem = "hours";
            }
            else
            {
                _intervalValue.Value = Math.Clamp(totalMinutes, 1, 10080);
                _intervalUnit.SelectedItem = "minutes";
            }

            _messagesTextBox.Lines = recipient.Messages.ToArray();
            _roomConfirmation.Checked = false;
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private bool SaveEditorToSelectedRecipient(bool showErrors)
    {
        if (_recipientsList.SelectedItem is not RecipientProfile recipient)
        {
            return false;
        }

        var userName = _userNameTextBox.Text.Trim();
        var messages = _messagesTextBox.Lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (showErrors && userName.Length == 0)
        {
            ShowValidationError("Enter the recipient's exact lobby username.", _userNameTextBox);
            return false;
        }

        if (showErrors && messages.Count == 0)
        {
            ShowValidationError("Enter at least one message.", _messagesTextBox);
            return false;
        }

        if (showErrors &&
            _settings.Recipients.Any(other =>
                other.Id != recipient.Id &&
                other.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
        {
            ShowValidationError("A recipient with this lobby username already exists.", _userNameTextBox);
            return false;
        }

        var multiplier = Equals(_intervalUnit.SelectedItem, "hours") ? 3600 : 60;
        recipient.UserName = userName;
        recipient.IntervalSeconds = checked((int)_intervalValue.Value * multiplier);
        recipient.Messages = messages;
        if (recipient.Messages.Count > 0)
        {
            recipient.NextMessageIndex %= recipient.Messages.Count;
        }
        else
        {
            recipient.NextMessageIndex = 0;
        }

        var selectedIndex = _recipientsList.SelectedIndex;
        _loadingEditor = true;
        try
        {
            _recipientsList.Items[selectedIndex] = recipient;
            _recipientsList.SelectedIndex = selectedIndex;
        }
        finally
        {
            _loadingEditor = false;
        }

        return true;
    }

    private void StartAutomation()
    {
        if (!SaveEditorToSelectedRecipient(showErrors: true) ||
            _recipientsList.SelectedItem is not RecipientProfile recipient)
        {
            return;
        }

        if (!_roomConfirmation.Checked)
        {
            MessageBox.Show(
                this,
                "Open this user's private room in Heroes III, then confirm the checkbox.",
                "Private room not confirmed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (_heroesWindow.FindLobbyWindow() == IntPtr.Zero)
        {
            MessageBox.Show(
                this,
                "Start Heroes III and enter the online lobby first.",
                "Heroes III not found",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        StopAutomation(null);
        _automationCancellation = new CancellationTokenSource();
        _armedRecipient = recipient;
        _nextSendAt = DateTimeOffset.Now.AddSeconds(recipient.IntervalSeconds);
        _startButton.Enabled = false;
        _stopButton.Enabled = true;
        _automationStatusLabel.Text = $"Armed for {recipient.UserName}. Keep that private room open.";
        Log($"Armed {recipient.UserName}; first scheduled message is in {FormatInterval(recipient.IntervalSeconds)}.");
        TrySaveSettings();
        UpdateNextSendLabel();
    }

    private void StopAutomation(string? reason)
    {
        _automationCancellation?.Cancel();
        _automationCancellation?.Dispose();
        _automationCancellation = null;
        _armedRecipient = null;
        _nextSendAt = null;
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        _automationStatusLabel.Text = "Not armed.";
        _nextSendLabel.Text = string.Empty;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Log(reason);
        }
    }

    private async void SchedulerTimer_Tick(object? sender, EventArgs e)
    {
        UpdateGameStatus();
        UpdateNextSendLabel();

        if (_armedRecipient is null ||
            _nextSendAt is null ||
            _sending ||
            DateTimeOffset.Now < _nextSendAt)
        {
            return;
        }

        await SendForRecipientAsync(_armedRecipient);
    }

    private async Task SendNowAsync()
    {
        if (!SaveEditorToSelectedRecipient(showErrors: true) ||
            _recipientsList.SelectedItem is not RecipientProfile recipient)
        {
            return;
        }

        if (!_roomConfirmation.Checked)
        {
            MessageBox.Show(
                this,
                "Confirm that this recipient's private chat is currently open.",
                "Private room not confirmed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        await SendForRecipientAsync(recipient);
    }

    private async Task SendForRecipientAsync(RecipientProfile recipient)
    {
        if (_sending)
        {
            return;
        }

        _sending = true;
        SetAutomationControlsEnabled(false);
        try
        {
            var message = recipient.GetNextMessage();
            var cancellationToken = _automationCancellation?.Token ?? CancellationToken.None;
            await _heroesWindow.SendToCurrentPrivateRoomAsync(message, cancellationToken);

            recipient.LastSentAt = DateTimeOffset.Now;
            if (_armedRecipient?.Id == recipient.Id)
            {
                _nextSendAt = DateTimeOffset.Now.AddSeconds(recipient.IntervalSeconds);
            }

            TrySaveSettings();
            var sentMessageNumber = recipient.NextMessageIndex == 0
                ? recipient.Messages.Count
                : recipient.NextMessageIndex;
            Log($"Sent message {sentMessageNumber} of {recipient.Messages.Count} to the current room for {recipient.UserName}.");
        }
        catch (OperationCanceledException)
        {
            Log("Message send cancelled.");
        }
        catch (InvalidOperationException ex)
        {
            Log($"Send failed: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Could not send message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            StopAutomation("Automation stopped after a send failure.");
        }
        finally
        {
            _sending = false;
            SetAutomationControlsEnabled(true);
        }
    }

    private void SetAutomationControlsEnabled(bool enabled)
    {
        _sendNowButton.Enabled = enabled;
        _stopButton.Enabled = enabled && _armedRecipient is not null;
        _startButton.Enabled = enabled && _armedRecipient is null;
    }

    private void UpdateGameStatus()
    {
        var found = _heroesWindow.FindLobbyWindow() != IntPtr.Zero;
        _gameStatusLabel.Text = found ? "Detected" : "Not detected";
        _gameStatusLabel.ForeColor = found ? Color.DarkGreen : Color.Firebrick;
    }

    private void UpdateNextSendLabel()
    {
        if (_nextSendAt is null)
        {
            _nextSendLabel.Text = string.Empty;
            return;
        }

        var remaining = _nextSendAt.Value - DateTimeOffset.Now;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        _nextSendLabel.Text =
            $"Next send: {_nextSendAt.Value:HH:mm:ss} ({remaining:hh\\:mm\\:ss} remaining)";
    }

    private void TrySaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (IOException ex)
        {
            Log($"Could not save settings: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Log($"Could not save settings: {ex.Message}");
        }
    }

    private void ShowValidationError(string message, Control control)
    {
        MessageBox.Show(this, message, "Check recipient settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        control.Focus();
    }

    private void Log(string message)
    {
        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static string FormatInterval(int totalSeconds)
    {
        var interval = TimeSpan.FromSeconds(totalSeconds);
        return interval.TotalHours >= 1
            ? $"{interval.TotalHours:0.##} hour(s)"
            : $"{interval.TotalMinutes:0.##} minute(s)";
    }
}
