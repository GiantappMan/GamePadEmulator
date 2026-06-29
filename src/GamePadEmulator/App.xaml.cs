using System;
using System.Windows;

namespace GamePadEmulator;

public partial class App : Application
{
    /// <summary>Single shared controller service for the whole app lifetime.</summary>
    public static Core.ControllerService Controllers { get; } = new();

    protected override void OnExit(ExitEventArgs e)
    {
        Controllers.Dispose();
        base.OnExit(e);
    }
}
