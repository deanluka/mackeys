namespace MacKeysRemapGui;

public class TrayIcon : IDisposable
{
    private NotifyIcon _notifyIcon = null!;
    private ContextMenuStrip _contextMenu = null!;
    private Form _form;
    private bool _disposed = false;
    private bool _isFirstRun;
    private bool _isTaskScheduler;
    private ToolStripButton _autoStartItem = null!;

    public TrayIcon(Form form, bool isFirstRun, bool isTaskScheduler = false)
    {
        _form = form;
        _isFirstRun = isFirstRun;
        _isTaskScheduler = isTaskScheduler;
        Initialize();
    }

    private void Initialize()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Show", null, (s, e) => ShowForm());
        _contextMenu.Items.Add("Hide", null, (s, e) => HideForm());
        _contextMenu.Items.Add(new ToolStripSeparator());

        // Auto-start menu item
        _autoStartItem = new ToolStripButton
        {
            Text = IsAutoStartEnabled() ? "✓ Auto-start" : "Auto-start",
            Checked = IsAutoStartEnabled(),
            CheckOnClick = true
        };
        _autoStartItem.Click += (s, e) =>
        {
            SetAutoStart(_autoStartItem.Checked);
        };
        _contextMenu.Items.Add(_autoStartItem);

        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Exit", null, (s, e) => ExitApp());

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "MacKeysRemap - Per-Device Key Remapper",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.Click += (s, e) =>
        {
            // Left click: show GUI directly
            if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
            {
                ShowForm();
            }
        };

        // Task Scheduler: go straight to tray; First run: show window; otherwise: go to tray
        if (_isTaskScheduler)
        {
            _form.Shown += (s, e) =>
            {
                _form.WindowState = FormWindowState.Minimized;
                _form.ShowInTaskbar = false;
                _form.Hide();
            };
        }
        else if (_isFirstRun)
        {
            _form.Shown += (s, e) =>
            {
                _form.WindowState = FormWindowState.Normal;
                _form.ShowInTaskbar = true;
            };
        }
        else
        {
            _form.Shown += (s, e) =>
            {
                _form.WindowState = FormWindowState.Minimized;
                _form.ShowInTaskbar = false;
                _form.Hide();
            };
        }
    }

    private void ShowForm()
    {
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.ShowInTaskbar = true;
        _form.Activate();
    }

    private void HideForm()
    {
        _form.Hide();
        _form.ShowInTaskbar = false;
    }

    private void ExitApp()
    {
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", false);
            return key?.GetValue("MacKeysRemap") != null;
        }
        catch
        {
            return false;
        }
    }

    private static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enable)
            {
                key.SetValue("MacKeysRemap", Application.ExecutablePath);
            }
            else
            {
                key.DeleteValue("MacKeysRemap", false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to set auto-start: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
    }
}
