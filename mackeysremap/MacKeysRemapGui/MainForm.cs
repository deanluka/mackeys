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
    private Label _statusLabel = null!;
    private TextBox _logTextBox = null!;
    private System.Windows.Forms.Timer _refreshTimer = null!;
    private Button _captureFromButton = null!;
    private Button _captureToButton = null!;
    private bool _capturingFrom = false;
    private bool _capturingTo = false;
    private IntPtr _captureContext = IntPtr.Zero;

    private List<KeyRemapping> _config = new();
    private InterceptionRemapper? _remapper;
    private TrayIcon? _trayIcon;
    private bool _driverWarningShown = false;

    // Interception P/Invoke
    private const string InterceptionDll = "interception.dll";
    private const int INTERCEPTION_KEYBOARD = 1;
    private const int INTERCEPTION_MAX_KEYBOARD = 10;
    private const ushort INTERCEPTION_FILTER_KEYBOARD_ALL = 0xFFFF;

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr interception_create_context();

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void interception_destroy_context(IntPtr context);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_send(IntPtr context, int device, ref KeyStroke stroke, int n);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_receive(IntPtr context, int device, ref KeyStroke stroke, int n);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void interception_set_filter(IntPtr context, int predicate, ushort filter);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_is_keyboard(int device);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int interception_get_hardware_id(int device, IntPtr buffer, int size);

    [DllImport(InterceptionDll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr interception_wait(IntPtr context);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyStroke
    {
        public ushort Code;
        public ushort State;
        public uint Information;
    }

    public MainForm()
    {
        InitializeComponent();
        LoadConfig();
        LoadKeyboards();

        bool isFirstRun = !File.Exists(ConfigManager.GetConfigPath());
        _trayIcon = new TrayIcon(this, isFirstRun);

        if (isFirstRun)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run");
                key.SetValue("MacKeysRemap", Application.ExecutablePath);
            }
            catch { }
        }

        Log("App started");
    }

    private void InitializeComponent()
    {
        Text = "MacKeysRemap - Per-Device Key Remapper";
        Size = new Size(800, 650);
        StartPosition = FormStartPosition.CenterScreen;

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
            Size = new Size(250, 25),
            DropDownStyle = ComboBoxStyle.DropDown
        };
        Controls.Add(_keyboardSelector);

        var refreshButton = new Button
        {
            Text = "Refresh",
            Location = new Point(340, 11),
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
            Size = new Size(120, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_fromKeySelector);

        _captureFromButton = new Button
        {
            Text = "Press",
            Location = new Point(205, 41),
            Size = new Size(50, 23)
        };
        _captureFromButton.Click += CaptureFromButton_Click;
        Controls.Add(_captureFromButton);

        // To key
        var toLabel = new Label
        {
            Text = "To:",
            Location = new Point(270, 45),
            AutoSize = true
        };
        Controls.Add(toLabel);

        _toKeySelector = new ComboBox
        {
            Location = new Point(300, 42),
            Size = new Size(120, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_toKeySelector);

        _captureToButton = new Button
        {
            Text = "Press",
            Location = new Point(425, 41),
            Size = new Size(50, 23)
        };
        _captureToButton.Click += CaptureToButton_Click;
        Controls.Add(_captureToButton);

        // Add button
        _addButton = new Button
        {
            Text = "Add",
            Location = new Point(490, 41),
            Size = new Size(50, 23)
        };
        _addButton.Click += AddButton_Click;
        Controls.Add(_addButton);

        // Remapping grid
        _remappingGrid = new DataGridView
        {
            Location = new Point(10, 75),
            Size = new Size(770, 200),
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

        // Start button
        _startButton = new Button
        {
            Text = "Start",
            Location = new Point(550, 285),
            Size = new Size(70, 25),
            BackColor = Color.LightGreen
        };
        _startButton.Click += StartButton_Click;
        Controls.Add(_startButton);

        // Stop button
        _stopButton = new Button
        {
            Text = "Stop",
            Location = new Point(630, 285),
            Size = new Size(70, 25),
            BackColor = Color.LightCoral,
            Enabled = false
        };
        _stopButton.Click += StopButton_Click;
        Controls.Add(_stopButton);

        // Status label
        _statusLabel = new Label
        {
            Text = "Status: Stopped",
            Location = new Point(10, 320),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
        };
        Controls.Add(_statusLabel);

        // Log textbox
        _logTextBox = new TextBox
        {
            Location = new Point(10, 350),
            Size = new Size(770, 200),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 8)
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
            _fromKeySelector.Items.Add(key);
            _toKeySelector.Items.Add(key);
        }
        if (_fromKeySelector.Items.Count > 0) _fromKeySelector.SelectedIndex = 0;
        if (_toKeySelector.Items.Count > 1) _toKeySelector.SelectedIndex = 1;

        // Refresh timer
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 3000
        };
        _refreshTimer.Tick += (s, e) => LoadKeyboards();
        _refreshTimer.Start();
    }

    private void LoadConfig()
    {
        _config = ConfigManager.Load();
        RefreshGrid();
    }

    private void LoadKeyboards()
    {
        _keyboardSelector.Items.Clear();

        try
        {
            for (int i = 1; i <= INTERCEPTION_MAX_KEYBOARD; i++)
            {
                if (interception_is_keyboard(i) == 1)
                {
                    string name = GetKeyboardName(i);
                    _keyboardSelector.Items.Add(new KeyboardItem { Id = i, Name = name });
                    Log($"Found keyboard {i}: {name}");
                }
            }
        }
        catch (DllNotFoundException)
        {
            if (!_driverWarningShown)
            {
                _driverWarningShown = true;
                var result = MessageBox.Show(
                    "Interception driver not found.\n\nShould I install it for you?",
                    "Driver Missing",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    InstallDriver();
                }
            }
            _keyboardSelector.Items.Add(new KeyboardItem { Id = 0, Name = "Interception driver not installed" });
            _keyboardSelector.Enabled = false;
            return;
        }

        if (_keyboardSelector.Items.Count > 0)
        {
            _keyboardSelector.Enabled = true;
            _keyboardSelector.SelectedIndex = 0;
        }
    }

    private string GetKeyboardName(int deviceId)
    {
        var buffer = Marshal.AllocHGlobal(1024);
        try
        {
            int len = interception_get_hardware_id(deviceId, buffer, 1024);
            return len > 0 ? Marshal.PtrToStringUni(buffer) ?? $"Keyboard {deviceId}" : $"Keyboard {deviceId}";
        }
        catch
        {
            return $"Keyboard {deviceId}";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
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
        _capturingFrom = true;
        _captureFromButton.Text = "...";
        _captureFromButton.Enabled = false;
        Log("Press a key for SOURCE...");

        Task.Run(() =>
        {
            try
            {
                _captureContext = interception_create_context();
                if (_captureContext == IntPtr.Zero)
                {
                    BeginInvoke(() =>
                    {
                        _captureFromButton.Text = "Press";
                        _captureFromButton.Enabled = true;
                        Log("Failed to create capture context");
                    });
                    return;
                }

                interception_set_filter(_captureContext, INTERCEPTION_KEYBOARD, INTERCEPTION_FILTER_KEYBOARD_ALL);

                var stroke = new KeyStroke();
                while (_capturingFrom)
                {
                    for (int i = 1; i <= INTERCEPTION_MAX_KEYBOARD; i++)
                    {
                        if (interception_is_keyboard(i) != 1) continue;
                        int result = interception_receive(_captureContext, i, ref stroke, 1);
                        if (result == 0) continue;

                        string keyName = GetKeyNameFromScanCode(stroke.Code);
                        BeginInvoke(() =>
                        {
                            _fromKeySelector.SelectedItem = keyName;
                            _captureFromButton.Text = "Press";
                            _captureFromButton.Enabled = true;
                            _capturingFrom = false;
                            Log($"Captured source key: {keyName} (scan code: 0x{stroke.Code:X})");
                        });
                        return;
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(() =>
                {
                    _captureFromButton.Text = "Press";
                    _captureFromButton.Enabled = true;
                    _capturingFrom = false;
                    Log($"Capture error: {ex.Message}");
                });
            }
        });
    }

    private void CaptureToButton_Click(object? sender, EventArgs e)
    {
        _capturingTo = true;
        _captureToButton.Text = "...";
        _captureToButton.Enabled = false;
        Log("Press a key for TARGET...");

        Task.Run(() =>
        {
            try
            {
                if (_captureContext == IntPtr.Zero)
                    _captureContext = interception_create_context();

                if (_captureContext == IntPtr.Zero)
                {
                    BeginInvoke(() =>
                    {
                        _captureToButton.Text = "Press";
                        _captureToButton.Enabled = true;
                        Log("Failed to create capture context");
                    });
                    return;
                }

                interception_set_filter(_captureContext, INTERCEPTION_KEYBOARD, INTERCEPTION_FILTER_KEYBOARD_ALL);

                var stroke = new KeyStroke();
                while (_capturingTo)
                {
                    for (int i = 1; i <= INTERCEPTION_MAX_KEYBOARD; i++)
                    {
                        if (interception_is_keyboard(i) != 1) continue;
                        int result = interception_receive(_captureContext, i, ref stroke, 1);
                        if (result == 0) continue;

                        string keyName = GetKeyNameFromScanCode(stroke.Code);
                        BeginInvoke(() =>
                        {
                            _toKeySelector.SelectedItem = keyName;
                            _captureToButton.Text = "Press";
                            _captureToButton.Enabled = true;
                            _capturingTo = false;
                            Log($"Captured target key: {keyName} (scan code: 0x{stroke.Code:X})");
                        });
                        return;
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(() =>
                {
                    _captureToButton.Text = "Press";
                    _captureToButton.Enabled = true;
                    _capturingTo = false;
                    Log($"Capture error: {ex.Message}");
                });
            }
        });
    }

    private static string GetKeyNameFromScanCode(ushort code)
    {
        return code switch
        {
            0x38 => "LAlt",
            0xE038 => "RAlt",
            0x5B => "LWin",
            0xE05C => "RWin",
            0x1D => "LCtrl",
            0xE01D => "RCtrl",
            0x2A => "LShift",
            0x36 => "RShift",
            0x3B => "F1",
            0x3C => "F2",
            0x3D => "F3",
            0x3E => "F4",
            0x3F => "F5",
            0x40 => "F6",
            0x41 => "F7",
            0x42 => "F8",
            0x43 => "F9",
            0x44 => "F10",
            0x57 => "F11",
            0x58 => "F12",
            0xE052 => "Insert",
            0xE053 => "Delete",
            0xE047 => "Home",
            0xE04F => "End",
            0xE049 => "PageUp",
            0xE051 => "PageDown",
            0xE037 => "PrintScreen",
            0x46 => "ScrollLock",
            0xE045 => "Pause",
            0x3A => "CapsLock",
            0x45 => "NumLock",
            0x01 => "Escape",
            0x39 => "Space",
            0x0F => "Tab",
            0x1C => "Enter",
            0x0E => "Backspace",
            _ => $"Key 0x{code:X}"
        };
    }

    private void AddButton_Click(object? sender, EventArgs e)
    {
        if (_fromKeySelector.SelectedItem == null || _toKeySelector.SelectedItem == null) return;

        string keyboard = _keyboardSelector.Text;
        string from = _fromKeySelector.SelectedItem.ToString()!;
        string to = _toKeySelector.SelectedItem.ToString()!;

        if (string.IsNullOrEmpty(keyboard))
        {
            MessageBox.Show("Please enter or select a keyboard name.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (from == to)
        {
            MessageBox.Show("From and To keys must be different.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.Add(new KeyRemapping { Keyboard = keyboard, From = from, To = to });
        RefreshGrid();
        Log($"Added: {keyboard} | {from} -> {to}");
    }

    private void RemoveButton_Click(object? sender, EventArgs e)
    {
        if (_remappingGrid.SelectedRows.Count == 0) return;

        int index = _remappingGrid.SelectedRows[0].Index;
        if (index >= 0 && index < _config.Count)
        {
            _config.RemoveAt(index);
            RefreshGrid();
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        ConfigManager.Save(_config);
        Log("Config saved");
        MessageBox.Show("Config saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void StartButton_Click(object? sender, EventArgs e)
    {
        ConfigManager.Save(_config);

        try
        {
            Log("Starting remapping...");
            Log($"Config has {_config.Count} mappings");

            _remapper = new InterceptionRemapper(_config);
            if (_remapper.Start())
            {
                _startButton.Enabled = false;
                _stopButton.Enabled = true;
                _statusLabel.Text = "Status: Running";
                _statusLabel.ForeColor = Color.Green;
                Log("Remapping started successfully");
            }
            else
            {
                Log("Failed: interception_create_context returned null");
                MessageBox.Show("Failed to start remapping. Make sure Interception driver is installed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        Log("Remapping stopped");
    }

    private void InstallDriver()
    {
        try
        {
            string url = "https://github.com/oblitum/Interception/releases/latest/download/Interception.zip";
            string tempPath = Path.GetTempPath();
            string zipPath = Path.Combine(tempPath, "Interception.zip");
            string extractPath = Path.Combine(tempPath, "Interception");

            using (var client = new System.Net.WebClient())
            {
                client.DownloadFile(url, zipPath);
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath, true);

            string installerPath = Directory.GetFiles(extractPath, "install-interception.exe", SearchOption.AllDirectories).FirstOrDefault() ?? "";
            if (!string.IsNullOrEmpty(installerPath))
            {
                var result = MessageBox.Show(
                    "Interception driver downloaded.\n\nInstall now? (requires reboot after)",
                    "Install Driver",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = "/install",
                        Verb = "runas",
                        UseShellExecute = true
                    });

                    process?.WaitForExit();

                    try
                    {
                        string dllSource = Directory.GetFiles(extractPath, "interception.dll", SearchOption.AllDirectories).FirstOrDefault() ?? "";
                        if (!string.IsNullOrEmpty(dllSource))
                        {
                            string dllDest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "interception.dll");
                            File.Copy(dllSource, dllDest, true);
                            Log("Copied interception.dll to exe folder");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to copy DLL: {ex.Message}");
                    }

                    MessageBox.Show(
                        "Driver installed.\n\nPlease reboot your computer for changes to take effect.",
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

    private void Log(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logTextBox?.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
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
        _refreshTimer?.Stop();
        _trayIcon?.Dispose();
        base.OnFormClosing(e);
    }
}

public class KeyboardItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public override string ToString() => Name;
}
