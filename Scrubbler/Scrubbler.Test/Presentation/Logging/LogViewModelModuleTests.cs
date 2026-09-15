using System.Collections.Specialized;
using Microsoft.Extensions.Logging;
using Moq;
using Scrubbler.Host.Presentation.Logging;
using Scrubbler.Host.Services;
using Scrubbler.Host.Services.Logging;
using Scrubbler.PluginBase.Services;

namespace Scrubbler.Test.Presentation.Logging;

public partial class LogViewModelTests
{
    [TestCase("All", 4)]
    [TestCase("iTunes Scrobbler", 2)]
    public void IncomingLogs_PreserveModuleSelectionAndVisibleEntries(string selection, int expectedCount)
    {
        var service = new HostLogService();
        var vm = new LogViewModel(service, Mock.Of<IUserFeedbackService>(),
            Mock.Of<IFilePickerService>(), Mock.Of<IFileStorageService>());
        service.Write(LogLevel.Information, "iTunes Scrobbler", "Playing");
        vm.SelectedModule = selection;
        var changes = new List<NotifyCollectionChangedAction>();
        vm.Modules.CollectionChanged += (_, e) =>
        {
            changes.Add(e.Action);
            // Model a selector clearing its two-way binding when its item disappears.
            if (!vm.Modules.Contains(vm.SelectedModule))
                vm.SelectedModule = null!;
        };

        service.Write(LogLevel.Information, "iTunes Scrobbler", "Updating now playing");
        Assert.That(changes, Is.Empty, "Existing modules must not mutate the dropdown.");
        service.Write(LogLevel.Information, "Apple Music Scrobbler", "Connected");
        service.Write(LogLevel.Information, "Zebra", "Connected");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(vm.SelectedModule, Is.EqualTo(selection));
            Assert.That(vm.FilteredEntries, Has.Count.EqualTo(expectedCount));
            Assert.That(vm.Modules, Is.EqualTo(new[] { "All", "Apple Music Scrobbler", "iTunes Scrobbler", "Zebra" }));
            Assert.That(changes, Is.EqualTo(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Add }));
        }
    }

    [Test]
    public void ClearLogs_ResetsModuleFilterAndAcceptsNewLogs()
    {
        var service = new HostLogService();
        var vm = new LogViewModel(service, Mock.Of<IUserFeedbackService>(),
            Mock.Of<IFilePickerService>(), Mock.Of<IFileStorageService>());
        service.Write(LogLevel.Information, "iTunes Scrobbler", "Playing");
        vm.SelectedModule = "iTunes Scrobbler";

        vm.ClearCommand.Execute(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(vm.SelectedModule, Is.EqualTo("All"));
            Assert.That(vm.Modules, Is.EqualTo(new[] { "All" }));
            Assert.That(vm.Entries, Is.Empty);
            Assert.That(vm.FilteredEntries, Is.Empty);
        }

        service.Write(LogLevel.Information, "iTunes Scrobbler", "Playing again");
        Assert.That(vm.FilteredEntries, Has.Count.EqualTo(1));
        Assert.That(vm.SelectedModule, Is.EqualTo("All"));
    }
}
