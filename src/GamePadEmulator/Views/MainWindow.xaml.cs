using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using GamePadEmulator.Core;

namespace GamePadEmulator.Views;

/// <summary>
/// Application shell: mode toggle (PS / Xbox), driver status, connect/disconnect,
/// and a live readout. Hosts the active <see cref="IGamepadView"/> in the centre stage.
/// </summary>
public partial class MainWindow : Window
{
    private ControllerType _type = ControllerType.Xbox;
    private bool _connected;
    private bool _gameMode;
    private bool _sideExpanded;   // 侧边栏默认折叠

    // 保存进入「游戏模式」前的窗口尺寸/位置，退出时还原。
    private double _normalWidth, _normalHeight;
    private double _normalLeft, _normalTop;

    public MainWindow()
    {
        InitializeComponent();

        // Default mode; overridable by GAMEPAD_MODE=ps for verification.
        var mode = Environment.GetEnvironmentVariable("GAMEPAD_MODE");
        if (string.Equals(mode, "ps", StringComparison.OrdinalIgnoreCase))
            SelectPs(null, null!);
        else
            SelectXbox(null, null!);   // default to Xbox (broadest game compatibility)

        RefreshStatus();

        // Optional auto game-mode for verification harness.
        if (string.Equals(Environment.GetEnvironmentVariable("GAMEPAD_GAMEMODE"), "1", StringComparison.OrdinalIgnoreCase))
        {
            Loaded += (_, _) => ToggleGameMode(null, null!);
        }

        // Optional self-capture for verification harness. Set GAMEPAD_CAPTURE=path
        // to have the window save a PNG of itself shortly after launch.
        var cap = Environment.GetEnvironmentVariable("GAMEPAD_CAPTURE");
        if (!string.IsNullOrEmpty(cap))
        {
            // Optional auto-connect so the captured screenshot shows the live "connected"
            // state. This exercises the exact same ControllerService.Connect path as the
            // GUI button; we drive it programmatically only for the screenshot harness.
            if (Environment.GetEnvironmentVariable("GAMEPAD_AUTOCONNECT") == "1")
            {
                var ok = App.Controllers.Connect(out _);
                if (ok)
                {
                    _connected = true;
                    ConnectBtn.Content = "断开连接";
                    ConnectBtn.Style = (Style)FindResource("DangerButton");
                    if (ViewHost.Content is IGamepadView gv) gv.Reset();
                    RefreshStatus();
                }
            }
            Utilities.WindowCapture.CaptureDelayed(this, cap);
        }
    }

    private void SelectXbox(object? sender, RoutedEventArgs e)
    {
        if (_connected) { Disconnect(); }
        _type = ControllerType.Xbox;
        App.Controllers.SelectedType = _type;
        ViewHost.Content = new XboxView();
        XboxTab.IsChecked = true;
        PsTab.IsChecked = false;
        ModeLabel.Text = "Xbox 360 (XInput)";
    }

    private void SelectPs(object? sender, RoutedEventArgs e)
    {
        if (_connected) { Disconnect(); }
        _type = ControllerType.PlayStation;
        App.Controllers.SelectedType = _type;
        ViewHost.Content = new Ds4View();
        PsTab.IsChecked = true;
        XboxTab.IsChecked = false;
        ModeLabel.Text = "DualShock 4 (PS / DInput)";
    }

    private void ConnectClick(object? sender, RoutedEventArgs e)
    {
        if (_connected)
        {
            Disconnect();
            return;
        }
        bool ok = App.Controllers.Connect(out string error);
        if (ok)
        {
            _connected = true;
            ConnectBtn.Content = "断开连接";
            ConnectBtn.Style = (Style)FindResource("DangerButton");
            if (ViewHost.Content is IGamepadView gv) gv.Reset();
        }
        else
        {
            MessageBox.Show(this, error, "连接失败",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshStatus();
    }

    private void Disconnect()
    {
        App.Controllers.Disconnect();
        _connected = false;
        ConnectBtn.Content = "连接虚拟手柄";
        ConnectBtn.Style = (Style)FindResource("PrimaryButton");
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        bool installed = ControllerService.IsViGEmBusInstalled();
        DriverDot.Fill = installed
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.OrangeRed;
        DriverText.Text = installed
            ? "ViGEmBus 驱动：已安装"
            : "ViGEmBus 驱动：未安装（无法在游戏中生效）";
        ConnDot.Fill = _connected
            ? System.Windows.Media.Brushes.LimeGreen
            : System.Windows.Media.Brushes.DimGray;
        ConnText.Text = _connected
            ? $"虚拟手柄：已连接 ({(_type == ControllerType.PlayStation ? "DualShock 4" : "Xbox 360")})"
            : "虚拟手柄：未连接";

        if (!installed && !_connected)
        {
            Hint.Visibility = Visibility.Visible;
            Hint.Text = "⚠ 未检测到 ViGEmBus 驱动。要真实操作游戏，请先安装 ViGEmBus 驱动（见底部说明）。UI 仍可正常交互预览。";
        }
        else if (_connected)
        {
            Hint.Visibility = Visibility.Visible;
            Hint.Text = "✅ 已连接。打开游戏，点击/拖拽手柄即可控制。摇杆按住拖动，松开回中；扳机按住蓄力。";
        }
        else
        {
            Hint.Visibility = Visibility.Collapsed;
        }
    }

    private void OpenDriverLink(object? sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/ViGEm/ViGEmBus/releases/latest",
                UseShellExecute = true
            });
        }
        catch { /* ignore launch failures */ }
    }

    /// <summary>
    /// 侧边栏展开/折叠（浮动层，不挤压手柄）。点击折叠标签条、展开态的 ✕、或半透明遮罩空白处都触发。
    /// 折叠态：右侧浮一条窄标签条；展开态：半透明遮罩 + 居中信息卡片浮于手柄之上。
    /// </summary>
    private void ToggleSidePanel(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_sideExpanded) ApplyCollapsedSidePanel();
        else ApplyExpandedSidePanel();
    }

    /// <summary>折叠侧栏：隐藏浮动面板，显示右侧窄标签条（仍可点开）。手柄画面不受任何挤压。</summary>
    private void ApplyCollapsedSidePanel()
    {
        _sideExpanded = false;
        SidePanel.Visibility = Visibility.Collapsed;
        CollapsedTab.Visibility = Visibility.Visible;
    }

    /// <summary>展开侧栏：浮动信息卡片叠在手柄之上（带遮罩），隐藏折叠标签条。</summary>
    private void ApplyExpandedSidePanel()
    {
        _sideExpanded = true;
        CollapsedTab.Visibility = Visibility.Collapsed;
        SidePanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 游戏模式开关：开启后窗口置顶浮于游戏之上，且点击时【不会夺取前台焦点】，
    /// 这样游戏始终保持活动窗口（看得见、有焦点），而你在浮窗上操作手柄，
    /// 信号照常注入游戏。实体手柄玩家正是这种体验：眼睛看游戏，手在别处操作。
    /// </summary>
    private void ToggleGameMode(object? sender, RoutedEventArgs e)
    {
        _gameMode = !_gameMode;
        var hwnd = new WindowInteropHelper(this).Handle;

        if (_gameMode)
        {
            // 记录当前尺寸以便退出时还原。
            _normalWidth = Width;
            _normalHeight = Height;
            _normalLeft = Left;
            _normalTop = Top;

            // 缩小成浮窗，靠右下角，不遮挡游戏主画面。
            Width = 460;
            Height = 360;
            // 浮窗里侧栏折叠成窄标签（仍可点开查看状态），而不是完全消失。
            ApplyCollapsedSidePanel();
            var screen = SystemParameters.WorkArea;
            Left = screen.Right - Width - 24;
            Top = screen.Bottom - Height - 24;

            // 置顶浮于所有窗口之上。
            Topmost = true;

            // WS_EX_NOACTIVATE：点击本窗口时不夺走前台焦点（游戏保持活动窗口）。
            int ex = (int)(long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE,
                new IntPtr(ex | (int)NativeMethods.WS_EX_NOACTIVATE));

            GameModeBtn.Content = "退出游戏模式";
            GameModeBtn.Style = (Style)FindResource("DangerButton");
        }
        else
        {
            Topmost = false;
            // 退出时把侧栏还原成进入游戏模式前的折叠态。
            ApplyCollapsedSidePanel();
            // 移除 NOACTIVATE，恢复正常窗口行为。
            int ex = (int)(long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE,
                new IntPtr(ex & ~(int)NativeMethods.WS_EX_NOACTIVATE));

            Width = _normalWidth; Height = _normalHeight;
            Left = _normalLeft; Top = _normalTop;

            GameModeBtn.Content = "游戏模式";
            GameModeBtn.Style = (Style)FindResource("PrimaryButton");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_connected) Disconnect();
        base.OnClosed(e);
    }
}
