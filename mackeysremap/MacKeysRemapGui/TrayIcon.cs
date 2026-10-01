namespace MacKeysRemapGui;

public class TrayIcon : IDisposable
{
    private NotifyIcon _notifyIcon = null!;
    private ContextMenuStrip _contextMenu = null!;
    private Form _form;

    public TrayIcon(Form form)
    {
        _form = form;
        Initialize();
    }

    private void Initialize()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Show", null, (s, e) => ShowForm());
        _contextMenu.Items.Add("Hide", null, (s, e) => HideForm());
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Exit", null, (s, e) => ExitApp());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "MacKeysRemap - Per-Device Key Remapper",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => ShowForm();

        _form.ResumeLayout();
        _form.WindowState = FormWindowState.Minimized;
        _form.ShowInTaskbar = false;
        _form.Hide();
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

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
    }
}
