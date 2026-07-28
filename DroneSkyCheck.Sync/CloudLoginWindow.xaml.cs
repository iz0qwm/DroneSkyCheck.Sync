using DroneSkyCheck.Sync.Core.Cloud;
using System.Windows;

namespace DroneSkyCheck.Sync;

public partial class CloudLoginWindow : Window
{
    private readonly ICloudAuthenticationService _authenticationService;

    public CloudLoginWindow(
        ICloudAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;

        InitializeComponent();
    }

    private async void DscPortalLoginButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetBusy(true, "Apro il portale Drone Sky Check nel browser...");

        try
        {
            var result =
                await _authenticationService.LoginWithDscPortal();

            if (result.IsSuccess)
            {
                DialogResult = true;
                Close();
                return;
            }

            StatusText.Text = result.ErrorMessage;
        }
        finally
        {
            if (IsVisible)
                SetBusy(false, StatusText.Text);
        }
    }

    private void SetBusy(
        bool isBusy,
        string statusMessage)
    {
        DscPortalLoginButton.IsEnabled = !isBusy;
        StatusText.Text = statusMessage;
    }
}

