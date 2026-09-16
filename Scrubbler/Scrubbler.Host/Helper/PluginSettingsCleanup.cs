namespace Scrubbler.Host.Helper;

internal static class PluginSettingsCleanup
{
    /// <summary>Deletes the settings and credentials owned by one explicitly uninstalled plugin.</summary>
    internal static void Delete(string pluginName, string? applicationData = null)
    {
        // Plugin display names currently define their settings directory. Never let
        // a malformed name select the shared root or a directory outside it.
        if (string.IsNullOrWhiteSpace(pluginName) || pluginName is "." or ".."
            || pluginName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || pluginName.Contains('/') || pluginName.Contains('\\')
            || pluginName.EndsWith('.') || pluginName.EndsWith(' '))
            throw new ArgumentException("Invalid plugin settings directory name.", nameof(pluginName));

        var root = Path.GetFullPath(Path.Combine(applicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Scrubbler", "Plugins"));
        var target = Path.GetFullPath(Path.Combine(root, pluginName));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(target), root, comparison))
            throw new InvalidOperationException("Plugin settings must be a direct child of the plugin settings root.");
        if (!Directory.Exists(target))
            return;

        // Reject redirected folders rather than risking deletion of unrelated data.
        foreach (var directory in new[] { Path.GetDirectoryName(root)!, root, target })
            if ((File.GetAttributes(directory) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cannot remove plugin settings through a redirected directory.");
        // Check one directory at a time so links are rejected before recursing into them.
        var pending = new Stack<string>();
        pending.Push(target);
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & System.IO.FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Cannot remove plugin settings containing redirected files or folders.");
                if ((attributes & System.IO.FileAttributes.Directory) != 0)
                    pending.Push(entry);
            }
        }

        Directory.Delete(target, recursive: true);
    }
}
