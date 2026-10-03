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
            using var process = Process.GetCurrentProcess();
            var parent = process.Parent();
            return parent?.ProcessName?.Equals("taskeng", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    // Note: Parent() extension requires System.Management package

    public static void EnsureTaskExists()
    {
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
                // Task exists — update path if needed
                var update = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks",
                        Arguments = $"/Change /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    }
                };
                update.Start();
                update.WaitForExit();
            }
            else
            {
                // Create task
                var create = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "schtasks",
                        Arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST /F",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    }
                };
                create.Start();
                create.WaitForExit();
            }
        }
        catch { }
    }

    public static void RemoveTask()
    {
        try
        {
            var remove = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/Delete /TN \"{TaskName}\" /F",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                }
            };
            remove.Start();
            remove.WaitForExit();
        }
        catch { }
    }
}

public static class ProcessExtensions
{
    public static Process? Parent(this Process process)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}");
            foreach (var obj in searcher.Get())
            {
                var parentId = Convert.ToInt32(obj["ParentProcessId"]);
                return Process.GetProcessById(parentId);
            }
        }
        catch { }
        return null;
    }
}
