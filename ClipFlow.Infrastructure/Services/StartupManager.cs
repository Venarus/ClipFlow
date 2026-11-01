using System;
using System.Diagnostics;
using System.IO;
using ClipFlow.Core.Interfaces;
using Microsoft.Win32;

namespace ClipFlow.Infrastructure.Services;

public class StartupManager : IStartupManager
{
    private const string AppName = "ClipFlow";
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsRunAtStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            var value = key?.GetValue(AppName) as string;
            if (string.IsNullOrEmpty(value)) return false;

            var currentExe = Environment.ProcessPath;
            return !string.IsNullOrEmpty(currentExe) && value.Contains(Path.GetFileName(currentExe), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public bool SetRunAtStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (key == null) return false;

            if (enable)
            {
                var currentExe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(currentExe))
                {
                    key.SetValue(AppName, $"\"{currentExe}\" --minimized");
                    return true;
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update startup registry: {ex.Message}");
        }

        return false;
    }
}
