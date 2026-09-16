using Scrubbler.Host.Services;

namespace Scrubbler.Test.Services;

public class PluginShadowCopyTests
{
    [Test]
    public void Shadow_copy_preserves_native_asset_paths_and_source_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "scrubbler-shadow-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "plugin");
        var nativePath = Path.Combine("runtimes", "win-x64", "native", "e_sqlite3.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(source, nativePath))!);
        try
        {
            File.WriteAllText(Path.Combine(source, "plugin.dll"), "managed");
            File.WriteAllText(Path.Combine(source, nativePath), "native");
            var shadow = PluginManager.CreateShadowCopy(source, Path.Combine(root, "shadow"));
            Assert.That(File.ReadAllText(Path.Combine(shadow, "plugin.dll")), Is.EqualTo("managed"));
            Assert.That(File.ReadAllText(Path.Combine(shadow, nativePath)), Is.EqualTo("native"));
            Assert.That(File.ReadAllText(Path.Combine(source, nativePath)), Is.EqualTo("native"));
        }
        finally { Directory.Delete(root, true); }
    }
}
