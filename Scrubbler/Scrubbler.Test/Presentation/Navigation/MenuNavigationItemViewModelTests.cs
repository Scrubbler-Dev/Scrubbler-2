using Moq;
using Scrubbler.Host.Presentation.Navigation;

namespace Scrubbler.Test.Presentation.Navigation;

/// <summary>
/// Tests for MenuNavigationItemViewModel constructor behavior.
/// </summary>
public partial class MenuNavigationItemViewModelTests
{
    /// <summary>
    /// Verifies that when the content implements INavigationStatusInfo, the constructor
    /// subscribes to the NavigationStatusChanged event.
    /// Input: a mock INavigationStatusInfo.
    /// Expected: the constructor adds an event handler to NavigationStatusChanged (i.e., the add accessor is invoked).
    /// </summary>
    [Test]
    public void MenuNavigationItemViewModel_Constructor_WithINavigationStatusInfo_SubscribesToNavigationStatusChanged()
    {
        // Arrange
        var mockNavInfo = new Mock<INavigationStatusInfo>(MockBehavior.Strict);
        var subscribed = false;
        mockNavInfo.SetupGet(m => m.NavigationStatus).Returns(new NavigationStatusEventArgs(2, 1, 3));

        // Capture subscription via SetupAdd to ensure the constructor wires the event handler.
        mockNavInfo
            .SetupAdd(m => m.NavigationStatusChanged += It.IsAny<EventHandler<NavigationStatusEventArgs>>())
            .Callback<EventHandler<NavigationStatusEventArgs>>(h => subscribed = true);

        // Act
        var vm = new MenuNavigationItemViewModel("Title", icon: null, content: mockNavInfo.Object);

        // Assert
        Assert.That(subscribed, Is.True, "Constructor should subscribe to NavigationStatusChanged when content implements INavigationStatusInfo.");
        Assert.That(vm.Errors, Is.EqualTo(2));
        Assert.That(vm.Warnings, Is.EqualTo(1));
        Assert.That(vm.Infos, Is.EqualTo(3));
    }

    /// <summary>
    /// Verifies that when Content implements INavigationStatusInfo:
    /// - Setting IsSelected to a different value updates the underlying content's IsSelected property.
    /// - PropertyChanged for "IsSelected" is raised exactly once for the change.
    /// - Setting IsSelected to the same value again does not raise PropertyChanged or update the content again.
    /// Input: initial state (default false), set to true, then set to true again.
    /// Expected: content.IsSelected set once; PropertyChanged raised once; no additional calls on second assignment.
    /// </summary>
    [Test]
    public void IsSelected_WithContentImplementingINavigationStatusInfo_UpdatesContentAndDoesNotNotifyOnSameValue()
    {
        // Arrange
        var mockStatus = new Mock<INavigationStatusInfo>();
        // Allow property set/get tracking to be verifiable
        mockStatus.SetupAllProperties();
        mockStatus.SetupGet(m => m.NavigationStatus).Returns(new NavigationStatusEventArgs(0, 0, 0));

        var vm = new MenuNavigationItemViewModel("title", null, mockStatus.Object);

        int propertyChangedCount = 0;
        string? lastPropertyName = null;
        vm.PropertyChanged += (s, e) =>
        {
            propertyChangedCount++;
            lastPropertyName = e.PropertyName;
        };

        // Pre-assert initial state
        Assert.That(vm.IsSelected, Is.False, "Initial IsSelected should be false by default.");

        // Act - change to true
        vm.IsSelected = true;

        // Assert - change applied and content updated
        Assert.That(vm.IsSelected, Is.True, "IsSelected should reflect the newly assigned true value.");
        mockStatus.VerifySet(m => m.IsSelected = true, Times.Once, "Content.IsSelected should be set once to true.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(propertyChangedCount, Is.EqualTo(1), "PropertyChanged should be raised exactly once for the change.");
            Assert.That(lastPropertyName, Is.EqualTo(nameof(vm.IsSelected)), "PropertyChanged should report the IsSelected property name.");
        }

        // Act - assign same value again
        vm.IsSelected = true;

        // Assert - no additional notifications or content updates
        mockStatus.VerifySet(m => m.IsSelected = true, Times.Once, "Content.IsSelected should not be set again when assigning the same value.");
        Assert.That(propertyChangedCount, Is.EqualTo(1), "PropertyChanged should not be raised again when setting the same value.");
    }

    /// <summary>
    /// Verifies that when Content does NOT implement INavigationStatusInfo (e.g., null or arbitrary object):
    /// - Setting IsSelected to a different value changes the view-model's IsSelected.
    /// - PropertyChanged for "IsSelected" is raised exactly once.
    /// - No interaction with content occurs (content is not casted or modified).
    /// Input: initial state (default false), set to true, then set to true again.
    /// Expected: IsSelected updated; PropertyChanged raised once; subsequent identical assignment does nothing.
    /// </summary>
    [Test]
    public void IsSelected_WithNonMatchingContent_RaisesPropertyChangedAndDoesNotInteractWithContent_OnSameValueNoOp()
    {
        // Arrange
        var arbitraryContent = new object();
        var vm = new MenuNavigationItemViewModel("title", null, arbitraryContent);

        int propertyChangedCount = 0;
        string? lastPropertyName = null;
        vm.PropertyChanged += (s, e) =>
        {
            propertyChangedCount++;
            lastPropertyName = e.PropertyName;
        };

        // Pre-assert initial state
        Assert.That(vm.IsSelected, Is.False, "Initial IsSelected should be false by default.");

        // Act - change to true
        vm.IsSelected = true;

        using (Assert.EnterMultipleScope())
        {
            // Assert - change applied and PropertyChanged raised
            Assert.That(vm.IsSelected, Is.True, "IsSelected should reflect the newly assigned true value.");
            Assert.That(propertyChangedCount, Is.EqualTo(1), "PropertyChanged should be raised exactly once for the change.");
            Assert.That(lastPropertyName, Is.EqualTo(nameof(vm.IsSelected)), "PropertyChanged should report the IsSelected property name.");
        }

        // Act - assign same value again
        vm.IsSelected = true;

        // Assert - no additional notifications
        Assert.That(propertyChangedCount, Is.EqualTo(1), "PropertyChanged should not be raised again when setting the same value.");
    }

    /// <summary>
    /// Verifies HasChildren returns false when the Children collection is empty and true when it contains items.
    /// Tests multiple counts to cover boundary behavior (0, 1, >1).
    /// </summary>
    /// <param name="initialCount">Initial number of children to add before checking HasChildren.</param>
    /// <param name="expectedHasChildren">Expected HasChildren result for the given initialCount.</param>
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(5, true)]
    public void HasChildren_VariousChildCounts_ReportsExpected(int initialCount, bool expectedHasChildren)
    {
        // Arrange
        // Title must be a non-null string per constructor; icon and content can be null.
        var vm = new MenuNavigationItemViewModel("title", icon: null, content: null);

        // Ensure starting state empty
        Assert.That(vm.Children, Is.Not.Null, "Children collection should be initialized by constructor.");

        // Add the requested number of children using mocks of NavigationItemViewModelBase
        for (int i = 0; i < initialCount; i++)
        {
            var mockChild = new Mock<NavigationItemViewModelBase>("child" + i, null!);
            vm.Children.Add(mockChild.Object);
        }

        // Act
        bool actual = vm.HasChildren;

        // Assert
        Assert.That(actual, Is.EqualTo(expectedHasChildren), $"HasChildren should be {expectedHasChildren} when Children has {initialCount} items.");
    }

    /// <summary>
    /// Ensures HasChildren updates when items are added and removed from the Children collection.
    /// Scenario: start empty -> add one item -> remove that item -> expect HasChildren to flip accordingly.
    /// </summary>
    [Test]
    public void HasChildren_AddThenRemove_ReflectsCollectionChanges()
    {
        // Arrange
        var vm = new MenuNavigationItemViewModel("title", icon: null, content: null);
        var mockChild = new Mock<NavigationItemViewModelBase>("child", null!);

        using (Assert.EnterMultipleScope())
        {
            // Precondition
            Assert.That(vm.Children, Is.Empty);
            Assert.That(vm.HasChildren, Is.False);
        }

        // Act - add
        vm.Children.Add(mockChild.Object);

        using (Assert.EnterMultipleScope())
        {
            // Assert - after adding
            Assert.That(vm.Children, Has.Count.EqualTo(1));
            Assert.That(vm.HasChildren, Is.True);
        }

        // Act - remove
        bool removed = vm.Children.Remove(mockChild.Object);

        using (Assert.EnterMultipleScope())
        {
            // Assert - after removing
            Assert.That(removed, Is.True, "Expected the previously added child to be removed successfully.");
            Assert.That(vm.Children, Is.Empty);
            Assert.That(vm.HasChildren, Is.False);
        }
    }

    /// <summary>
    /// Verifies HasInfos reflects whether Infos is greater than zero.
    /// Input conditions: various integer values for Infos including int.MinValue, -1, 0, 1 and int.MaxValue.
    /// Expected result: HasInfos == (Infos &gt; 0).
    /// </summary>
    /// <param name="infosValue">The value to assign to Infos.</param>
    /// <param name="expected">Expected HasInfos result.</param>
    [TestCase(int.MinValue, false)]
    [TestCase(-1, false)]
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(int.MaxValue, true)]
    public void HasInfos_SetInfosValue_ReturnsExpected(int infosValue, bool expected)
    {
        // Arrange
        var vm = new MenuNavigationItemViewModel("title", null)
        {
            // Act
            Infos = infosValue
        };

        // Assert
        Assert.That(vm.HasInfos, Is.EqualTo(expected));
    }

    /// <summary>
    /// Ensures HasInfos updates correctly when Infos is changed multiple times on the same instance.
    /// Input conditions: start default, set to positive, then back to zero.
    /// Expected result: false -> true -> false.
    /// </summary>
    [Test]
    public void HasInfos_ChangesWhenInfosUpdated_MatchesExpectedTransitions()
    {
        // Arrange
        var vm = new MenuNavigationItemViewModel("menu", null);

        // Act & Assert - initial should be false (default 0)
        Assert.That(vm.HasInfos, Is.False, "Initial HasInfos should be false when Infos defaults to 0.");

        // Act - set to a positive value
        vm.Infos = 2;
        Assert.That(vm.HasInfos, Is.True, "HasInfos should be true when Infos is set to a positive value.");

        // Act - set back to zero
        vm.Infos = 0;
        Assert.That(vm.HasInfos, Is.False, "HasInfos should be false after Infos is set back to zero.");
    }
}
