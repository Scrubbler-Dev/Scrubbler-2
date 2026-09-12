using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Scrubbler.Host.Presentation.Navigation;
using Scrubbler.Host.Services;
using Scrubbler.Host.Services.Logging;
using Scrubbler.PluginBase.Services;

namespace Scrubbler.Host.Presentation.Logging;

internal partial class LogViewModel : ObservableObject, IHostedService, INavigationStatusInfo
{
    #region Properties

    public ObservableCollection<LogMessage> Entries { get; } = [];

    public ObservableCollection<LogMessage> FilteredEntries { get; } = [];

    public bool CanSave => FilteredEntries.Count > 0;

    public ObservableCollection<LogLevelFilterViewModel> LogLevelFilters { get; } = [];

    public ObservableCollection<string> Modules { get; } = [];

    [ObservableProperty]
    private string _selectedModule = ALLMODULE;

    [ObservableProperty]
    private string _searchText = string.Empty;

    private readonly IUserFeedbackService _userFeedbackService;
    private readonly IFilePickerService _filePickerService;
    private readonly IFileStorageService _fileStorageService;

    private static readonly string[] _textFiles = [".txt"];
    private const string ALLMODULE = "All";

    public event EventHandler<NavigationStatusEventArgs>? NavigationStatusChanged;

    public NavigationStatusEventArgs NavigationStatus => new(_recentErrors, _recentWarnings, 0);

    [ObservableProperty]
    private bool _isSelected;

    private int _recentErrors;
    private int _recentWarnings;

    #endregion Properties

    #region Construction

    public LogViewModel(HostLogService hostLogService, IUserFeedbackService userFeedbackService, IFilePickerService filePickerService, IFileStorageService fileStorageService)
    {
        hostLogService.MessageLogged += Add;
        _userFeedbackService = userFeedbackService;
        _filePickerService = filePickerService;
        _fileStorageService = fileStorageService;

        foreach (LogLevel level in Enum.GetValues<LogLevel>())
        {
            var filterVm = new LogLevelFilterViewModel(level);
            filterVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LogLevelFilterViewModel.IsEnabled))
                {
                    RebuildFilteredEntries();
                }
            };
            LogLevelFilters.Add(filterVm);
        }

        RebuildModulesList();
    }

    #endregion Construction

    [RelayCommand]
    private void Clear()
    {
        Entries.Clear();
        FilteredEntries.Clear();
        RebuildModulesList();
        SelectedModule = ALLMODULE;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        if (!CanSave)
            return;

        var file = await _filePickerService.SaveFileAsync("scrubbler_log", new Dictionary<string, IReadOnlyList<string>>
                                                                           {
                                                                               { "Text file", _textFiles }
                                                                           });
        if (file != null)
            await _fileStorageService.WriteLinesAsync(file, FilteredEntries.Select(l => l.ToString()));
    }

    private void RebuildFilteredEntries()
    {
        FilteredEntries.Clear();
        foreach (var entry in Entries)
        {
            if (MatchesFilter(entry))
                FilteredEntries.Add(entry);
        }

        SaveCommand.NotifyCanExecuteChanged();
    }

    private void RebuildModulesList()
    {
        Modules.Clear();
        Modules.Add(ALLMODULE);
        var modules = Entries.Select(e => e.Module).Distinct().OrderBy(m => m);
        foreach (var module in modules)
        {
            Modules.Add(module);
        }
    }

    private bool MatchesFilter(LogMessage entry)
    {
        // search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            if (!entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                (entry.Exception == null || !entry.Exception.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        // module
        if (SelectedModule != ALLMODULE && entry.Module != SelectedModule)
            return false;

        // log level
        foreach (var filter in LogLevelFilters)
        {
            if (!filter.PassesFilter(entry))
                return false;
        }

        return true;
    }


    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private void Add(LogMessage entry)
    {
        Entries.Add(entry);

        RebuildModulesList();

        if (MatchesFilter(entry))
            FilteredEntries.Add(entry);

        if (entry.Level == LogLevel.Error || entry.Level == LogLevel.Critical)
        {
            _userFeedbackService.ShowError($"{entry.Message} | error: {entry.Exception?.Message ?? "Unknown Error"}");

            if (!IsSelected)
            {
                _recentErrors++;
                NavigationStatusChanged?.Invoke(this, NavigationStatus);
            }
        }
        else if (entry.Level == LogLevel.Warning)
        {
            _userFeedbackService.ShowWarning(entry.Message);

            if (!IsSelected)
            {
                _recentWarnings++;
                NavigationStatusChanged?.Invoke(this, NavigationStatus);
            }
        }
    }

    partial void OnSelectedModuleChanged(string value)
    {
        RebuildFilteredEntries();
    }

    partial void OnSearchTextChanged(string value)
    {
        RebuildFilteredEntries();
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            _recentErrors = 0;
            _recentWarnings = 0;
            NavigationStatusChanged?.Invoke(this, NavigationStatus);
        }
    }
}
