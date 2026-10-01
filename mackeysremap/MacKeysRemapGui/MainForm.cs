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
    private System.Windows.Forms.Timer _refreshTimer = null!;

    private List<KeyRemapping> _config = new();
    private InterceptionRemapper? _remapper;
    private TrayIcon? _trayIcon;
    private bool _driverWarningShown = false;

    // Interception P/Invoke
    private const string InterceptionDll = "interception.dll";
    private const int INTERCEPTION_KEYBOARD = 1;
    private const int INTERCEPTION_MAX_KEYBOARD = 1;
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

        // First run: no config file existed before; show window
        // Subsequent runs: config exists; go straight to tray
        bool isFirstRun = !File.Exists(ConfigManager.GetConfigPath());
        _trayIcon = new TrayIcon(this, isFirstRun);

        // Enable auto-start on first run
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
    }

    private void InitializeComponent()
    {
        Text = "MacKeysRemap - Per-Device Key Remapper";
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // Keyboard selector (for adding mappings)
        var keyboardLabel = new Label
        {
            Text = "Add mapping for:",
            Location = new Point(20, 20),
            AutoSize = true
        };
        Controls.Add(keyboardLabel);

        _keyboardSelector = new ComboBox
        {
            Location = new Point(150, 17),
            Size = new Size(250, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_keyboardSelector);

        // Refresh button
        var refreshButton = new Button
        {
            Text = "Refresh",
            Location = new Point(420, 16),
            Size = new Size(80, 25)
        };
        refreshButton.Click += (s, e) => LoadKeyboards();
        Controls.Add(refreshButton);

        // From key
        var fromLabel = new Label
        {
            Text = "From Key:",
            Location = new Point(20, 60),
            AutoSize = true
        };
        Controls.Add(fromLabel);

        _fromKeySelector = new ComboBox
        {
            Location = new Point(150, 57),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_fromKeySelector);

        // To key
        var toLabel = new Label
        {
            Text = "To Key:",
            Location = new Point(370, 60),
            AutoSize = true
        };
        Controls.Add(toLabel);

        _toKeySelector = new ComboBox
        {
            Location = new Point(450, 57),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_toKeySelector);

        // Add button
        _addButton = new Button
        {
            Text = "Add",
            Location = new Point(670, 56),
            Size = new Size(80, 25)
        };
        _addButton.Click += AddButton_Click;
        Controls.Add(_addButton);

        // Remapping grid
        _remappingGrid = new DataGridView
        {
            Location = new Point(20, 100),
            Size = new Size(740, 300),
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
            Text = "Remove Selected",
            Location = new Point(20, 410),
            Size = new Size(120, 30)
        };
        _removeButton.Click += RemoveButton_Click;
        Controls.Add(_removeButton);

        // Save button
        _saveButton = new Button
        {
            Text = "Save Config",
            Location = new Point(160, 410),
            Size = new Size(100, 30)
        };
        _saveButton.Click += SaveButton_Click;
        Controls.Add(_saveButton);

        // Start button
        _startButton = new Button
        {
            Text = "Start Remapping",
            Location = new Point(500, 410),
            Size = new Size(120, 30),
            BackColor = Color.LightGreen
        };
        _startButton.Click += StartButton_Click;
        Controls.Add(_startButton);

        // Stop button
        _stopButton = new Button
        {
            Text = "Stop Remapping",
            Location = new Point(640, 410),
            Size = new Size(120, 30),
            BackColor = Color.LightCoral,
            Enabled = false
        };
        _stopButton.Click += StopButton_Click;
        Controls.Add(_stopButton);

        // Status label
        _statusLabel = new Label
        {
            Text = "Status: Stopped",
            Location = new Point(20, 460),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
        };
        Controls.Add(_statusLabel);

        // Config path label
        var configPathLabel = new Label
        {
            Text = $"Config: {ConfigManager.GetConfigPath()}",
            Location = new Point(20, 500),
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
            Interval = 2000
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
            return len > 0 ? Marshal.PtrToStringAnsi(buffer) ?? $"Keyboard {deviceId}" : $"Keyboard {deviceId}";
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

    private void AddButton_Click(object? sender, EventArgs e)
    {
        if (_keyboardSelector.SelectedItem is not KeyboardItem item) return;
        if (_fromKeySelector.SelectedItem == null || _toKeySelector.SelectedItem == null) return;

        string keyboard = item.Name;
        string from = _fromKeySelector.SelectedItem.ToString()!;
        string to = _toKeySelector.SelectedItem.ToString()!;

        if (from == to)
        {
            MessageBox.Show("From and To keys must be different.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _config.Add(new KeyRemapping { Keyboard = keyboard, From = from, To = to });
        RefreshGrid();
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
        MessageBox.Show("Config saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void StartButton_Click(object? sender, EventArgs e)
    {
        ConfigManager.Save(_config);

        _remapper = new InterceptionRemapper(_config);
        if (_remapper.Start())
        {
            _startButton.Enabled = false;
            _stopButton.Enabled = true;
            _statusLabel.Text = "Status: Running";
            _statusLabel.ForeColor = Color.Green;
        }
        else
        {
            MessageBox.Show("Failed to start remapping. Make sure Interception driver is installed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            // Find installer recursively (it may be in a subfolder)
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
            MessageBox.Show($"Failed to install driver: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
