using System;
using System.Threading.Tasks;
using ClipFlow.Core.Models;

namespace ClipFlow.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Settings { get; }
    event EventHandler<AppSettings>? SettingsChanged;
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
    void ResetToDefaults();
}
