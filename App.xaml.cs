using System.Windows;
using MindForge.Utils;

namespace MindForge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Logger.Info("MindForge gestartet");
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Unbehandelter Fehler", args.Exception);
            MessageBox.Show($"Fehler: {args.Exception.Message}", "MindForge Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("MindForge beendet");
        base.OnExit(e);
    }
}
