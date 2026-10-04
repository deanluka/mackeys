using System;
using System.Diagnostics;

namespace MacKeysRemapGui;

public static class TaskSchedulerHelper
{
    private const string TaskName = "MacKeysRemap";

    public static bool IsStartedFromTaskScheduler()
    {
        try
        {
            return Environment.GetCommandLineArgs().Contains("/tray");
        }
        catch
        {
            return false;
        }
    }

    public static void EnsureTaskExists()
    {
        // If started from task scheduler, don't create/update task
        if (IsStartedFromTaskScheduler())
            return;

        try
        {
            string exePath = System.Windows.Forms.Application.ExecutablePath;

            // Check if task exists
            var check = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Query /TN \"{TaskName}\" /FO LIST",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            check.Start();
            check.WaitForExit();

            if (check.ExitCode == 0)
            {
                // Task exists — no need to update
                return;
            }

            // Create task
            var create = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" /tray\" /SC ONLOGON /RL HIGHEST /F",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                }
            };
            create.Start();
            create.WaitForExit();
        }
        catch { }
    }
}
