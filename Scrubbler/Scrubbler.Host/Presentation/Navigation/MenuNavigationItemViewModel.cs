using System.Collections.ObjectModel;

namespace Scrubbler.Host.Presentation.Navigation;

internal partial class MenuNavigationItemViewModel : NavigationItemViewModelBase
{
    #region Properties

    public IconSource? Icon { get; }

    public ObservableCollection<NavigationItemViewModelBase> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded; // for groups

    public bool HasChildren => Children.Count > 0;

    public override bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (IsSelected != value)
            {
                if (Content is INavigationStatusInfo n)
                    n.IsSelected = value;

                SetProperty(ref _isSelected, value);
            }
        }
    }
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    private int _errors;

    public bool HasErrors => Errors > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    private int _warnings;

    public bool HasWarnings => Warnings > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInfos))]
    private int _infos;

    public bool HasInfos => Infos > 0;

    #endregion Properties

    public MenuNavigationItemViewModel(string title, IconSource? icon, object? content = null)
        : base(title, content)
    {
        Icon = icon;

        if (content is INavigationStatusInfo n)
        {
            n.NavigationStatusChanged += Content_NavigationStatusChanged;
            Content_NavigationStatusChanged(n, n.NavigationStatus);
        }
    }

    private void Content_NavigationStatusChanged(object? sender, NavigationStatusEventArgs e)
    {
        Errors = e.Errors;
        Warnings = e.Warnings;
        Infos = e.Infos;
    }
}
