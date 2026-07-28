using DroneSkyCheck.Sync.Core.Cloud;
using System.Threading;
using System.Windows;

namespace DroneSkyCheck.Sync;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(
            true,
            "DroneSkyCheck.Sync.SingleInstance",
            out var isFirstInstance);

        _ownsSingleInstanceMutex = isFirstInstance;

        var authenticationService =
            new FirebaseAuthenticationService();

        var callbackUri =
            GetCallbackUri(e.Args);

        if (callbackUri != null)
        {
            var callbackResult =
                await authenticationService.HandleDscPortalCallback(
                    callbackUri);

            if (!callbackResult.IsSuccess)
            {
                MessageBox.Show(
                    callbackResult.ErrorMessage,
                    "Account Cloud DSC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            if (!isFirstInstance)
            {
                Shutdown();
                return;
            }
        }
        else if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        var mainWindow =
            new MainWindow(authenticationService);

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex)
            _singleInstanceMutex?.ReleaseMutex();

        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    private static Uri? GetCallbackUri(IReadOnlyList<string> args)
    {
        var callbackArg =
            args.FirstOrDefault(arg =>
                arg.StartsWith(
                    "droneskycheck-sync://auth/callback",
                    StringComparison.OrdinalIgnoreCase));

        return Uri.TryCreate(
            callbackArg,
            UriKind.Absolute,
            out var callbackUri)
            ? callbackUri
            : null;
    }
}
