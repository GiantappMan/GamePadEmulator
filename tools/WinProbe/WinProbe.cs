// 探测目标窗口的尺寸与扩展样式，验证「游戏模式」是否生效：
//   WS_EX_NOACTIVATE (0x08000000)  -> 点击不夺焦点
//   WS_EX_TOPMOST    (0x00000008)  -> 置顶
using System;
using System.Runtime.InteropServices;
using System.Text;

class WinProbe
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtr")] static extern IntPtr GetWindowLongPtr64(IntPtr h, int i);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

    const int GWL_EXSTYLE = -20;
    const long WS_EX_TOPMOST = 0x8L;
    const long WS_EX_NOACTIVATE = 0x08000000L;

    static IntPtr found = IntPtr.Zero;

    static bool Cb(IntPtr h, IntPtr l)
    {
        var sb = new StringBuilder(256);
        GetWindowText(h, sb, 256);
        if (sb.ToString().Contains("\u624b\u67c4\u6a21\u62df\u5668")) // "手柄模拟器"
            found = h;
        return true;
    }

    static void Main()
    {
        EnumWindows(Cb, IntPtr.Zero);
        if (found == IntPtr.Zero) { Console.WriteLine("WINDOW_NOT_FOUND"); return; }

        long ex = (long)GetWindowLongPtr64(found, GWL_EXSTYLE);
        GetWindowRect(found, out var r);
        int w = r.R - r.L, hh = r.B - r.T;

        Console.WriteLine($"Handle: 0x{(long)found:X}");
        Console.WriteLine($"Size: {w} x {hh}");
        Console.WriteLine($"WS_EX_TOPMOST (zhitding): {(ex & WS_EX_TOPMOST) != 0}");
        Console.WriteLine($"WS_EX_NOACTIVATE (click-no-focus): {(ex & WS_EX_NOACTIVATE) != 0}");
        // 预期游戏模式: size ~460x360, TOPMOST=true, NOACTIVATE=true
        bool gameMode = (ex & WS_EX_NOACTIVATE) != 0 && (ex & WS_EX_TOPMOST) != 0 && w <= 500 && hh <= 400;
        Console.WriteLine($"GAME_MODE_ACTIVE: {gameMode}");
    }
}
