namespace Scrubbler.Host.Presentation.Navigation;

internal interface INavigationStatusInfo
{
    event EventHandler<NavigationStatusEventArgs>? NavigationStatusChanged;

    /// <summary>
    /// Gets the current status, including changes made before an event subscriber attached.
    /// </summary>
    NavigationStatusEventArgs NavigationStatus { get; }

    /// <summary>
    /// Gets if this item is selected, possibly clearing the status.
    /// </summary>
    bool IsSelected { get; set; }
}
