using System.Runtime.InteropServices;

namespace Screenshotss;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 1;
    private readonly Action capture;

    internal bool Registered { get; }

    internal HotkeyWindow(Action capture, Keys key)
    {
        this.capture = capture;
        CreateHandle(new CreateParams { Caption = "Screenshotss hotkey", Parent = new IntPtr(-3) });
        Registered = RegisterHotKey(Handle, HotkeyId, 0x0002 | 0x0004 | 0x4000, (uint)key);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312 && message.WParam.ToInt32() == HotkeyId)
            capture();
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero)
            return;
        if (Registered)
            UnregisterHotKey(Handle, HotkeyId);
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
