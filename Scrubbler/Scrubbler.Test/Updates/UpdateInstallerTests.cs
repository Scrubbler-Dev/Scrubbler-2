using System.IO.Compression;
using Scrubbler.Updater;

namespace Scrubbler.Test.Updates;

[TestFixture]
public class UpdateInstallerTests
{
    private string _root = null!;
    private string _appDir = null!;
    private string _package = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"scrubbler_update_test_{Guid.NewGuid():N}");
        _appDir = Path.Combine(_root, "Scrubbler");
        _package = Path.Combine(_root, "update.zip");
        Directory.CreateDirectory(_appDir);
        File.WriteAllText(Path.Combine(_appDir, "app.dll"), "old app");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Install_PreservesInstalledPluginsAfterBackupIsDeleted()
    {
        WriteInstalledFile("Plugins/Example/plugin.dll", "installed plugin");
        WriteInstalledFile("Plugins/Example/native/dependency.dll", "native dependency");
        WriteInstalledFile("Plugins/Example/settings.json", "local settings");
        Directory.CreateDirectory(Path.Combine(_appDir, "Plugins", "Example", "empty"));
        WriteInstalledFile("obsolete.dll", "old app dependency");
        CreatePackage(("app.dll", "new app"), ("Updater/updater.dll", "new updater"));

        var backup = UpdateInstaller.Install(_appDir, _package);
        Directory.Delete(backup, recursive: true);

        Assert.Multiple(() =>
        {
            Assert.That(ReadInstalledFile("app.dll"), Is.EqualTo("new app"));
            Assert.That(ReadInstalledFile("Updater/updater.dll"), Is.EqualTo("new updater"));
            Assert.That(ReadInstalledFile("Plugins/Example/plugin.dll"), Is.EqualTo("installed plugin"));
            Assert.That(ReadInstalledFile("Plugins/Example/native/dependency.dll"), Is.EqualTo("native dependency"));
            Assert.That(ReadInstalledFile("Plugins/Example/settings.json"), Is.EqualTo("local settings"));
            Assert.That(Directory.Exists(Path.Combine(_appDir, "Plugins", "Example", "empty")), Is.True);
            Assert.That(File.Exists(Path.Combine(_appDir, "obsolete.dll")), Is.False);
        });
    }

    [Test]
    public void Install_KeepsInstalledPluginWhenPackageContainsTheSameFile()
    {
        WriteInstalledFile("Plugins/Example/plugin.dll", "installed plugin");
        CreatePackage(("app.dll", "new app"), ("Plugins/Example/plugin.dll", "bundled plugin"));

        UpdateInstaller.Install(_appDir, _package);

        Assert.That(ReadInstalledFile("Plugins/Example/plugin.dll"), Is.EqualTo("installed plugin"));
    }

    [Test]
    public void Install_WorksWithoutInstalledPlugins()
    {
        CreatePackage(("app.dll", "new app"));

        UpdateInstaller.Install(_appDir, _package);

        Assert.That(ReadInstalledFile("app.dll"), Is.EqualTo("new app"));
    }

    [Test]
    public void Install_WhenPluginCopyFails_LeavesOriginalInstallationIntact()
    {
        WriteInstalledFile("Plugins/Example/plugin.dll", "installed plugin");
        // A file at the destination prevents creating the plugin directory.
        CreatePackage(("app.dll", "new app"), ("Plugins", "not a directory"));

        Assert.Throws<IOException>(() => UpdateInstaller.Install(_appDir, _package));

        Assert.Multiple(() =>
        {
            Assert.That(ReadInstalledFile("app.dll"), Is.EqualTo("old app"));
            Assert.That(ReadInstalledFile("Plugins/Example/plugin.dll"), Is.EqualTo("installed plugin"));
            Assert.That(Directory.GetDirectories(_root), Is.EquivalentTo(new[] { _appDir }));
        });
    }

    [Test]
    public void Install_WhenPackageIsInvalid_LeavesOriginalInstallationIntact()
    {
        WriteInstalledFile("Plugins/Example/plugin.dll", "installed plugin");
        File.WriteAllText(_package, "not a ZIP");

        Assert.Throws<InvalidDataException>(() => UpdateInstaller.Install(_appDir, _package));

        Assert.Multiple(() =>
        {
            Assert.That(ReadInstalledFile("app.dll"), Is.EqualTo("old app"));
            Assert.That(ReadInstalledFile("Plugins/Example/plugin.dll"), Is.EqualTo("installed plugin"));
        });
    }

    private void CreatePackage(params (string Path, string Contents)[] files)
    {
        using var archive = ZipFile.Open(_package, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            using var writer = new StreamWriter(archive.CreateEntry(file.Path).Open());
            writer.Write(file.Contents);
        }
    }

    private void WriteInstalledFile(string relativePath, string contents)
    {
        var path = Path.Combine(_appDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private string ReadInstalledFile(string relativePath) => File.ReadAllText(Path.Combine(_appDir, relativePath));
}
