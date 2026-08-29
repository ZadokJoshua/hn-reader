using Avalonia;
using System;
using System.Diagnostics;

namespace HNReader.Avalonia;

internal static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    [STAThread]
    public static void Main(string[] args)
    {
#if DEBUG
        Trace.Listeners.Add(new ConsoleTraceListener());
#endif
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
