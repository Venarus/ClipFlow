using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClipFlow.Core.Interfaces;
using ClipFlow.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ClipFlow.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IClipboardStorageRepository _repository;
    private readonly IClipboardMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly IStartupManager _startupManager;
    private readonly IHotkeyService _hotkeyService;
    private readonly Dispatcher _dispatcher;

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private ObservableCollection<ClipboardItemViewModel> _items = [];

    [ObservableProperty]
    private ClipboardItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _currentSection = "All";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private DateFilterOption _selectedDateFilter = DateFilterOption.All;

    [ObservableProperty]
    private SortOption _selectedSortOption = SortOption.Newest;

    [ObservableProperty]
    private bool _isMultiSelectMode;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _isMonitoring = true;

    [ObservableProperty]
    private string _monitoringStatusText = "Monitoring Active";

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private AppSettings _settings;

    [ObservableProperty]
    private StorageStats? _storageStats;

    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _excludedApps = [];

    [ObservableProperty]
    private string _newExcludedAppName = string.Empty;

    [ObservableProperty]
    private string _toastMessage = string.Empty;

    [ObservableProperty]
    private bool _isToastVisible;



    public MainViewModel(
        IClipboardStorageRepository repository,
        IClipboardMonitorService monitorService,
        ISettingsService settingsService,
        IStartupManager startupManager,
        IHotkeyService hotkeyService)
    {
        _repository = repository;
        _monitorService = monitorService;
        _settingsService = settingsService;
        _startupManager = startupManager;
        _hotkeyService = hotkeyService;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _settings = _settingsService.Settings;
        _isMonitoring = _monitorService.IsMonitoring;

        // Wire monitor events
        _monitorService.ClipboardItemCaptured += OnClipboardItemCaptured;
        _monitorService.MonitoringStateChanged += (s, active) =>
        {
            _dispatcher.Invoke(() =>
            {
                IsMonitoring = active;
                MonitoringStatusText = active ? "Monitoring Active" : "Monitoring Paused";
            });
        };

        // Wire hotkey events
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
    }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        await _settingsService.LoadSettingsAsync();
        Settings = _settingsService.Settings;

        // Populate excluded apps
        ExcludedApps = new ObservableCollection<string>(Settings.ExcludedApplications);

        await RefreshCategoriesAsync();
        await LoadHistoryAsync();
        await RefreshStorageStatsAsync();
    }

    private void OnClipboardItemCaptured(object? sender, ClipboardItemEventArgs e)
    {
        _dispatcher.Invoke(async () =>
        {
            // Add or bump in storage
            await _repository.AddOrUpdateAsync(e.Item);

            // Reload history to reflect updated order and counts
            await LoadHistoryAsync();
            await RefreshStorageStatsAsync();

            ShowToast($"Copied {e.Item.ContentType} from {e.Item.SourceApplication ?? "System"}");
        });
    }

    private void OnHotkeyPressed(object? sender, string actionKey)
    {
        _dispatcher.Invoke(async () =>
        {
            switch (actionKey)
            {
                case "ToggleWindow":
                    // Handled in MainWindow
                    break;
                case "ClearHistory":
                    await ClearAllHistoryAsync();
                    break;
                case "ToggleMonitoring":
                    ToggleMonitoring();
                    break;
            }
        });
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        Task.Delay(200, token).ContinueWith(async t =>
        {
            if (!t.IsCanceled)
            {
                await _dispatcher.InvokeAsync(LoadHistoryAsync);
            }
        }, token);
    }

    partial void OnSelectedDateFilterChanged(DateFilterOption value)
    {
        _ = LoadHistoryAsync();
    }

    partial void OnSelectedSortOptionChanged(SortOption value)
    {
        _ = LoadHistoryAsync();
    }

    partial void OnCurrentSectionChanged(string value)
    {
        OnPropertyChanged(nameof(SectionDisplayName));
        _ = LoadHistoryAsync();
    }

    public string SectionDisplayName => CurrentSection switch
    {
        "Text" => "Text & Code",
        "Images" => "Images",
        "Links" => "Links & URLs",
        "Files" => "Files & Folders",
        "Favorites" => "Favorites",
        "Pinned" => "Pinned Items",
        "RecentlyUsed" => "Recently Used",
        _ => "All Items"
    };

    public async Task LoadHistoryAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            var filter = new FilterCriteria
            {
                SearchQuery = SearchQuery,
                DateFilter = SelectedDateFilter,
                SortBy = SelectedSortOption,
                Limit = 400
            };

            // Section filter
            switch (CurrentSection)
            {
                case "Text":
                    filter.ContentTypes = [ContentType.Text, ContentType.RichText, ContentType.Html, ContentType.Code, ContentType.Email, ContentType.Phone];
                    break;
                case "Images":
                    filter.ContentType = ContentType.Image;
                    break;
                case "Links":
                    filter.ContentType = ContentType.Url;
                    break;
                case "Files":
                    filter.ContentType = ContentType.FileDrop;
                    break;
                case "Favorites":
                    filter.OnlyFavorites = true;
                    break;
                case "Pinned":
                    filter.OnlyPinned = true;
                    break;
                case "RecentlyUsed":
                    filter.SortBy = SortOption.MostUsed;
                    break;
                case "Categories":
                    if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "All")
                    {
                        filter.Category = SelectedCategory;
                    }
                    break;
            }

            var dbItems = await _repository.GetItemsAsync(filter);
            var vms = dbItems.Select(x =>
            {
                var vm = new ClipboardItemViewModel(x);
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ClipboardItemViewModel.IsSelected))
                    {
                        UpdateSelectedCount();
                    }
                };
                return vm;
            }).ToList();

            Items = new ObservableCollection<ClipboardItemViewModel>(vms);

            // Maintain or update selection
            if (SelectedItem != null)
            {
                SelectedItem = Items.FirstOrDefault(i => i.Id == SelectedItem.Id) ?? Items.FirstOrDefault();
            }
            else
            {
                SelectedItem = Items.FirstOrDefault();
            }

            UpdateSelectedCount();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    [RelayCommand]
    public async Task SelectSectionAsync(string section)
    {
        if (string.Equals(section, "Settings", StringComparison.OrdinalIgnoreCase))
        {
            IsSettingsOpen = true;
            return;
        }

        IsSettingsOpen = false;
        CurrentSection = section;
        OnPropertyChanged(nameof(SectionDisplayName));
        await LoadHistoryAsync();
    }

    [RelayCommand]
    public async Task CopyItemAsync(ClipboardItemViewModel? vm)
    {
        var target = vm ?? SelectedItem;
        if (target == null) return;

        bool success = await _monitorService.CopyToClipboardAsync(target.Model);
        if (success)
        {
            await _repository.IncrementUseCountAsync(target.Id);
            ShowToast("Copied to clipboard!");
        }
        else
        {
            ShowToast("Failed to copy to clipboard");
        }
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync(ClipboardItemViewModel? vm)
    {
        var target = vm ?? SelectedItem;
        if (target == null) return;

        target.IsFavorite = !target.IsFavorite;
        await _repository.ToggleFavoriteAsync(target.Id);
        await RefreshStorageStatsAsync();
    }

    [RelayCommand]
    public async Task TogglePinAsync(ClipboardItemViewModel? vm)
    {
        var target = vm ?? SelectedItem;
        if (target == null) return;

        target.IsPinned = !target.IsPinned;
        await _repository.TogglePinAsync(target.Id);
        await RefreshStorageStatsAsync();
    }

    [RelayCommand]
    public async Task DeleteItemAsync(ClipboardItemViewModel? vm)
    {
        var target = vm ?? SelectedItem;
        if (target == null) return;

        await _repository.DeleteItemAsync(target.Id);
        Items.Remove(target);
        if (SelectedItem == target)
        {
            SelectedItem = Items.FirstOrDefault();
        }
        await RefreshStorageStatsAsync();
        ShowToast("Item deleted");
    }

    [RelayCommand]
    public void ToggleMultiSelect()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        if (!IsMultiSelectMode)
        {
            foreach (var item in Items)
            {
                item.IsSelected = false;
            }
            UpdateSelectedCount();
        }
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var item in Items)
        {
            item.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var item in Items)
        {
            item.IsSelected = false;
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;

        var ids = selected.Select(i => i.Id).ToList();
        int count = await _repository.DeleteItemsAsync(ids);

        foreach (var item in selected)
        {
            Items.Remove(item);
        }

        if (SelectedItem != null && selected.Contains(SelectedItem))
        {
            SelectedItem = Items.FirstOrDefault();
        }

        IsMultiSelectMode = false;
        UpdateSelectedCount();
        await RefreshStorageStatsAsync();
        ShowToast($"Deleted {count} items");
    }

    [RelayCommand]
    public async Task FavoriteSelectedAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        foreach (var item in selected)
        {
            if (!item.IsFavorite)
            {
                item.IsFavorite = true;
                await _repository.ToggleFavoriteAsync(item.Id);
            }
        }
        ClearSelection();
        IsMultiSelectMode = false;
        await RefreshStorageStatsAsync();
        ShowToast("Added selected to favorites");
    }

    [RelayCommand]
    public async Task ClearAllHistoryAsync()
    {
        var res = MessageBox.Show(
            "Are you sure you want to clear clipboard history?\nPinned items will be preserved.",
            "ClipFlow - Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            int deleted = await _repository.ClearHistoryAsync(preservePinned: true);
            await LoadHistoryAsync();
            await RefreshStorageStatsAsync();
            ShowToast($"Cleared {deleted} history items");
        }
    }

    [RelayCommand]
    public void ToggleMonitoring()
    {
        if (_monitorService.IsMonitoring)
        {
            _monitorService.PauseMonitoring();
            Settings.IsMonitoringEnabled = false;
        }
        else
        {
            _monitorService.ResumeMonitoring();
            Settings.IsMonitoringEnabled = true;
        }
        _ = _settingsService.SaveSettingsAsync();
    }

    [RelayCommand]
    public void OpenSettings()
    {
        IsSettingsOpen = true;
    }

    [RelayCommand]
    public void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        // Update excluded list in model
        Settings.ExcludedApplications = ExcludedApps.ToList();

        // Apply startup configuration
        _startupManager.SetRunAtStartup(Settings.StartWithWindows);

        await _settingsService.SaveSettingsAsync();
        ShowToast("Settings saved successfully");
    }

    [RelayCommand]
    public void ResetSettings()
    {
        _settingsService.ResetToDefaults();
        Settings = _settingsService.Settings;
        ExcludedApps = new ObservableCollection<string>(Settings.ExcludedApplications);
        ShowToast("Settings reset to defaults");
    }

    [RelayCommand]
    public async Task RunCleanupNowAsync()
    {
        int deleted = await _repository.RunAutoCleanupAsync(Settings.AutoCleanupDays, Settings.MaxHistoryItems);
        await LoadHistoryAsync();
        await RefreshStorageStatsAsync();
        ShowToast($"Cleanup complete: {deleted} items removed");
    }

    [RelayCommand]
    public async Task VacuumDatabaseAsync()
    {
        await _repository.VacuumDatabaseAsync();
        await RefreshStorageStatsAsync();
        ShowToast("Database vacuumed & optimized");
    }

    [RelayCommand]
    public async Task BackupDatabaseAsync()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Backup ClipFlow Database",
            Filter = "SQLite Database (*.db)|*.db",
            FileName = $"ClipFlow_Backup_{DateTime.Now:yyyyMMdd_HHmm}.db"
        };

        if (sfd.ShowDialog() == true)
        {
            await _repository.BackupDatabaseAsync(sfd.FileName);
            ShowToast("Database backup saved successfully");
        }
    }

    [RelayCommand]
    public async Task RestoreDatabaseAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Restore ClipFlow Database",
            Filter = "SQLite Database (*.db)|*.db"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                await _repository.RestoreDatabaseAsync(ofd.FileName);
                await LoadHistoryAsync();
                await RefreshStorageStatsAsync();
                ShowToast("Database restored successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restore database: {ex.Message}", "ClipFlow Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void AddExcludedApp()
    {
        if (!string.IsNullOrWhiteSpace(NewExcludedAppName))
        {
            var clean = NewExcludedAppName.Trim();
            if (!ExcludedApps.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                ExcludedApps.Add(clean);
                Settings.ExcludedApplications = ExcludedApps.ToList();
                _ = _settingsService.SaveSettingsAsync();
                NewExcludedAppName = string.Empty;
                ShowToast($"Added {clean} to exclusion list");
            }
        }
    }

    [RelayCommand]
    public void RemoveExcludedApp(string? appName)
    {
        if (!string.IsNullOrEmpty(appName) && ExcludedApps.Contains(appName))
        {
            ExcludedApps.Remove(appName);
            Settings.ExcludedApplications = ExcludedApps.ToList();
            _ = _settingsService.SaveSettingsAsync();
            ShowToast($"Removed {appName} from exclusion list");
        }
    }

    [RelayCommand]
    public void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowToast($"Could not open URL: {ex.Message}");
        }
    }

    [RelayCommand]
    public void OpenFileInExplorer(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            else if (Directory.Exists(filePath))
            {
                Process.Start("explorer.exe", $"\"{filePath}\"");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Could not open path: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SaveImageAs(ClipboardItemViewModel? vm)
    {
        var target = vm ?? SelectedItem;
        if (target == null || string.IsNullOrEmpty(target.ImageStoragePath) || !File.Exists(target.ImageStoragePath))
        {
            return;
        }

        var sfd = new SaveFileDialog
        {
            Title = "Save Image",
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*",
            FileName = $"ClipFlow_Image_{target.CreatedAt:yyyyMMdd_HHmmss}.png"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.Copy(target.ImageStoragePath, sfd.FileName, true);
                ShowToast("Image saved");
            }
            catch (Exception ex)
            {
                ShowToast($"Failed to save image: {ex.Message}");
            }
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = Items.Count(i => i.IsSelected);
    }

    private async Task RefreshCategoriesAsync()
    {
        var cats = await _repository.GetCategoriesAsync();
        Categories = new ObservableCollection<string>(cats);
    }

    private async Task RefreshStorageStatsAsync()
    {
        StorageStats = await _repository.GetStorageStatsAsync();
    }

    public void ShowToast(string message)
    {
        ToastMessage = message;
        IsToastVisible = true;

        Task.Delay(2500).ContinueWith(_ =>
        {
            _dispatcher.Invoke(() =>
            {
                IsToastVisible = false;
            });
        });
    }
}
