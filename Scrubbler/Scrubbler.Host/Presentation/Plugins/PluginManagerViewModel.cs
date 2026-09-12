using Scrubbler.Host.Presentation.Navigation;
using Scrubbler.Host.Services;

namespace Scrubbler.Host.Presentation.Plugins;

internal class PluginManagerViewModel : ObservableObject, INavigationStatusInfo
{
    #region Properties

    public InstalledPluginsViewModel Installed { get; }

    public AvailablePluginsViewModel Available { get; }

    public bool IsSelected { get; set; }

    public event EventHandler<NavigationStatusEventArgs>? NavigationStatusChanged;

    public NavigationStatusEventArgs NavigationStatus => Installed.NavigationStatus;

    #endregion Properties

    public PluginManagerViewModel(IPluginManager manager)
    {
        Installed = new InstalledPluginsViewModel(manager);
        Installed.NavigationStatusChanged += Installed_NavigationStatusChanged;
        Available = new AvailablePluginsViewModel(manager);
    }

    private void Installed_NavigationStatusChanged(object? sender, NavigationStatusEventArgs e)
    {
        NavigationStatusChanged?.Invoke(this, e);
    }
}
