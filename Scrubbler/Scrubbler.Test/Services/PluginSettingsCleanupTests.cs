using Scrubbler.Host.Helper;

namespace Scrubbler.Test.Services;

public class PluginSettingsCleanupTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "scrubbler-settings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Deletes_selected_plugins_settings_and_credentials_only()
    {
        var selected = Path.Combine(_root, "Scrubbler", "Plugins", "Last.fm");
        var other = Path.Combine(_root, "Scrubbler", "Plugins", "ListenBrainz");
        Directory.CreateDirectory(selected);
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(selected, "settings.json"), "preferences");
        File.WriteAllText(Path.Combine(selected, "settings.dat"), "credentials");
        File.WriteAllText(Path.Combine(other, "settings.dat"), "other credentials");
        File.WriteAllText(Path.Combine(_root, "Scrubbler", "settings.json"), "host preferences");

        PluginSettingsCleanup.Delete("Last.fm", _root);

        Assert.That(Directory.Exists(selected), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(other, "settings.dat")), Is.EqualTo("other credentials"));
        Assert.That(File.ReadAllText(Path.Combine(_root, "Scrubbler", "settings.json")), Is.EqualTo("host preferences"));
    }

    [Test]
    public void Missing_settings_are_a_noop()
    {
        Assert.DoesNotThrow(() => PluginSettingsCleanup.Delete("Manual Scrobbler", _root));
        Assert.That(Directory.GetFileSystemEntries(_root), Is.Empty);
    }

    [TestCase("")]
    [TestCase("..")]
    [TestCase(".")]
    [TestCase("../other")]
    [TestCase("..\\other")]
    [TestCase("/outside")]
    [TestCase("Last.fm.")]
    public void Rejects_names_that_could_escape_or_alias_the_settings_directory(string name)
    {
        Assert.Throws<ArgumentException>(() => PluginSettingsCleanup.Delete(name, _root));
    }
}
