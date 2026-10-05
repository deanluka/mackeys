using System.Data;
using System.Runtime.InteropServices;

namespace MacKeysRemapGui;

public class MainForm : Form
{
    private ComboBox _keyboardSelector = null!;
    private ComboBox _fromKeySelector = null!;
    private ComboBox _toKeySelector = null!;
    private DataGridView _remappingGrid = null!;
    private Button _addButton = null!;
    private Button _removeButton = null!;
    private Button _saveButton = null!;
    private Button _startButton = null!;
    private Button _stopButton = null!;
    private Button _exitButton = null!;
    private Button _installDriverButton = null!;
    private Label _statusLabel = null!;
    private TextBox _logTextBox = null!;
    private Button _captureFromButton = null!;
    private Button _captureToButton = null!;

    private List<KeyRemapping> _config = new();
    private InterceptionRemapper? _remapper;
    private TrayIcon? _trayIcon;
    private bool _driverWarningShown = false;

    public MainForm()
    {
        InitializeComponent();
        LoadConfig();

        // Check if started from Task Scheduler
        bool isTaskScheduler = TaskSchedulerHelper.IsStartedFromTaskScheduler();
        bool isFirstRun = !File.Exists(ConfigManager.GetConfigPath());

        // Show window unless started from Task Scheduler
        bool showWindow = !isTaskScheduler;
        _trayIcon = new TrayIcon(this, showWindow, isTaskScheduler);

        // Ensure Task Scheduler task exists (replaces registry)
        TaskSchedulerHelper.EnsureTaskExists();

        // Load keyboards after window is shown
        Load += async (s, e) =>
        {
            Log($"App started (taskScheduler: {isTaskScheduler}, firstRun: {isFirstRun})");

            // Always load keyboards, even when started from task scheduler
            LoadKeyboards();

            // Auto-start mapping if config has rules
            if (_config.Count > 0)
            {
                await Task.Delay(500);
                BeginInvoke(() =>
                {
                    if (_startButton != null && !_startButton.IsDisposed && _startButton.Enabled)
                    {
                        Log("[AutoStart] Launching remapping engine automatically (config has rules)...");
                        _startButton.PerformClick();
                    }
                });
            }
        };
    }

    private void InitializeComponent()
    {
        Text = "MacKeysRemap v1.1 - Per-Device Key Remapper";
        Size = new Size(800, 650);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = new System.Drawing.Icon("icon.ico");

        // Keyboard selector
        var keyboardLabel = new Label
        {
            Text = "Keyboard:",
            Location = new Point(10, 15),
            AutoSize = true
        };
        Controls.Add(keyboardLabel);

        _keyboardSelector = new ComboBox
        {
            Location = new Point(80, 12),
            Size = new Size(330, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_keyboardSelector);

        var refreshButton = new Button
        {
            Text = "Refresh",
            Location = new Point(420, 11),
            Size = new Size(60, 23)
        };
        refreshButton.Click += (s, e) => LoadKeyboards();
        Controls.Add(refreshButton);

        // From key
        var fromLabel = new Label
        {
            Text = "From:",
            Location = new Point(10, 45),
            AutoSize = true
        };
        Controls.Add(fromLabel);

        _fromKeySelector = new ComboBox
        {
            Location = new Point(80, 42),
            Size = new Size(130, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_fromKeySelector);

        _captureFromButton = new Button
        {
            Text = "Press",
            Location = new Point(215, 41),
            Size = new Size(50, 23)
        };
        _captureFromButton.Click += CaptureFromButton_Click;
        Controls.Add(_captureFromButton);

        // To key
        var toLabel = new Label
        {
            Text = "To:",
            Location = new Point(280, 45),
            AutoSize = true
        };
        Controls.Add(toLabel);

        _toKeySelector = new ComboBox
        {
            Location = new Point(310, 42),
            Size = new Size(130, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_toKeySelector);

        _captureToButton = new Button
        {
            Text = "Press",
            Location = new Point(445, 41),
            Size = new Size(50, 23)
        };
        _captureToButton.Click += CaptureToButton_Click;
        Controls.Add(_captureToButton);

        // Add button
        _addButton = new Button
        {
            Text = "Add",
            Location = new Point(510, 41),
            Size = new Size(60, 23)
        };
        _addButton.Click += AddButton_Click;
        Controls.Add(_addButton);

        // Remapping grid
        _remappingGrid = new DataGridView
        {
            Location = new Point(10, 75),
            Size = new Size(765, 200),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _remappingGrid.Columns.Add("Keyboard", "Keyboard");
        _remappingGrid.Columns.Add("From", "From Key");
        _remappingGrid.Columns.Add("To", "To Key");
        Controls.Add(_remappingGrid);

        // Remove button
        _removeButton = new Button
        {
            Text = "Remove",
            Location = new Point(10, 285),
            Size = new Size(80, 25)
        };
        _removeButton.Click += RemoveButton_Click;
        Controls.Add(_removeButton);

        // Save button
        _saveButton = new Button
        {
            Text = "Save",
            Location = new Point(100, 285),
            Size = new Size(60, 25)
        };
        _saveButton.Click += SaveButton_Click;
        Controls.Add(_saveButton);

        // Status label
        _statusLabel = new Label
        {
            Text = "Status: Stopped",
            Location = new Point(170, 290),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
        };
        Controls.Add(_statusLabel);

        // Start button
        _startButton = new Button
        {
            Text = "Start",
            Location = new Point(470, 285),
            Size = new Size(70, 25),
            BackColor = Color.LightGreen
        };
        _startButton.Click += StartButton_Click;
        Controls.Add(_startButton);

        // Stop button
        _stopButton = new Button
        {
            Text = "Stop",
            Location = new Point(550, 285),
            Size = new Size(70, 25),
            BackColor = Color.LightCoral,
            Enabled = false
        };
        _stopButton.Click += StopButton_Click;
        Controls.Add(_stopButton);

        // Exit button
        _exitButton = new Button
        {
            Text = "Exit App",
            Location = new Point(630, 285),
            Size = new Size(75, 25),
            BackColor = Color.LightGray
        };
        _exitButton.Click += ExitButton_Click;
        Controls.Add(_exitButton);

        // Install driver button
        _installDriverButton = new Button
        {
            Text = "Install Driver",
            Location = new Point(10, 320),
            Size = new Size(100, 25),
            BackColor = Color.LightYellow,
            Visible = false
        };
        _installDriverButton.Click += InstallDriverButton_Click;
        Controls.Add(_installDriverButton);

        // Log textbox
        _logTextBox = new TextBox
        {
            Location = new Point(10, 320),
            Size = new Size(765, 280),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 10)
        };
        Controls.Add(_logTextBox);

        // Config path label
        var configPathLabel = new Label
        {
            Text = $"Config: {ConfigManager.GetConfigPath()}",
            Location = new Point(10, 560),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        Controls.Add(configPathLabel);

        // Populate key selectors
        foreach (var key in KeyNames.GetAllKeyNames())
        {
            _fromKeySelector.Items.Add(new KeySelectorItem(key, KeyNames.GetDisplayName(key)));
            _toKeySelector.Items.Add(new KeySelectorItem(key, KeyNames.GetDisplayName(key)));
        }

        SelectKeyInComboBox(_fromKeySelector, "LAlt");
        SelectKeyInComboBox(_toKeySelector, "LWin");
    }

    private static void SelectKeyInComboBox(ComboBox comboBox, string keyName)
    {
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is KeySelectorItem item && item.KeyName.Equals(keyName, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
        if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
    }

    private void LoadConfig()
    {
        _config = ConfigManager.Load();
        RefreshGrid();
    }

    private void LoadKeyboards()
    {
        _keyboardSelector.Items.Clear();

        IntPtr context = IntPtr.Zero;
        try
        {
            context = InterceptionNative.interception_create_context();
            if (context == IntPtr.Zero)
            {
                ShowDriverMissingPrompt();
                return;
            }

            _keyboardSelector.Items.Add(new KeyboardItem
            {
                DeviceId = 0,
                HardwareId = "All",
                DisplayName = "All Keyboards"
            });

            int detectedCount = 0;
            for (int i = 1; i <= InterceptionNative.INTERCEPTION_MAX_KEYBOARD; i++)
            {
                if (InterceptionNative.interception_is_keyboard(i) == 1)
                {
                    string hwId = InterceptionNative.GetHardwareId(context, i);
                    if (!string.IsNullOrWhiteSpace(hwId))
                    {
                        detectedCount++;
                        string friendly = InterceptionNative.GetFriendlyDeviceName(hwId, i);
                        _keyboardSelector.Items.Add(new KeyboardItem
                        {
                            DeviceId = i,
                            HardwareId = hwId,
                            DisplayName = friendly
                        });
                        Log($"Found keyboard {i}: {friendly} ({hwId})");
                    }
                }
            }

            if (detectedCount == 0)
            {
                Log("No active keyboard devices responded with hardware IDs.");
            }
            else
            {
                Log($"Total active keyboards detected: {detectedCount}");
            }
        }
        catch (DllNotFoundException)
        {
            ShowDriverMissingPrompt();
            return;
        }
        catch (Exception ex)
        {
            Log($"Error enumerating keyboards: {ex.Message}");
        }
        finally
        {
            if (context != IntPtr.Zero)
            {
                InterceptionNative.interception_destroy_context(context);
            }
        }

        // Add custom string match option
        _keyboardSelector.Items.Add(new KeyboardItem { DeviceId = -1, DisplayName = "Match custom string" });

        if (_keyboardSelector.Items.Count > 0)
        {
            _keyboardSelector.Enabled = true;
            _keyboardSelector.DropDownStyle = ComboBoxStyle.DropDown;
            _keyboardSelector.SelectedIndex = _keyboardSelector.Items.Count > 1 ? 1 : 0;
        }
    }

    private void ShowDriverMissingPrompt()
    {
        if (!_driverWarningShown)
        {
            _driverWarningShown = true;
            _installDriverButton.Visible = true;
            Log("Interception driver not found. Click 'Install Driver' to download and install.");
        }
        _keyboardSelector.Items.Add(new KeyboardItem { DeviceId = 0, DisplayName = "Interception driver not installed" });
        _keyboardSelector.Enabled = false;
    }

    private void RefreshGrid()
    {
        _remappingGrid.Rows.Clear();
        foreach (var remap in _config)
        {
            _remappingGrid.Rows.Add(
                remap.Keyboard,
                KeyNames.GetDisplayName(remap.From),
                KeyNames.GetDisplayName(remap.To)
            );
        }
    }

    private void CaptureFromButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new KeyCaptureDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dialog.CapturedKey))
        {
            SelectKeyInComboBox(_fromKeySelector, dialog.CapturedKey);
            Log($"Captured source key: {dialog.CapturedKey} ({KeyNames.GetDisplayName(dialog.CapturedKey)})");
        }
    }

    private void CaptureToButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new KeyCaptureDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dialog.CapturedKey))
        {
            SelectKeyInComboBox(_toKeySelector, dialog.CapturedKey);
            Log($"Captured target key: {dialog.CapturedKey} ({KeyNames.GetDisplayName(dialog.CapturedKey)})");
        }
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        if (_fromKeySelector.SelectedItem is not KeySelectorItem fromItem ||
            _toKeySelector.SelectedItem is not KeySelectorItem toItem)
        {
            return;
        }

        string keyboard = _keyboardSelector.SelectedItem is KeyboardItem kbItem ? kbItem.DisplayName : _keyboardSelector.Text;
        string from = fromItem.KeyName;
        string to = toItem.KeyName;

        if (string.IsNullOrWhiteSpace(keyboard))
        {
            MessageBox.Show("Please select a keyboard.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (from == to)
        {
            MessageBox.Show("From and To keys must be different.", "Invalid Remap", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.Add(new KeyRemapping { Keyboard = keyboard, From = from, To = to });
        RefreshGrid();
        Log($"Added mapping: [{keyboard}] {from} -> {to}");
    }

    private void RemoveButton_Click(object? sender, EventArgs e)
    {
        if (_remappingGrid.SelectedRows.Count == 0) return;

        int index = _remappingGrid.SelectedRows[0].Index;
        if (index >= 0 && index < _config.Count)
        {
            var removed = _config[index];
            _config.RemoveAt(index);
            RefreshGrid();
            Log($"Removed mapping: [{removed.Keyboard}] {removed.From} -> {removed.To}");
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        ConfigManager.Save(_config);
        Log("Config saved to file.");
        MessageBox.Show("Configuration saved successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void StartButton_Click(object? sender, EventArgs e)
    {
        ConfigManager.Save(_config);

        try
        {
            Log("Starting remapping engine...");
            Log($"Loaded {_config.Count} active remapping rule(s):");
            for (int i = 0; i < _config.Count; i++)
            {
                var r = _config[i];
                Log($"  #{i + 1}: [{r.Keyboard}] {r.From} -> {r.To}");
            }

            _remapper = new InterceptionRemapper(_config);
            _remapper.OnLog += msg =>
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    try { BeginInvoke(() => Log(msg)); } catch { }
                }
            };

            if (_remapper.Start())
            {
                _startButton.Enabled = false;
                _stopButton.Enabled = true;
                _statusLabel.Text = "Status: Running";
                _statusLabel.ForeColor = Color.Green;
                Log("Remapping is active.");
            }
            else
            {
                Log("Failed: interception_create_context returned null (driver not installed or administrator permission needed)");
                MessageBox.Show("Failed to start remapping. Please ensure Interception driver is installed and the app is run as Administrator.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            Log($"CRASH: {ex.GetType().Name}: {ex.Message}");
            Log($"Stack: {ex.StackTrace}");
            MessageBox.Show($"Crash on start:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopButton_Click(object? sender, EventArgs e)
    {
        _remapper?.Stop();
        _remapper = null;

        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        _statusLabel.Text = "Status: Stopped";
        _statusLabel.ForeColor = Color.Black;
        Log("Remapping stopped.");
    }

    private void ExitButton_Click(object? sender, EventArgs e)
    {
        _remapper?.Stop();
        _remapper = null;
        _trayIcon?.Dispose();
        Application.Exit();
    }

    private void InstallDriverButton_Click(object? sender, EventArgs e)
    {
        InstallDriver();
    }

    private async void InstallDriver()
    {
        try
        {
            string url = "https://github.com/oblitum/Interception/releases/latest/download/Interception.zip";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string zipPath = Path.Combine(baseDir, "Interception.zip");

            Log("Downloading Interception driver package...");
            using (var client = new System.Net.Http.HttpClient())
            {
                var bytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(zipPath, bytes);
            }
            Log("Download complete. Extracting...");

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, baseDir, true);
            Log("Extraction complete.");

            string installerPath = Directory.GetFiles(baseDir, "install-interception.exe", SearchOption.AllDirectories).FirstOrDefault() ?? "";
            if (!string.IsNullOrEmpty(installerPath))
            {
                var result = MessageBox.Show(
                    "Interception driver downloaded.\n\nInstall now? (Requires reboot after installation)",
                    "Install Driver",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    Log("Installing driver...");
                    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = "/install",
                        Verb = "runas",
                        UseShellExecute = true
                    });

                    process?.WaitForExit();
                    Log("Driver installer finished.");

                    try
                    {
                        string dllSource = Directory.GetFiles(baseDir, "interception.dll", SearchOption.AllDirectories)
                            .FirstOrDefault(p => p.Contains("x64", StringComparison.OrdinalIgnoreCase))
                            ?? Directory.GetFiles(baseDir, "interception.dll", SearchOption.AllDirectories).FirstOrDefault() ?? "";

                        if (!string.IsNullOrEmpty(dllSource))
                        {
                            string dllDest = Path.Combine(baseDir, "interception.dll");
                            File.Copy(dllSource, dllDest, true);
                            Log("Copied interception.dll to application directory.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to copy DLL: {ex.Message}");
                    }

                    MessageBox.Show(
                        "Driver installed successfully.\n\nPlease reboot your computer for changes to take effect.",
                        "Reboot Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Driver install failed: {ex.Message}");
            MessageBox.Show($"Failed to install driver: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private const int LogMaxLines = 3000;
    private const int LogKeepLines = 2500;

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        if (_logTextBox != null && !_logTextBox.IsDisposed)
        {
            _logTextBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");

            string? txt = null;
            int newlines = -1;
            try
            {
                txt = _logTextBox.Text;
                newlines = 0;
                for (int i = txt.Length - 1; i >= 0 && newlines <= LogMaxLines; i--)
                    if (txt[i] == '\n') newlines++;
            }
            catch { newlines = -1; }

            if (newlines > LogMaxLines)
            {
                int trimStart = 0;
                int passed = 0;
                int linesToCut = newlines - LogKeepLines;
                for (int i = 0; i < txt!.Length && passed < linesToCut; i++)
                {
                    if (txt[i] == '\n') { passed++; trimStart = i + 1; }
                }
                if (trimStart > 0 && trimStart < txt.Length)
                {
                    _logTextBox.Select(0, trimStart);
                    _logTextBox.SelectedText = "";
                    _logTextBox.Select(_logTextBox.TextLength, 0);
                    _logTextBox.ScrollToCaret();
                }
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _remapper?.Stop();
        _trayIcon?.Dispose();
        base.OnFormClosing(e);
    }
}

public class KeyCaptureDialog : Form
{
    public string CapturedKey { get; private set; } = "";
    private IntPtr _context = IntPtr.Zero;
    private readonly CancellationTokenSource _cts = new();
    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _hookProc;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    public KeyCaptureDialog()
    {
        Text = "Press a key...";
        Size = new Size(320, 160);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var label = new Label
        {
            Text = "Press any key to capture...",
            Location = new Point(30, 25),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 11, FontStyle.Regular)
        };
        Controls.Add(label);

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(110, 75),
            Size = new Size(85, 28),
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(cancelButton);

        _hookProc = HookCallback;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);

        Task.Run(() => CaptureLoop(_cts.Token));
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);
            string keyName = KeyNames.GetKeyNameFromVK((byte)vkCode);
            if (!string.IsNullOrEmpty(keyName))
            {
                BeginInvoke(() =>
                {
                    if (string.IsNullOrEmpty(CapturedKey))
                    {
                        CapturedKey = keyName;
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                });
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void CaptureLoop(CancellationToken ct)
    {
        try
        {
            _context = InterceptionNative.interception_create_context();
            if (_context == IntPtr.Zero) return;

            InterceptionNative.interception_set_filter(
                _context,
                InterceptionNative.IsKeyboardPredicate,
                InterceptionNative.INTERCEPTION_FILTER_KEY_ALL);

            var stroke = new InterceptionNative.KeyStroke();
            while (!ct.IsCancellationRequested && _context != IntPtr.Zero)
            {
                int device = InterceptionNative.interception_wait_with_timeout(_context, 50);
                if (device <= 0) continue;

                if (InterceptionNative.interception_receive(_context, device, ref stroke, 1) > 0)
                {
                    bool isKeyUp = (stroke.State & InterceptionNative.INTERCEPTION_KEY_UP) != 0;
                    bool isE0 = (stroke.State & InterceptionNative.INTERCEPTION_KEY_E0) != 0;

                    InterceptionNative.interception_send(_context, device, ref stroke, 1);

                    if (!isKeyUp)
                    {
                        string keyName = KeyNames.GetKeyName(stroke.Code, isE0);
                        BeginInvoke(() =>
                        {
                            if (string.IsNullOrEmpty(CapturedKey))
                            {
                                CapturedKey = keyName;
                                DialogResult = DialogResult.OK;
                                Close();
                            }
                        });
                        return;
                    }
                }
            }
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts.Cancel();
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        if (_context != IntPtr.Zero)
        {
            InterceptionNative.interception_destroy_context(_context);
            _context = IntPtr.Zero;
        }
        base.OnFormClosing(e);
    }
}

public class KeyboardItem
{
    public int DeviceId { get; set; }
    public string HardwareId { get; set; } = "";
    public string DisplayName { get; set; } = "";

    public override string ToString() => DisplayName;
}

public class KeySelectorItem
{
    public string KeyName { get; set; }
    public string DisplayName { get; set; }

    public KeySelectorItem(string keyName, string displayName)
    {
        KeyName = keyName;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}
