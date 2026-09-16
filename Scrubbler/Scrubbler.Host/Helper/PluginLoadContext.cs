using System.Reflection;
using System.Runtime.Loader;

namespace Scrubbler.Host.Helper;

internal class PluginLoadContext(string mainAssemblyPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // if already loaded in default, reuse it
        var existing = Default.Assemblies
            .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
            return null;

        // otherwise resolve from plugin folder
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path != null)
            return LoadFromAssemblyPath(path);

        return null;
    }

}
