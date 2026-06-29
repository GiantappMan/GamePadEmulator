// XInput 实时监控 —— 验证手柄输入是否注入成功
// 用法：先运行模拟器并「连接虚拟手柄」，再运行本程序；操作模拟器手柄，
//       这里会实时打印按下的按键和摇杆数值。如果这里能读到，游戏就能读到。
using System;
using System.Runtime.InteropServices;
using System.Threading;

class XInputMonitor
{
    [StructLayout(LayoutKind.Sequential)]
    struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger, bRightTrigger;
        public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct XINPUT_STATE { public uint dwPacketNumber; public XINPUT_GAMEPAD Gamepad; }

    [DllImport("xinput9_1_0.dll")] static extern uint XInputGetState(uint i, out XINPUT_STATE s);

    // 按键位掩码
    const ushort
        UP = 0x0001, DOWN = 0x0002, LEFT = 0x0004, RIGHT = 0x0008,
        START = 0x0010, BACK = 0x0020, L3 = 0x0040, R3 = 0x0080,
        LB = 0x0100, RB = 0x0200, GUIDE = 0x0400, _ = 0x0800,
        A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

    static void Main()
    {
        Console.Title = "XInput 手柄监控 (ESC 退出)";
        Console.WriteLine("正在检测虚拟手柄... (先在模拟器里点「连接虚拟手柄」)");
        Console.WriteLine();

        uint prevPacket = 0;
        var prev = default(XINPUT_GAMEPAD);
        bool connected = false;

        while (true)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) break;

            uint err = XInputGetState(0, out var st);

            if (err == 1167) // 未连接
            {
                if (connected) { Console.WriteLine("⚠ 手柄断开"); connected = false; }
                Thread.Sleep(200);
                continue;
            }
            if (!connected) { Console.WriteLine("✅ 检测到手柄！现在操作模拟器：\n"); connected = true; }

            // 状态变化或有摇杆/扳机活动时打印
            var g = st.Gamepad;
            bool changed = st.dwPacketNumber != prevPacket;
            bool analogActive = Math.Abs(g.sThumbLX) > 2000 || Math.Abs(g.sThumbLY) > 2000 ||
                                Math.Abs(g.sThumbRX) > 2000 || Math.Abs(g.sThumbRY) > 2000 ||
                                g.bLeftTrigger > 10 || g.bRightTrigger > 10;

            if (changed || analogActive)
            {
                Console.SetCursorPosition(0, Console.CursorTop < 4 ? 4 : Console.CursorTop);
                Console.WriteLine(new string(' ', 90));
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine(
                    $"按键:{Buttons(g.wButtons),-34} " +
                    $"左摇杆({g.sThumbLX,+6},{g.sThumbLY,+6}) " +
                    $"右摇杆({g.sThumbRX,+6},{g.sThumbRY,+6}) " +
                    $"LT:{g.bLeftTrigger,3} RT:{g.bRightTrigger,3}");
            }
            prev = g; prevPacket = st.dwPacketNumber;
            Thread.Sleep(16); // ~60fps
        }
    }

    static string Buttons(ushort w)
    {
        var b = "";
        if ((w & UP) != 0) b += "↑";
        if ((w & DOWN) != 0) b += "↓";
        if ((w & LEFT) != 0) b += "←";
        if ((w & RIGHT) != 0) b += "→";
        if ((w & A) != 0) b += " A";
        if ((w & B) != 0) b += " B";
        if ((w & X) != 0) b += " X";
        if ((w & Y) != 0) b += " Y";
        if ((w & LB) != 0) b += " LB";
        if ((w & RB) != 0) b += " RB";
        if ((w & L3) != 0) b += " L3";
        if ((w & R3) != 0) b += " R3";
        if ((w & START) != 0) b += " START";
        if ((w & BACK) != 0) b += " BACK";
        if ((w & GUIDE) != 0) b += " GUIDE";
        return b.Length == 0 ? "(无)" : b;
    }
}
