using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace HerculesAudioControl;

public partial class App : Application
{
    private static bool _isShowingError;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        if (_isShowingError)
            return;

        _isShowingError = true;

        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "ZLDAudioControl",
                "Logs");

            Directory.CreateDirectory(directory);

            string path = Path.Combine(
                directory,
                $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            File.WriteAllText(
                path,
                e.Exception.ToString());

            MessageBox.Show(
                "ZLD Audio Control hat einen Fehler abgefangen.\n\n" +
                e.Exception.Message +
                "\n\nFehlerprotokoll:\n" +
                path,
                "ZLD Audio Control",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Kein zweiter Fehlerdialog, falls selbst das Logging fehlschlägt.
        }
        finally
        {
            _isShowingError = false;
        }
    }
}
