using LaunchpadWindows.Models;
using LaunchpadWindows.SystemIntegration;

namespace LaunchpadWindows.Tests.SystemIntegration;

public sealed class HotkeyServiceTests
{
    [Fact]
    public void Register_ReturnsConflictWhenNativeRegistrationFails()
    {
        FakeHotkeyRegistrar registrar = new(false);
        HotkeyService service = new(registrar);

        HotkeyRegistrationResult result = service.Register(nint.Zero, HotkeyGesture.Default);

        Assert.False(result.Success);
        Assert.Contains("unavailable", result.Message);
    }

    [Fact]
    public void HandleHotkeyMessage_RaisesActivated()
    {
        FakeHotkeyRegistrar registrar = new(true);
        HotkeyService service = new(registrar);
        int activations = 0;
        service.Activated += (_, _) => activations++;
        service.Register(nint.Zero, HotkeyGesture.Default);

        service.HandleHotkeyMessage(HotkeyService.WmHotkey, HotkeyService.DefaultHotkeyId);

        Assert.Equal(1, activations);
    }

    [Fact]
    public void Register_RestoresPreviousHotkeyWhenNewRegistrationFails()
    {
        FakeHotkeyRegistrar registrar = new(true, false, true);
        HotkeyService service = new(registrar);
        service.Register((nint)123, HotkeyGesture.Default);

        HotkeyRegistrationResult result = service.Register((nint)123, new HotkeyGesture(Control: true, Alt: true, Shift: false, Windows: false, Key: "L"));

        Assert.False(result.Success);
        Assert.Equal(3, registrar.RegisterCount);
        Assert.Equal(1, registrar.UnregisterCount);
    }

    private sealed class FakeHotkeyRegistrar(params bool[] registerResults) : IHotkeyRegistrar
    {
        private int _nextResult;
        public int RegisterCount { get; private set; }
        public int UnregisterCount { get; private set; }

        public bool Register(nint windowHandle, int id, HotkeyModifiers modifiers, int virtualKey)
        {
            RegisterCount++;
            bool result = registerResults[Math.Min(_nextResult, registerResults.Length - 1)];
            _nextResult++;
            return result;
        }

        public void Unregister(nint windowHandle, int id)
        {
            UnregisterCount++;
        }
    }
}
