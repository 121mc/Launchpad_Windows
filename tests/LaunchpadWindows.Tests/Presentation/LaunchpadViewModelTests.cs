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

    private sealed class FakeLauncher(LaunchResult result) : ILauncher
    {
        public List<string> LaunchedItemIds { get; } = [];

        public LaunchResult Launch(LaunchItem item)
        {
            LaunchedItemIds.Add(item.Id);
            return result;
        }
    }
}
