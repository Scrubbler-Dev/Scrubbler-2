using Microsoft.Extensions.Logging;
using Moq;
using Scrubbler.Host.Models;
using Scrubbler.Host.Presentation.Logging;
using Scrubbler.Host.Presentation.Navigation;
using Scrubbler.Host.Services;
using Scrubbler.Host.Services.Logging;
using Scrubbler.PluginBase.Services;

namespace Scrubbler.Test.Presentation.Logging;

/// <summary>
/// Tests for Scrubbler.Host.Presentation.Logging.LogViewModel.
/// Focused exclusively on StartAsync behavior.
/// </summary>
public partial class LogViewModelTests
{
    /// <summary>
    /// Verifies that constructing LogViewModel initializes LogLevelFilters for every LogLevel,
    /// creates a Modules list that initially contains only the 'All' sentinel, and that Entries/FilteredEntries start empty.
    /// </summary>
    [Test]
    public void LogViewModel_Constructor_InitializesFiltersModulesAndCollections()
    {
        // Arrange
        var hostLogService = new HostLogService();
        var userFeedbackMock = new Mock<IUserFeedbackService>(MockBehavior.Strict);
        var filePickerMock = new Mock<IFilePickerService>(MockBehavior.Strict);
        var fileStorageMock = new Mock<IFileStorageService>(MockBehavior.Strict);

        // Act
        var vm = new LogViewModel(hostLogService, userFeedbackMock.Object, filePickerMock.Object, fileStorageMock.Object);

        // Assert
        var expectedLevels = Enum.GetValues<LogLevel>().Length;
        Assert.That(vm.LogLevelFilters, Is.Not.Null, "LogLevelFilters collection should be created.");
        Assert.That(vm.LogLevelFilters, Has.Count.EqualTo(expectedLevels), "There should be one LogLevelFilterViewModel per LogLevel value.");
        // Ensure each enum value has a corresponding filter
        var levelsInFilters = vm.LogLevelFilters.Select(f => f.Level).OrderBy(l => (int)l).ToArray();
        var expectedLevelsArray = Enum.GetValues<LogLevel>().OrderBy(l => (int)l).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(levelsInFilters, Is.EqualTo(expectedLevelsArray), "LogLevelFilters should contain all LogLevel values.");

            // Modules should contain only the ALL sentinel initially
            Assert.That(vm.Modules, Is.Not.Null);
        }

        Assert.That(vm.Modules, Has.Count.EqualTo(1), "Modules should contain only the 'All' sentinel after construction when no entries exist.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(vm.Modules.First(), Is.EqualTo("All"));

            // Entries and FilteredEntries should be empty
            Assert.That(vm.Entries, Is.Not.Null);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(vm.Entries, Is.Empty);
            Assert.That(vm.FilteredEntries, Is.Not.Null);
        }
        Assert.That(vm.FilteredEntries, Is.Empty);
    }

    /// <summary>
    /// Verifies that the constructor wires the HostLogService.MessageLogged event to the viewmodel's Add method:
    /// - When a message is written via HostLogService.Write it appears in Entries and (when filters allow) in FilteredEntries.
    /// - Toggling the corresponding LogLevelFilterViewModel.IsEnabled raises RebuildFilteredEntries and removes/keeps entries accordingly.
    /// - The IUserFeedbackService methods are invoked for Warning/Error/Critical message levels as implemented by Add.
    /// </summary>
    /// <param name="level">The LogLevel to test (Information, Warning, Error, Critical).</param>
    [TestCase(LogLevel.Information)]
    [TestCase(LogLevel.Warning)]
    [TestCase(LogLevel.Error)]
    [TestCase(LogLevel.Critical)]
    public void LogViewModel_Constructor_SubscribesToHostLogServiceAndRespondsToMessages(LogLevel level)
    {
        // Arrange
        var hostLogService = new HostLogService();
        var userFeedbackMock = new Mock<IUserFeedbackService>(MockBehavior.Strict);
        var filePickerMock = new Mock<IFilePickerService>(MockBehavior.Strict);
        var fileStorageMock = new Mock<IFileStorageService>(MockBehavior.Strict);

        // Expectation setup for feedback calls: only Warning => ShowWarning; Error/Critical => ShowError; others => none.
        switch (level)
        {
            case LogLevel.Warning:
                userFeedbackMock.Setup(u => u.ShowWarning(It.IsAny<string>(), It.IsAny<TimeSpan?>()));
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                // For error/critical we expect ShowError with message that includes the exception message
                userFeedbackMock.Setup(u => u.ShowError(It.Is<string>(s => s.Contains("ExMsg")), It.IsAny<TimeSpan?>()));
                break;
            default:
                // No calls expected; leave strict mock to throw if any unexpected call happens.
                break;
        }

        var vm = new LogViewModel(hostLogService, userFeedbackMock.Object, filePickerMock.Object, fileStorageMock.Object);

        // Act - write a log entry through the HostLogService (constructor should have subscribed)
        Exception? ex = (level == LogLevel.Error || level == LogLevel.Critical) ? new Exception("ExMsg") : null;
        hostLogService.Write(level, "ModuleA", "This is a message", ex);

        // Assert Entries and FilteredEntries updated based on current filters (all enabled by default)
        Assert.That(vm.Entries, Has.Count.EqualTo(1), "Entries should contain the message written via HostLogService.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(vm.Entries[0].Level, Is.EqualTo(level));
            Assert.That(vm.Entries[0].Module, Is.EqualTo("ModuleA"));

            // Since filters default to enabled, and SelectedModule is 'All' and SearchText empty, the entry should be in FilteredEntries
            Assert.That(vm.FilteredEntries, Has.Count.EqualTo(1), "FilteredEntries should include the entry when filters allow it.");

            // Verify modules list updated to include ModuleA (and 'All' first)
            Assert.That(vm.Modules, Has.Count.GreaterThanOrEqualTo(2), "Modules should include the 'All' sentinel and the added module.");
        }
        Assert.That(vm.Modules, Does.Contain("ModuleA"));

        // Find the corresponding LogLevelFilterViewModel and disable it, which should trigger RebuildFilteredEntries (via PropertyChanged handler wired in ctor)
        var filterVm = vm.LogLevelFilters.FirstOrDefault(f => f.Level == level);
        Assert.That(filterVm, Is.Not.Null, $"Filter for level {level} should exist.");

        // Act - disable the filter
        filterVm.IsEnabled = false;

        // Assert - the entry should be removed from FilteredEntries
        Assert.That(vm.FilteredEntries, Is.Empty, "FilteredEntries should be empty after disabling the relevant level filter.");

        // Verify expected user feedback calls
        switch (level)
        {
            case LogLevel.Warning:
                userFeedbackMock.Verify(u => u.ShowWarning(It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Once);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                userFeedbackMock.Verify(u => u.ShowError(It.Is<string>(s => s.Contains("ExMsg")), It.IsAny<TimeSpan?>()), Times.Once);
                break;
            default:
                // Ensure no other feedback methods were called
                userFeedbackMock.VerifyNoOtherCalls();
                break;
        }
    }

    /// <summary>
    /// Verifies that CanSave returns false when the FilteredEntries collection is empty.
    /// Input condition: a newly constructed LogViewModel with no filtered entries.
    /// Expected result: CanSave is false.
    /// </summary>
    [Test]
    public void CanSave_EmptyFilteredEntries_ReturnsFalse()
    {
        // Arrange
        var hostLogService = new HostLogService();
        var userFeedback = new Mock<IUserFeedbackService>();
        var filePicker = new Mock<IFilePickerService>();
        var fileStorage = new Mock<IFileStorageService>();

        var vm = new LogViewModel(hostLogService, userFeedback.Object, filePicker.Object, fileStorage.Object);

        // Sanity: ensure FilteredEntries is empty
        Assert.That(vm.FilteredEntries, Is.Not.Null);
        Assert.That(vm.FilteredEntries, Is.Empty);

        // Act
        bool canSave = vm.CanSave;

        // Assert
        Assert.That(canSave, Is.False);
    }

    /// <summary>
    /// Verifies that CanSave returns true when the FilteredEntries collection contains one or more items.
    /// Input conditions: add N items into FilteredEntries (N provided by test cases).
    /// Expected result: CanSave is true for N &gt; 0.
    /// </summary>
    /// <param name="itemCount">Number of items to add to FilteredEntries.</param>
    [TestCase(1)]
    [TestCase(3)]
    public void CanSave_FilteredEntriesWithItems_ReturnsTrue(int itemCount)
    {
        // Arrange
        var hostLogService = new HostLogService();
        var userFeedback = new Mock<IUserFeedbackService>();
        var filePicker = new Mock<IFilePickerService>();
        var fileStorage = new Mock<IFileStorageService>();

        var vm = new LogViewModel(hostLogService, userFeedback.Object, filePicker.Object, fileStorage.Object);

        // Act - add items
        for (int i = 0; i < itemCount; i++)
        {
            var msg = new LogMessage(DateTime.UtcNow, LogLevel.Information, $"Module{i}", $"Message {i}");
            vm.FilteredEntries.Add(msg);
        }

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(vm.FilteredEntries, Has.Count.EqualTo(itemCount));
            Assert.That(vm.CanSave, Is.True);
        }
    }

    /// <summary>
    /// Verifies that CanSave updates to false after previously having items when FilteredEntries is cleared.
    /// Input condition: add one item, then clear the collection.
    /// Expected result: CanSave becomes false after clearing.
    /// </summary>
    [Test]
    public void CanSave_AfterClearing_ReturnsFalse()
    {
        // Arrange
        var hostLogService = new HostLogService();
        var userFeedback = new Mock<IUserFeedbackService>();
        var filePicker = new Mock<IFilePickerService>();
        var fileStorage = new Mock<IFileStorageService>();

        var vm = new LogViewModel(hostLogService, userFeedback.Object, filePicker.Object, fileStorage.Object);

        var msg = new LogMessage(DateTime.UtcNow, LogLevel.Warning, "TestModule", "Test message");
        vm.FilteredEntries.Add(msg);

        using (Assert.EnterMultipleScope())
        {
            // Precondition
            Assert.That(vm.FilteredEntries, Is.Not.Empty);
            Assert.That(vm.CanSave, Is.True);
        }

        // Act
        vm.FilteredEntries.Clear();

        using (Assert.EnterMultipleScope())
        {
            // Assert
            Assert.That(vm.FilteredEntries, Is.Empty);
            Assert.That(vm.CanSave, Is.False);
        }
    }

    [TestCase(LogLevel.Error, 2, 0)]
    [TestCase(LogLevel.Critical, 2, 0)]
    [TestCase(LogLevel.Warning, 0, 2)]
    [TestCase(LogLevel.Information, 0, 0)]
    public void Sidebar_CreatedAfterStartupMessages_ShowsUnreadCounts(LogLevel level, int errors, int warnings)
    {
        var hostLogService = new HostLogService();
        var log = new LogViewModel(hostLogService, Mock.Of<IUserFeedbackService>(),
            Mock.Of<IFilePickerService>(), Mock.Of<IFileStorageService>());

        hostLogService.Write(level, "Startup", "First startup message");
        hostLogService.Write(level, "Startup", "Second startup message");

        var sidebarItem = new MenuNavigationItemViewModel("Logs", null, log);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sidebarItem.Errors, Is.EqualTo(errors));
            Assert.That(sidebarItem.Warnings, Is.EqualTo(warnings));
            Assert.That(sidebarItem.HasErrors, Is.EqualTo(errors > 0));
            Assert.That(sidebarItem.HasWarnings, Is.EqualTo(warnings > 0));
            Assert.That(sidebarItem.Infos, Is.Zero);
        }
    }

    [Test]
    public void Sidebar_OpeningLogs_ClearsStartupCountsAndTracksNewUnreadMessages()
    {
        var hostLogService = new HostLogService();
        var log = new LogViewModel(hostLogService, Mock.Of<IUserFeedbackService>(),
            Mock.Of<IFilePickerService>(), Mock.Of<IFileStorageService>());

        hostLogService.Write(LogLevel.Error, "Startup", "Startup error");
        hostLogService.Write(LogLevel.Warning, "Startup", "Startup warning");
        var sidebarItem = new MenuNavigationItemViewModel("Logs", null, log);
        Assert.That(sidebarItem.Errors, Is.EqualTo(1));
        Assert.That(sidebarItem.Warnings, Is.EqualTo(1));

        sidebarItem.IsSelected = true;
        hostLogService.Write(LogLevel.Error, "Startup", "Error while viewing logs");
        Assert.That(sidebarItem.HasErrors, Is.False);
        Assert.That(sidebarItem.HasWarnings, Is.False);

        sidebarItem.IsSelected = false;
        var recreatedItem = new MenuNavigationItemViewModel("Logs", null, log);
        Assert.That(recreatedItem.HasErrors, Is.False);
        Assert.That(recreatedItem.HasWarnings, Is.False);

        hostLogService.Write(LogLevel.Warning, "Runtime", "New unread warning");
        Assert.That(sidebarItem.Warnings, Is.EqualTo(1));
        Assert.That(recreatedItem.Warnings, Is.EqualTo(1));
        Assert.That(recreatedItem.Errors, Is.Zero);
    }

    [Test]
    public void NavigationStatusInfo_RaisesEventsWhenNotSelected()
    {
        // Arrange
        var hostLogService = new HostLogService();
        var userFeedbackMock = new Mock<IUserFeedbackService>(MockBehavior.Strict);
        var filePickerMock = new Mock<IFilePickerService>(MockBehavior.Strict);
        var fileStorageMock = new Mock<IFileStorageService>(MockBehavior.Strict);

        // Allow expected feedback calls
        userFeedbackMock.Setup(u => u.ShowWarning(It.IsAny<string>(), It.IsAny<TimeSpan?>()));
        userFeedbackMock.Setup(u => u.ShowError(It.Is<string>(s => s.Contains("ExMsg")), It.IsAny<TimeSpan?>()));

        var vm = new LogViewModel(hostLogService, userFeedbackMock.Object, filePickerMock.Object, fileStorageMock.Object);

        var events = new List<NavigationStatusEventArgs>();
        vm.NavigationStatusChanged += (s, e) => events.Add(e);

        // Act - produce a warning and an error while not selected
        hostLogService.Write(LogLevel.Warning, "ModuleA", "Warning1");
        hostLogService.Write(LogLevel.Error, "ModuleA", "Error1", new Exception("ExMsg"));

        // Assert - two events raised with accumulated counts
        Assert.That(events, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(events[0].Warnings, Is.EqualTo(1));
            Assert.That(events[0].Errors, Is.Zero);
            Assert.That(events[1].Warnings, Is.EqualTo(1));
            Assert.That(events[1].Errors, Is.EqualTo(1));
        }

        userFeedbackMock.Verify(u => u.ShowWarning(It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Once);
        userFeedbackMock.Verify(u => u.ShowError(It.Is<string>(s => s.Contains("ExMsg")), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Test]
    public void NavigationStatusInfo_ResetsWhenSelectedAndSuppressesFurtherEvents()
    {
        // Arrange
        var hostLogService = new HostLogService();
        var userFeedbackMock = new Mock<IUserFeedbackService>(MockBehavior.Strict);
        var filePickerMock = new Mock<IFilePickerService>(MockBehavior.Strict);
        var fileStorageMock = new Mock<IFileStorageService>(MockBehavior.Strict);

        // Allow expected feedback calls
        userFeedbackMock.Setup(u => u.ShowWarning(It.IsAny<string>(), It.IsAny<TimeSpan?>()));
        userFeedbackMock.Setup(u => u.ShowError(It.Is<string>(s => s.Contains("ExMsg")), It.IsAny<TimeSpan?>()));

        var vm = new LogViewModel(hostLogService, userFeedbackMock.Object, filePickerMock.Object, fileStorageMock.Object);

        var events = new List<NavigationStatusEventArgs>();
        vm.NavigationStatusChanged += (s, e) => events.Add(e);

        // Act - increment counts while not selected
        hostLogService.Write(LogLevel.Warning, "ModuleA", "Warn1");
        hostLogService.Write(LogLevel.Error, "ModuleA", "Err1", new Exception("ExMsg"));

        Assert.That(events, Has.Count.EqualTo(2));

        // Act - select the viewmodel which should reset counts and raise a zeroed event
        vm.IsSelected = true;

        Assert.That(events, Has.Count.EqualTo(3));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(events[2].Errors, Is.Zero);
            Assert.That(events[2].Warnings, Is.Zero);
        }

        // Act - produce another warning while selected; should not raise NavigationStatusChanged
        hostLogService.Write(LogLevel.Warning, "ModuleA", "Warn2");

        Assert.That(events, Has.Count.EqualTo(3));

        // Verify feedback calls: two warnings and one error
        userFeedbackMock.Verify(u => u.ShowWarning(It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Exactly(2));
        userFeedbackMock.Verify(u => u.ShowError(It.Is<string>(s => s.Contains("ExMsg")), It.IsAny<TimeSpan?>()), Times.Once);
    }
}
