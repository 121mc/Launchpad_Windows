using LaunchpadWindows.Models;
using LaunchpadWindows.Presentation;
using LaunchpadWindows.Shell;

namespace LaunchpadWindows.Tests.Presentation;

public sealed class LaunchpadViewModelTests
{
    [Fact]
    public void LaunchCommand_ClosesAfterSuccessfulLaunch()
    {
        LaunchItem item = LaunchItem.CreateManual("File", LaunchItemKind.File, @"C:\file.txt");
        FakeLauncher launcher = new(LaunchResult.Ok());
        LaunchpadViewModel vm = new(launcher);
        bool closeRequested = false;
        vm.CloseRequested += (_, _) => closeRequested = true;
        vm.SetItems([item]);

        vm.LaunchCommand.Execute(item);

        Assert.True(closeRequested);
        Assert.Equal(item.Id, launcher.LaunchedItemIds.Single());
    }

    [Fact]
    public void Reorder_MovesItemAndRaisesOrderChanged()
    {
        LaunchItem a = LaunchItem.CreateManual("A", LaunchItemKind.File, @"C:\a.txt");
        LaunchItem b = LaunchItem.CreateManual("B", LaunchItemKind.File, @"C:\b.txt");
        LaunchpadViewModel vm = new(new FakeLauncher(LaunchResult.Ok()));
        string[]? order = null;
        vm.OrderChanged += (_, ids) => order = ids;
        vm.SetItems([a, b]);

        vm.MoveItem(fromIndex: 1, toIndex: 0);

        Assert.Equal(["B", "A"], vm.Items.Select(item => item.DisplayName).ToArray());
        Assert.NotNull(order);
        Assert.Equal([b.Id, a.Id], order!);
    }

    [Fact]
    public void LaunchCommand_FailedLaunchSetsErrorAndDoesNotClose()
    {
        LaunchItem item = LaunchItem.CreateManual("File", LaunchItemKind.File, @"C:\file.txt");
        FakeLauncher launcher = new(LaunchResult.Fail("Could not launch"));
        LaunchpadViewModel vm = new(launcher);
        bool closeRequested = false;
        List<string?> changedProperties = [];
        vm.SetItems([item]);
        vm.CloseRequested += (_, _) => closeRequested = true;
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        vm.LaunchCommand.Execute(item);

        Assert.False(closeRequested);
        Assert.Equal("Could not launch", vm.ErrorMessage);
        Assert.Contains(nameof(LaunchpadViewModel.ErrorMessage), changedProperties);
        Assert.Equal(item.Id, launcher.LaunchedItemIds.Single());
    }

    [Fact]
    public void LaunchCommand_SuccessfulLaunchAfterFailureClearsErrorAndCloses()
    {
        LaunchItem item = LaunchItem.CreateManual("File", LaunchItemKind.File, @"C:\file.txt");
        FakeLauncher launcher = new(LaunchResult.Fail("Could not launch"), LaunchResult.Ok());
        LaunchpadViewModel vm = new(launcher);
        bool closeRequested = false;
        vm.CloseRequested += (_, _) => closeRequested = true;
        vm.SetItems([item]);
        vm.LaunchCommand.Execute(item);
        Assert.Equal("Could not launch", vm.ErrorMessage);

        vm.LaunchCommand.Execute(item);

        Assert.Null(vm.ErrorMessage);
        Assert.True(closeRequested);
        Assert.Equal([item.Id, item.Id], launcher.LaunchedItemIds);
    }

    [Fact]
    public void SetItems_ClearsErrorAfterFailedLaunch()
    {
        LaunchItem item = LaunchItem.CreateManual("File", LaunchItemKind.File, @"C:\file.txt");
        LaunchpadViewModel vm = new(new FakeLauncher(LaunchResult.Fail("Could not launch")));
        vm.SetItems([item]);
        vm.LaunchCommand.Execute(item);
        Assert.Equal("Could not launch", vm.ErrorMessage);

        vm.SetItems([item]);

        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void LaunchCommand_InvalidParameterCannotExecuteAndDoesNotLaunch()
    {
        FakeLauncher launcher = new(LaunchResult.Ok());
        LaunchpadViewModel vm = new(launcher);

        Exception? nullParameterException = Record.Exception(() => vm.LaunchCommand.Execute(null));
        Exception? wrongParameterException = Record.Exception(() => vm.LaunchCommand.Execute("not a launch item"));

        Assert.False(vm.LaunchCommand.CanExecute(null));
        Assert.False(vm.LaunchCommand.CanExecute("not a launch item"));
        Assert.Null(nullParameterException);
        Assert.Null(wrongParameterException);
        Assert.Empty(launcher.LaunchedItemIds);
    }

    private sealed class FakeLauncher(params LaunchResult[] results) : ILauncher
    {
        private readonly Queue<LaunchResult> _results = new(results);

        public List<string> LaunchedItemIds { get; } = [];

        public LaunchResult Launch(LaunchItem item)
        {
            LaunchedItemIds.Add(item.Id);
            return _results.Dequeue();
        }
    }
}
