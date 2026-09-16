using System.Reflection;
using System.Text.Json;
using CommunityToolkit.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Scrubbler.Host.Helper;
using Scrubbler.PluginBase.Plugin;
using Scrubbler.PluginBase.Plugin.Account;
using Scrubbler.PluginBase.Services;
using Scrubbler.PluginBase.Settings;

namespace Scrubbler.Host.Services;

internal class PluginManager : IPluginManager
{
    #region Properties

    private readonly ILogService _logService;
    private readonly ISettingsStore _settings;
    private readonly IWritableOptions<UserConfig> _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherQueue _dispatcherQueue;
    private Task? _initializationTask;
    private readonly string _rootDir;
    private readonly string _shadowRoot;

    public bool IsFetchingPlugins
    {
        get => _isFetchingPlugins;
        private set
        {
            if (IsFetchingPlugins != value)
            {
                _isFetchingPlugins = value;
                IsFetchingPluginsChanged?.Invoke(this, IsFetchingPlugins);
            }
        }
    }
    private bool _isFetchingPlugins;

    public bool IsAnyAccountPluginScrobbling => InstalledPlugins.OfType<IAccountPlugin>().Any(p => p.IsScrobblingEnabled);
    public event EventHandler? IsAnyAccountPluginScrobblingChanged;

    public event EventHandler<bool>? IsFetchingPluginsChanged;
    public event EventHandler? PluginInstalled;
    public event EventHandler? PluginUninstalled;
    public event EventHandler<IPlugin>? PluginUnloading;

    #endregion Properties

    #region Construction

    public PluginManager(IModuleLogServiceFactory logFactory, ISettingsStore settings, IWritableOptions<UserConfig> config, IServiceProvider serviceProvider, DispatcherQueue dispatcherQueue)
    {
        _settings = settings;
        _config = config;
        _serviceProvider = serviceProvider;
        _dispatcherQueue = dispatcherQueue;
        _logService = logFactory.Create("Plugin Manager");

        if (Environment.GetEnvironmentVariable("SCRUBBLER_PLUGIN_MODE") == "Debug")
            _rootDir = Path.Combine(Environment.GetEnvironmentVariable("SOLUTIONDIR")!, "DebugPlugins");
        else
            _rootDir = Path.Combine(AppContext.BaseDirectory, "Plugins");

        _shadowRoot = Path.Combine(_rootDir, ".shadow");

        Directory.CreateDirectory(_rootDir);
        Directory.CreateDirectory(_shadowRoot);

        CleanupShadowRoot();

        Application.Current.Suspending += async (s, e) =>
        {
            await SaveAllPluginsAsync();
        };
    }

    #endregion Construction

    #region Plugin lists

    public IEnumerable<IPlugin> InstalledPlugins => _installed.Select(p => p.Plugin);

    private readonly List<InstalledPluginEntry> _installed = [];

    public List<PluginManifestEntry> AvailablePlugins { get; private set; } = [];

    #endregion Plugin lists

    #region Public API

    public Task InitializeAsync() => _dispatcherQueue.EnqueueAsync(() =>
        _initializationTask ??= InitializeCoreAsync());

    public async Task InstallAsync(PluginManifestEntry manifest)
    {
        var pluginDir = Path.Combine(_rootDir, manifest.Id);

        if (Directory.Exists(pluginDir))
            Directory.Delete(pluginDir, recursive: true);

        Directory.CreateDirectory(pluginDir);

        var zipPath = Path.Combine(pluginDir, $"{manifest.Id}.zip");

        using (var http = new HttpClient())
        {
            var data = await http.GetByteArrayAsync(manifest.SourceUri);
            await File.WriteAllBytesAsync(zipPath, data);
        }

        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, pluginDir);
        File.Delete(zipPath);

        // load only the new plugin
        await LoadPluginsFromDirectory(pluginDir, recursive: false);

        UpdateAccountFunctionsReceiver();
        PluginInstalled?.Invoke(this, EventArgs.Empty);
    }

    public async Task UninstallAsync(IPlugin plugin)
    {
        var entry = _installed.FirstOrDefault(x => ReferenceEquals(x.Plugin, plugin));
        if (entry == null)
            return;

        var pluginName = entry.Plugin.Name;
        var pluginFolder = entry.OriginalFolder;

        await UnloadPlugin(entry);

        _logService.Info($"Unloaded plugin: {pluginName}");

        try
        {
            if (Directory.Exists(pluginFolder))
            {
                Directory.Delete(pluginFolder, recursive: true);
                _logService.Info($"Deleted plugin files from {pluginFolder}");
            }

            PluginSettingsCleanup.Delete(pluginName);
            _logService.Info($"Removed saved settings for '{pluginName}'.");

            PluginUninstalled?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to completely remove plugin files or saved settings for '{pluginName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Unloads the installed plugin matching the manifest and returns the original plugin folder
    /// which the caller can then delete/reinstall from.
    /// </summary>
    public async Task<string?> UpdateAsync(PluginManifestEntry manifest)
    {
        // NOTE: you still want to replace Name with Id long-term
        var entry = _installed.FirstOrDefault(x => x.Plugin.Name == manifest.Name);
        if (entry == null)
        {
            _logService.Warn("Plugin to update was not found among installed plugins.");
            return null;
        }

        _logService.Info($"Updating plugin '{entry.Plugin.Name}'");

        if (entry.Plugin is IPersistentPlugin persistentPlugin)
        {
            try
            {
                await persistentPlugin.SaveAsync();
            }
            catch (Exception ex)
            {
                _logService.Warn($"Failed to save plugin state before update: {ex}");
            }
        }

        var pluginFolder = entry.OriginalFolder;

        await UnloadPlugin(entry);

        return pluginFolder;
    }

    public async Task RefreshAvailablePluginsAsync()
    {
        AvailablePlugins.Clear();
        IsFetchingPlugins = true;

        try
        {
            var repos = await _settings.GetAsync<List<PluginRepository>>("PluginRepositories")
                       ??
                       [
                           // default fallback if settings.json has nothing
                           new("Default", "https://raw.githubusercontent.com/shoegazessb/scrubbler-plugins/main/plugins.json")
                       ];

            var all = new List<PluginManifestEntry>();

            using var http = new HttpClient();
            var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            foreach (var repo in repos)
            {
                try
                {
                    var json = await http.GetStringAsync(repo.Url);
                    var docs = JsonSerializer.Deserialize<List<PluginManifestEntry>>(json, serializerOptions);

                    if (docs != null)
                    {
                        all.AddRange(docs);
                        _logService.Info($"Fetched {docs.Count} plugins from repo {repo.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logService.Error($"Failed to fetch plugins from repo {repo.Name}: {ex.Message}");
                }
            }

            AvailablePlugins = all;
        }
        finally
        {
            IsFetchingPlugins = false;
        }
    }

    public async Task SaveAllPluginsAsync()
    {
        foreach (var plugin in InstalledPlugins.OfType<IPersistentPlugin>())
        {
            try
            {
                await plugin.SaveAsync();
            }
            catch (Exception ex)
            {
                // centralize logging instead of crashing
                _logService.Warn($"Failed to save {plugin.Name}: {ex}");
            }
        }
    }

    public void UpdateAccountFunctionsReceiver()
    {
        var container = GetAccountFunctionContainer();

        foreach (var plugin in InstalledPlugins.OfType<IAcceptAccountFunctions>())
        {
            plugin.SetAccountFunctionsContainer(container);
        }
    }

    #endregion Public API

    #region Private load/unload

    private async Task InitializeCoreAsync()
    {
        await LoadPluginsFromDirectory(_rootDir, recursive: true);
        _ = RefreshAvailablePluginsAsync();
        UpdateAccountFunctionsReceiver();
    }

    // Plugin constructors and settings loading may create timers or update bound view models.
    // Dispatch startup discovery and newly installed plugins through the same UI-thread path.
    private Task LoadPluginsFromDirectory(string directory, bool recursive = true) =>
        _dispatcherQueue.EnqueueAsync(() => LoadPluginsFromDirectoryCore(directory, recursive));

    private async Task LoadPluginsFromDirectoryCore(string directory, bool recursive)
    {
        if (!Directory.Exists(directory))
            return;

        var search = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var dll in Directory.EnumerateFiles(directory, "Scrubbler.Plugin.*.dll", search))
        {
            try
            {
                var originalFolder = Path.GetDirectoryName(dll)!;

                // shadow-copy the plugin folder so the original files are always deletable
                var shadowFolder = CreateShadowCopy(originalFolder, _shadowRoot);
                var shadowDll = Path.Combine(shadowFolder, Path.GetFileName(dll));

                var context = new PluginLoadContext(shadowDll);
                var asm = context.LoadFromAssemblyPath(shadowDll);

                var types = SafeGetTypes(asm)
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

                if (!types.Any())
                    _logService.Warn($"No IPlugin implementations found in {dll}");
                else
                {
                    foreach (var pluginType in types)
                    {
                        if (ActivatorUtilities.CreateInstance(_serviceProvider, pluginType) is IPlugin plugin)
                        {
                            _installed.Add(new InstalledPluginEntry(plugin, context, originalFolder, shadowFolder));

                            if (plugin is IPersistentPlugin persistentPlugin)
                                await persistentPlugin.LoadAsync();

                            if (plugin is IAccountPlugin accountPlugin)
                                accountPlugin.IsScrobblingEnabledChanged += AccountPlugin_IsScrobblingEnabledChanged;

                            _logService.Info($"Loaded Plugin: {plugin.Name} v{asm.GetName().Version}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to load plugin from {dll}: {ex.Message}");
            }
        }
    }

    private async Task UnloadPlugin(InstalledPluginEntry entry)
    {
        PluginUnloading?.Invoke(this, entry.Plugin);

        // detach EVERYTHING first
        if (entry.Plugin is IAccountPlugin accountPlugin)
            accountPlugin.IsScrobblingEnabledChanged -= AccountPlugin_IsScrobblingEnabledChanged;

        // dispose while assembly is still valid
        if (entry.Plugin is IDisposable d)
            d.Dispose();

        _installed.Remove(entry);

        entry.Context.Unload();

        // force collection
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Yield();
        }

        // best-effort cleanup of shadow copy
        TryDeleteDirectory(entry.ShadowFolder);
    }

    #endregion Private load/unload

    #region Shadow copy helpers

    private void CleanupShadowRoot()
    {
        try
        {
            if (Directory.Exists(_shadowRoot))
                Directory.Delete(_shadowRoot, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }

        Directory.CreateDirectory(_shadowRoot);
    }

    internal static string CreateShadowCopy(string sourceDir, string shadowRoot)
    {
        var shadowDir = Path.Combine(shadowRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shadowDir);

        // Preserve runtime-specific native assets and other plugin subdirectories.
        // Do not follow links outside the installed plugin folder.
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = System.IO.FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", options))
        {
            var dest = Path.Combine(shadowDir, Path.GetRelativePath(sourceDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        return shadowDir;
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    #endregion Shadow copy helpers

    #region Account functions receiver

    private AccountFunctionContainer GetAccountFunctionContainer()
    {
        var plugin = InstalledPlugins
            .OfType<IAccountPlugin>()
            .FirstOrDefault(p => p.Id == _config.Value.AccountFunctionsPluginID);

        if (plugin == null && _config.Value.AccountFunctionsPluginID != null)
        {
            _logService.Warn($"Configured Account Functions Plugin '{_config.Value.AccountFunctionsPluginID}' not found. Reverting to default.");
            _config.UpdateAsync(current =>
            {
                var updated = current with
                {
                    AccountFunctionsPluginID = null
                };

                return updated;
            });
        }

        return new AccountFunctionContainer(plugin);
    }

    private void AccountPlugin_IsScrobblingEnabledChanged(object? sender, EventArgs e)
    {
        IsAnyAccountPluginScrobblingChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion Account functions receiver

    #region Reflection helper

    private Type[] SafeGetTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var msgs = ex.LoaderExceptions
                .Where(e => e != null)
                .Select(e => e!.Message);

            _logService.Error($"Some types in {asm.FullName} could not be loaded: {string.Join("; ", msgs)}");

            return ex.Types.Where(t => t != null).ToArray()!;
        }
    }

    #endregion Reflection helper

    #region Nested entry type

    private sealed class InstalledPluginEntry(IPlugin plugin, PluginLoadContext context, string originalFolder, string shadowFolder)
    {
        public IPlugin Plugin { get; } = plugin;
        public PluginLoadContext Context { get; } = context;
        public string OriginalFolder { get; } = originalFolder;
        public string ShadowFolder { get; } = shadowFolder;
    }

    #endregion Nested entry type
}
