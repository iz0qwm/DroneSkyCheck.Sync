using DroneSkyCheck.Sync.Core.Cloud;
using DroneSkyCheck.Sync.Core.Devices;
using DroneSkyCheck.Sync.Models.Cloud;
using DroneSkyCheck.Sync.Models.Devices;
using DroneSkyCheck.Sync.Models.FlightLogs;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DroneSkyCheck.Sync;

public partial class MainWindow : Window
{
    private const long MaxFlightLogBytes = 50L * 1024L * 1024L;

    private readonly DriveDeviceProvider _driveDeviceProvider = new();
    private readonly MtpDeviceProvider _mtpDeviceProvider = new();
    private readonly MtpFlightLogProvider _mtpFlightLogProvider = new();
    private readonly ICloudAuthenticationService _cloudAuthenticationService;
    private readonly IFlightLogImportService _flightLogImportService;

    private bool _isAuthenticated;
    private bool _hasDscPlus;
    private bool _isTransferInProgress;
    private int _newLogsCount;
    private Device? _selectedDevice;
    private IReadOnlyList<FlightLogFile> _currentFlightLogs = [];
    private readonly HashSet<string> _cloudKnownFlightLogKeys = [];

    public MainWindow()
        : this(
            new FirebaseAuthenticationService(),
            new DjiFlightLogImportService())
    {
    }

    public MainWindow(
        ICloudAuthenticationService cloudAuthenticationService)
        : this(
            cloudAuthenticationService,
            new DjiFlightLogImportService())
    {
    }

    public MainWindow(
        ICloudAuthenticationService cloudAuthenticationService,
        IFlightLogImportService flightLogImportService)
    {
        _cloudAuthenticationService = cloudAuthenticationService;
        _flightLogImportService = flightLogImportService;

        InitializeComponent();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        SetDisconnectedAccountState();

        await RestoreCloudSessionAsync();

        await LoadDevicesAsync();
    }

    private async Task RestoreCloudSessionAsync()
    {
        await _cloudAuthenticationService.GetIdTokenAsync();

        if (_cloudAuthenticationService.IsAuthenticated)
        {
            SetConnectedAccountState(
                _cloudAuthenticationService.GetCurrentUser());
        }
    }

    private async Task LoadDevicesAsync()
    {
        try
        {
            RefreshDevicesButton.IsEnabled = false;
            StatusText.Text = "Ricerca dei dispositivi collegati...";

            DevicesList.ItemsSource = null;
            FlightLogsList.ItemsSource = null;
            _selectedDevice = null;
            _currentFlightLogs = [];
            _cloudKnownFlightLogKeys.Clear();

            ResetLogStatistics();

            var devices = new List<Device>();

            devices.AddRange(
                await _driveDeviceProvider.GetDevicesAsync());

            devices.AddRange(
                await _mtpDeviceProvider.GetDevicesAsync());

            DevicesList.ItemsSource = devices;

            StatusText.Text = devices.Count switch
            {
                0 => "Nessun dispositivo rilevato.",
                1 => "1 dispositivo rilevato.",
                _ => $"{devices.Count} dispositivi rilevati."
            };
        }
        catch (Exception ex)
        {
            StatusText.Text =
                $"Errore durante la ricerca dei dispositivi: {ex.Message}";
        }
        finally
        {
            RefreshDevicesButton.IsEnabled = true;
        }
    }

    private void DevicesList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (DevicesList.SelectedItem is not Device device)
        {
            _selectedDevice = null;
            _currentFlightLogs = [];
            _cloudKnownFlightLogKeys.Clear();

            SelectedSourceText.Text =
                "Seleziona un dispositivo per cercare i Flight Log.";

            FlightLogsList.ItemsSource = null;

            ResetLogStatistics();

            return;
        }

        LoadFlightLogs(device);
    }

    private void LoadFlightLogs(Device device)
    {
        _selectedDevice = device;
        _currentFlightLogs = [];
        _cloudKnownFlightLogKeys.Clear();

        SelectedSourceText.Text =
            $"Ricerca dei Flight Log in {device.Name}...";

        StatusText.Text =
            $"Analisi di {device.Name}...";

        try
        {
            if (!device.IsMtp)
            {
                FlightLogsList.ItemsSource = null;
                _currentFlightLogs = [];

                SelectedSourceText.Text =
                    $"{device.Name} è disponibile, ma la lettura delle cartelle locali non è ancora implementata.";

                StatusText.Text =
                    "Origine locale non ancora supportata.";

                ResetLogStatistics();

                return;
            }

            var logs = _mtpFlightLogProvider.GetFlightLogs(device.DeviceId);

            FlightLogsList.ItemsSource = logs;
            _currentFlightLogs = logs;

            UpdateNewLogsCount();

            LogsFoundValueText.Text = logs.Count.ToString();
            NewLogsValueText.Text = logs.Count.ToString();
            ImportedLogsValueText.Text = "0";

            SelectedSourceText.Text = logs.Count switch
            {
                0 => $"Nessun Flight Log DJI trovato in {device.Name}.",
                1 => $"1 Flight Log DJI trovato in {device.Name}.",
                _ => $"{logs.Count} Flight Log DJI trovati in {device.Name}."
            };

            StatusText.Text = logs.Count switch
            {
                0 => "Nessun Flight Log disponibile.",
                1 => "1 Flight Log pronto.",
                _ => $"{logs.Count} Flight Log pronti."
            };

            UpdateTransferAvailability();
        }
        catch (Exception ex)
        {
            FlightLogsList.ItemsSource = null;
            _currentFlightLogs = [];
            _cloudKnownFlightLogKeys.Clear();

            SelectedSourceText.Text =
                "Non è stato possibile leggere i Flight Log dal dispositivo.";

            StatusText.Text =
                $"Errore durante la lettura: {ex.Message}";

            ResetLogStatistics();
        }
    }

    private async void RefreshDevicesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await LoadDevicesAsync();
    }

    private async void TransferButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await TransferFlightLogsAsync();
    }

    private void FlightLogsList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateTransferAvailability();
    }

    private async void CloudAccountButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_cloudAuthenticationService.IsAuthenticated)
        {
            var user = _cloudAuthenticationService.GetCurrentUser();

            var logoutResult = MessageBox.Show(
                $"Disconnettere {GetCloudUserDisplayName(user)}?",
                "Account Cloud DSC",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (logoutResult != MessageBoxResult.Yes)
                return;

            _cloudAuthenticationService.Logout();

            SetDisconnectedAccountState();

            StatusText.Text = "Account Cloud DSC disconnesso.";

            return;
        }

        var loginWindow = new CloudLoginWindow(_cloudAuthenticationService)
        {
            Owner = this
        };

        var loginResult = loginWindow.ShowDialog();

        if (loginResult != true)
            return;

        await _cloudAuthenticationService.GetIdTokenAsync();

        SetConnectedAccountState(
            _cloudAuthenticationService.GetCurrentUser());

        StatusText.Text = "Account Cloud DSC connesso.";
    }

    private void SetDisconnectedAccountState()
    {
        _isAuthenticated = false;
        _hasDscPlus = false;

        CloudStatusIndicator.Fill =
            new SolidColorBrush(Color.FromRgb(154, 164, 173));

        CloudStatusText.Text = "Non connesso";
        CloudAccountText.Text = "Account Cloud DSC";
        CloudAccountButton.Content = "Accedi";

        UpdateTransferAvailability();
    }

    private void SetConnectedAccountState(CloudUser? user)
    {
        if (user == null)
        {
            SetDisconnectedAccountState();
            return;
        }

        _isAuthenticated = true;
        _hasDscPlus = user.Plan == CloudAccountPlan.DscPlus;

        CloudStatusIndicator.Fill =
            new SolidColorBrush(Color.FromRgb(25, 135, 84));

        CloudStatusText.Text = "Connesso";
        CloudAccountText.Text = $"Piano {GetCloudPlanText(user.Plan)}";
        CloudAccountButton.Content = GetCloudUserDisplayName(user);

        UpdateTransferAvailability();
    }

    private void UpdateTransferAvailability()
    {
        if (_isTransferInProgress)
        {
            TransferButton.IsEnabled = false;
            TransferRequirementText.Text = "Trasferimento in corso...";
            return;
        }

        var selectedLogsCount =
            GetSelectedFlightLogs().Count;

        var availableTransferCount =
            GetTransferCandidateLogs().Count;

        var hasOperatorContext =
            HasOperatorContext(
                _cloudAuthenticationService.GetCurrentUser());

        var canTransfer =
            _isAuthenticated &&
            _hasDscPlus &&
            hasOperatorContext &&
            availableTransferCount > 0;

        TransferButton.IsEnabled = canTransfer;
        TransferButton.Content = selectedLogsCount > 0
            ? "Trasferisci selezione"
            : "Trasferisci a DSC";

        if (canTransfer)
        {
            TransferRequirementText.Text = selectedLogsCount > 0
                ? $"{availableTransferCount} log selezionati per DSC"
                : $"{availableTransferCount} log pronti per DSC";
        }
        else
        {
            TransferRequirementText.Text =
                GetTransferRequirementMessage(selectedLogsCount);
        }
    }

    private string GetTransferRequirementMessage(
        int selectedLogsCount)
    {
        if (!_isAuthenticated)
            return "Accedi al tuo Account Cloud DSC.";

        if (!_hasDscPlus)
            return "La sincronizzazione richiede DSC+.";

        if (!HasOperatorContext(_cloudAuthenticationService.GetCurrentUser()))
            return "Operatore UAS non disponibile per l'account cloud.";

        if (_newLogsCount == 0)
            return "Nessun nuovo Flight Log da trasferire.";

        if (selectedLogsCount > 0)
            return "I log selezionati risultano gia gestiti.";

        return string.Empty;
    }

    private static string GetCloudUserDisplayName(CloudUser? user)
    {
        if (user == null)
            return "Account DSC";

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            return user.DisplayName;

        if (!string.IsNullOrWhiteSpace(user.Email))
            return user.Email;

        return "Account DSC";
    }

    private static string GetCloudPlanText(CloudAccountPlan plan)
    {
        return plan == CloudAccountPlan.DscPlus
            ? "DSC+"
            : "FREE";
    }

    private async Task TransferFlightLogsAsync()
    {
        var transferLogs =
            GetTransferCandidateLogs();

        if (_selectedDevice == null ||
            transferLogs.Count == 0)
        {
            StatusText.Text = "Nessun Flight Log da trasferire.";
            return;
        }

        var idToken =
            await _cloudAuthenticationService.GetIdTokenAsync();

        var user =
            _cloudAuthenticationService.GetCurrentUser();

        if (string.IsNullOrWhiteSpace(idToken) ||
            user == null)
        {
            SetDisconnectedAccountState();
            StatusText.Text = "Sessione cloud scaduta. Accedi di nuovo.";
            return;
        }

        var operatorId =
            user.OperatorId;

        var ownerUid =
            user.OwnerUid;

        if (string.IsNullOrWhiteSpace(operatorId) ||
            string.IsNullOrWhiteSpace(ownerUid))
        {
            StatusText.Text = "Operatore UAS non disponibile per l'account cloud.";
            return;
        }

        var uploadedCount = 0;
        var alreadyImportedCount = 0;
        var duplicateFlightCount = 0;
        var reviewRequiredCount = 0;
        var failedCount = 0;
        var wasStopped = false;
        var progressWindow = new TransferProgressWindow
        {
            Owner = this
        };

        progressWindow.Start(
            transferLogs.Count,
            _selectedDevice.Name,
            operatorId,
            ownerUid);

        progressWindow.Show();

        _isTransferInProgress = true;
        UpdateTransferAvailability();
        RefreshDevicesButton.IsEnabled = false;

        try
        {
            for (var index = 0; index < transferLogs.Count; index++)
            {
                if (progressWindow.IsStopRequested)
                {
                    wasStopped = true;
                    break;
                }

                var flightLog = transferLogs[index];
                var progressEntry =
                    progressWindow.AddEntry(
                        index + 1,
                        flightLog.FileName);

                try
                {
                    StatusText.Text =
                        $"Preparazione {index + 1}/{transferLogs.Count}: {flightLog.FileName}";

                    progressWindow.UpdateEntry(
                        progressEntry,
                        "In corso",
                        "Lettura file");

                    var content =
                        await ReadFlightLogBytesAsync(
                            _selectedDevice,
                            flightLog);

                    if (content.LongLength > MaxFlightLogBytes)
                    {
                        throw new InvalidOperationException(
                            "Flight Log oltre il limite massimo di 50 MB.");
                    }

                    var sha256 =
                        ComputeSha256(content);

                    progressWindow.UpdateEntry(
                        progressEntry,
                        "In corso",
                        "Controllo cloud",
                        sha256,
                        content.LongLength);

                    StatusText.Text =
                        $"Controllo cloud {index + 1}/{transferLogs.Count}: {flightLog.FileName}";

                    var checkResult =
                        await _flightLogImportService.CheckDjiFlightLogImportAsync(
                            idToken,
                            operatorId,
                            ownerUid,
                            sha256,
                            content.LongLength,
                            flightLog.FileName);

                    if (checkResult.AlreadyImported)
                    {
                        alreadyImportedCount++;
                        MarkCloudKnown(flightLog);
                        progressWindow.MarkAlreadyImported(progressEntry);
                        UpdateTransferStatistics(
                            uploadedCount,
                            alreadyImportedCount,
                            duplicateFlightCount,
                            reviewRequiredCount,
                            failedCount,
                            transferLogs.Count,
                            index + 1);

                        continue;
                    }

                    StatusText.Text =
                        $"Invio a DSC {index + 1}/{transferLogs.Count}: {flightLog.FileName}";

                    progressWindow.UpdateEntry(
                        progressEntry,
                        "In corso",
                        "Upload verso DSC");

                    var importResult =
                        await _flightLogImportService.ImportDjiFlightLogAsync(
                            idToken,
                            operatorId,
                            ownerUid,
                            sha256,
                            content,
                            flightLog.FileName);

                    if (!importResult.PersistedFleetRegistry)
                    {
                        throw new InvalidOperationException(
                            GetIncompleteImportMessage(importResult));
                    }

                    if (importResult.IsDuplicateFlight)
                    {
                        duplicateFlightCount++;
                        MarkCloudKnown(flightLog);
                        progressWindow.MarkDuplicateFlight(progressEntry);
                    }
                    else if (importResult.RequiresReview)
                    {
                        reviewRequiredCount++;
                        MarkCloudKnown(flightLog);
                        progressWindow.MarkRequiresReview(
                            progressEntry,
                            GetReviewMessage(importResult));
                    }
                    else
                    {
                        uploadedCount++;
                        MarkCloudKnown(flightLog);
                        progressWindow.MarkUploaded(progressEntry);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    progressWindow.MarkFailed(
                        progressEntry,
                        ex);

                    StatusText.Text =
                        $"Errore su {flightLog.FileName}: {ex.Message}";
                }

                UpdateTransferStatistics(
                    uploadedCount,
                    alreadyImportedCount,
                    duplicateFlightCount,
                    reviewRequiredCount,
                    failedCount,
                    transferLogs.Count,
                    index + 1);

                if (progressWindow.IsStopRequested)
                {
                    wasStopped = true;
                    break;
                }
            }
        }
        finally
        {
            _isTransferInProgress = false;
            RefreshDevicesButton.IsEnabled = true;
            progressWindow.Complete(wasStopped);
        }

        UpdateNewLogsCount();

        var cloudKnownCount =
            _cloudKnownFlightLogKeys.Count;

        ImportedLogsValueText.Text = cloudKnownCount.ToString();
        NewLogsValueText.Text = _newLogsCount.ToString();

        SelectedSourceText.Text = wasStopped
            ? $"Trasferimento interrotto: {uploadedCount} caricati, {alreadyImportedCount} gia presenti, {duplicateFlightCount} voli gia noti, {reviewRequiredCount} in revisione, {failedCount} errori."
            : $"Trasferimento completato su {transferLogs.Count} log: {uploadedCount} caricati, {alreadyImportedCount} gia presenti, {duplicateFlightCount} voli gia noti, {reviewRequiredCount} in revisione, {failedCount} errori.";

        StatusText.Text = wasStopped
            ? "Trasferimento interrotto. Puoi chiudere la finestra e riprovare quando vuoi."
            : (failedCount, reviewRequiredCount) switch
        {
            (0, 0) => "I Flight Log selezionati risultano gia presenti o trasferiti su DSC.",
            (0, _) => "Alcuni Flight Log richiedono revisione nel registro flotta DSC.",
            _ => $"{failedCount} Flight Log non trasferiti. Puoi riprovare."
        };

        UpdateTransferAvailability();
    }

    private async Task<byte[]> ReadFlightLogBytesAsync(
        Device device,
        FlightLogFile flightLog)
    {
        if (device.IsMtp)
        {
            return await RunInStaThreadAsync(() =>
                _mtpFlightLogProvider.ReadFlightLogBytes(
                    device.DeviceId,
                    flightLog));
        }

        if (string.IsNullOrWhiteSpace(flightLog.FullPath) ||
            !File.Exists(flightLog.FullPath))
        {
            throw new FileNotFoundException(
                "Flight Log locale non trovato.",
                flightLog.FullPath);
        }

        return await File.ReadAllBytesAsync(flightLog.FullPath);
    }

    private static Task<T> RunInStaThreadAsync<T>(Func<T> action)
    {
        var completion =
            new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return completion.Task;
    }

    private void UpdateTransferStatistics(
        int uploadedCount,
        int alreadyImportedCount,
        int duplicateFlightCount,
        int reviewRequiredCount,
        int failedCount,
        int totalCount,
        int processedCount)
    {
        ImportedLogsValueText.Text =
            _cloudKnownFlightLogKeys.Count.ToString();
        NewLogsValueText.Text =
            (_newLogsCount - processedCount + failedCount).ToString();
    }

    private static bool HasOperatorContext(CloudUser? user)
    {
        return user != null &&
            !string.IsNullOrWhiteSpace(user.OperatorId) &&
            !string.IsNullOrWhiteSpace(user.OwnerUid);
    }

    private static string GetIncompleteImportMessage(
        DjiFlightLogImportResult importResult)
    {
        var detail =
            GetReviewMessage(importResult);

        return string.IsNullOrWhiteSpace(detail)
            ? "Import non completato nel registro flotta DSC."
            : $"Import non completato nel registro flotta DSC: {detail}";
    }

    private static string GetReviewMessage(
        DjiFlightLogImportResult importResult)
    {
        if (!string.IsNullOrWhiteSpace(importResult.ReconciliationIssue))
            return GetReconciliationIssueText(importResult.ReconciliationIssue);

        if (!string.IsNullOrWhiteSpace(importResult.ReconciliationStatus))
            return GetReconciliationIssueText(importResult.ReconciliationStatus);

        if (!string.IsNullOrWhiteSpace(importResult.ReconciliationAction))
            return importResult.ReconciliationAction;

        return string.Empty;
    }

    private static string GetReconciliationIssueText(
        string issue)
    {
        return issue switch
        {
            "missing_aircraft_serial" => "seriale drone mancante",
            "aircraft_serial_model_mismatch" => "seriale e modello drone non coincidono",
            "multiple_manual_candidates" => "piu droni manuali candidati",
            _ => issue
        };
    }

    private static string ComputeSha256(byte[] content)
    {
        return Convert
            .ToHexString(SHA256.HashData(content))
            .ToLowerInvariant();
    }

    private void ResetLogStatistics()
    {
        _newLogsCount = 0;
        _currentFlightLogs = [];
        _cloudKnownFlightLogKeys.Clear();

        LogsFoundValueText.Text = "0";
        NewLogsValueText.Text = "0";
        ImportedLogsValueText.Text = "0";

        UpdateTransferAvailability();
    }

    private IReadOnlyList<FlightLogFile> GetTransferCandidateLogs()
    {
        var selectedLogs =
            GetSelectedFlightLogs();

        var sourceLogs =
            selectedLogs.Count > 0
                ? selectedLogs
                : _currentFlightLogs;

        return sourceLogs
            .Where(log => !_cloudKnownFlightLogKeys.Contains(GetFlightLogKey(log)))
            .ToList();
    }

    private IReadOnlyList<FlightLogFile> GetSelectedFlightLogs()
    {
        if (FlightLogsList?.SelectedItems == null)
            return [];

        return FlightLogsList
            .SelectedItems
            .OfType<FlightLogFile>()
            .ToList();
    }

    private void MarkCloudKnown(FlightLogFile flightLog)
    {
        _cloudKnownFlightLogKeys.Add(
            GetFlightLogKey(flightLog));
    }

    private void UpdateNewLogsCount()
    {
        _newLogsCount = _currentFlightLogs.Count(log =>
            !_cloudKnownFlightLogKeys.Contains(GetFlightLogKey(log)));
    }

    private static string GetFlightLogKey(FlightLogFile flightLog)
    {
        if (!string.IsNullOrWhiteSpace(flightLog.SourceObjectId))
            return $"mtp:{flightLog.SourceObjectId}";

        if (!string.IsNullOrWhiteSpace(flightLog.FullPath))
            return $"path:{flightLog.FullPath}";

        return $"file:{flightLog.FileName}:{flightLog.Size}:{flightLog.LastModifiedUtc:O}";
    }
}
