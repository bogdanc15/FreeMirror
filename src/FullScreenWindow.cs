// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace FreeMirror;

sealed class FullScreenWindow
{
    nint window;
    int style;
    Placement placement;
    public bool Active => window != 0 && IsWindow(window);
    public static Rectangle Bounds(nint handle)
    {
        if (!GetWindowRect(handle, out var rect)) throw new Win32Exception();
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public static nint FindVideoWindow(int processId)
    {
        nint found = 0;
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out uint owner);
            if (owner != processId || !IsWindowVisible(handle)) return true;
            var className = new StringBuilder(128);
            GetClassName(handle, className, className.Capacity);
            if (className.ToString() is not ("GSTD3D11" or "GstD3D12Hwnd")) return true;
            found = handle;
            return false;
        }, 0);
        return found;
    }

    public void Toggle(nint handle)
    {
        if (Active && window == handle)
        {
            SetWindowLong(handle, -16, style);
            if (!SetWindowPlacement(handle, ref placement) || !SetWindowPos(handle, 0, 0, 0, 0, 0, 0x37)) throw new Win32Exception();
            window = 0;
            return;
        }
        if (!IsWindow(handle)) throw new InvalidOperationException("Start mirroring from your Apple device first, then click Full screen.");
        placement = new Placement { Length = Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(handle, ref placement)) throw new Win32Exception();
        style = GetWindowLong(handle, -16);
        var display = Screen.FromHandle(handle).Bounds;
        // Keep the receiver's video pipeline untouched; only its native window changes.
        SetWindowLong(handle, -16, style & ~0x21CF0000);
        if (!SetWindowPos(handle, 0, display.X, display.Y, display.Width, display.Height, 0x20))
        {
            SetWindowLong(handle, -16, style);
            throw new Win32Exception();
        }
        window = handle;
    }

    public void Reset() => window = 0;

    [StructLayout(LayoutKind.Sequential)]
    struct Placement
    {
        public int Length, Flags, ShowCommand;
        public Point MinPosition, MaxPosition;
        public Rect NormalPosition;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int Left, Top, Right, Bottom; }
    delegate bool WindowCallback(nint handle, nint parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(nint handle, StringBuilder name, int maxCount);
    [DllImport("user32.dll")] static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll", SetLastError = true)] static extern int GetWindowLong(nint handle, int index);
    [DllImport("user32.dll", SetLastError = true)] static extern int SetWindowLong(nint handle, int index, int value);
    [DllImport("user32.dll", SetLastError = true)] static extern bool GetWindowPlacement(nint handle, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] static extern bool GetWindowRect(nint handle, out Rect bounds);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPlacement(nint handle, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(nint handle, nint after, int x, int y, int width, int height, uint flags);
}
