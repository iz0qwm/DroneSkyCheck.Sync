using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace DroneSkyCheck.Sync;

public partial class TransferProgressWindow : Window
{
    private bool _isComplete;
    private bool _isStopped;
    private int _totalCount;
    private int _processedCount;
    private int _uploadedCount;
    private int _alreadyImportedCount;
    private int _duplicateFlightCount;
    private int _reviewRequiredCount;
    private int _failedCount;
    private string _operatorId = string.Empty;
    private string _ownerUid = string.Empty;
    private string _deviceName = string.Empty;
    private DateTimeOffset _startedAt;
    private DateTimeOffset? _completedAt;

    public TransferProgressWindow()
    {
        Entries = [];

        InitializeComponent();

        DataContext = this;
        Closing += TransferProgressWindow_Closing;
    }

    public ObservableCollection<TransferLogEntry> Entries { get; }

    public bool IsStopRequested { get; private set; }

    public void Start(
        int totalCount,
        string deviceName,
        string operatorId,
        string ownerUid)
    {
        _totalCount = totalCount;
        _deviceName = deviceName;
        _operatorId = operatorId;
        _ownerUid = ownerUid;
        _startedAt = DateTimeOffset.Now;
        IsStopRequested = false;
        _isStopped = false;

        TransferProgressBar.Maximum = Math.Max(totalCount, 1);
        TransferProgressBar.Value = 0;
        StopTransferButton.IsEnabled = true;
        FooterText.Text = "Puoi interrompere il trasferimento; la coda si fermera dopo il file corrente.";

        UpdateSummary();
    }

    public TransferLogEntry AddEntry(
        int number,
        string fileName)
    {
        var entry = new TransferLogEntry
        {
            Number = number,
            FileName = fileName,
            Status = "In corso",
            Detail = "Preparazione"
        };

        Entries.Add(entry);
        EntriesList.ScrollIntoView(entry);

        return entry;
    }

    public void UpdateEntry(
        TransferLogEntry entry,
        string status,
        string detail,
        string sha256 = "",
        long sizeBytes = 0,
        string error = "")
    {
        entry.Status = status;
        entry.Detail = detail;

        if (!string.IsNullOrWhiteSpace(sha256))
            entry.Sha256 = sha256;

        if (sizeBytes > 0)
            entry.SizeBytes = sizeBytes;

        if (!string.IsNullOrWhiteSpace(error))
            entry.Error = error;

        EntriesList.Items.Refresh();
        EntriesList.ScrollIntoView(entry);
    }

    public void MarkUploaded(TransferLogEntry entry)
    {
        _uploadedCount++;
        MarkProcessed(entry, "Caricato", "Import completato");
    }

    public void MarkAlreadyImported(TransferLogEntry entry)
    {
        _alreadyImportedCount++;
        MarkProcessed(entry, "Gia presente", "File gia importato nel cloud");
    }

    public void MarkDuplicateFlight(TransferLogEntry entry)
    {
        _duplicateFlightCount++;
        MarkProcessed(entry, "Volo noto", "File ricevuto, volo gia noto");
    }

    public void MarkRequiresReview(
        TransferLogEntry entry,
        string detail)
    {
        _reviewRequiredCount++;
        MarkProcessed(
            entry,
            "Revisione",
            string.IsNullOrWhiteSpace(detail)
                ? "Importato, associazione drone da verificare"
                : detail);
    }

    public void MarkFailed(
        TransferLogEntry entry,
        Exception exception)
    {
        _failedCount++;
        MarkProcessed(
            entry,
            "Errore",
            exception.Message,
            exception.ToString());
    }

    public void Complete(
        bool wasStopped = false)
    {
        _completedAt = DateTimeOffset.Now;
        _isComplete = true;
        _isStopped = wasStopped || IsStopRequested;
        TransferProgressBar.Value = _processedCount;
        StopTransferButton.IsEnabled = false;
        CopyReportButton.IsEnabled = true;
        SaveReportButton.IsEnabled = true;
        CloseButton.IsEnabled = true;
        FooterText.Text = _isStopped
            ? "Trasferimento interrotto. Report parziale disponibile."
            : "Report disponibile: puoi copiarlo o salvarlo per inviarlo allo sviluppatore.";

        UpdateSummary();
    }

    public string BuildReport()
    {
        var builder = new StringBuilder();

        builder.AppendLine("DroneSkyCheck.Sync - Report trasferimento Flight Log");
        builder.AppendLine($"Avvio: {_startedAt:yyyy-MM-dd HH:mm:ss zzz}");

        if (_completedAt.HasValue)
            builder.AppendLine($"Fine: {_completedAt:yyyy-MM-dd HH:mm:ss zzz}");

        builder.AppendLine($"Dispositivo: {_deviceName}");
        builder.AppendLine($"OperatorId: {_operatorId}");
        builder.AppendLine($"OwnerUid: {_ownerUid}");
        builder.AppendLine($"Totale: {_totalCount}");
        builder.AppendLine($"Processati: {_processedCount}");
        builder.AppendLine($"Interrotto: {(_isStopped ? "Si" : "No")}");
        builder.AppendLine($"Caricati: {_uploadedCount}");
        builder.AppendLine($"Gia presenti: {_alreadyImportedCount}");
        builder.AppendLine($"Voli gia noti: {_duplicateFlightCount}");
        builder.AppendLine($"In revisione: {_reviewRequiredCount}");
        builder.AppendLine($"Errori: {_failedCount}");
        builder.AppendLine();

        foreach (var entry in Entries)
        {
            builder.AppendLine($"[{entry.Number}] {entry.FileName}");
            builder.AppendLine($"Stato: {entry.Status}");
            builder.AppendLine($"Dettaglio: {entry.Detail}");

            if (entry.SizeBytes > 0)
                builder.AppendLine($"Dimensione: {entry.SizeBytes}");

            if (!string.IsNullOrWhiteSpace(entry.Sha256))
                builder.AppendLine($"SHA-256: {entry.Sha256}");

            if (!string.IsNullOrWhiteSpace(entry.Error))
            {
                builder.AppendLine("Errore completo:");
                builder.AppendLine(entry.Error);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private void MarkProcessed(
        TransferLogEntry entry,
        string status,
        string detail,
        string error = "")
    {
        _processedCount++;

        UpdateEntry(
            entry,
            status,
            detail,
            error: error);

        TransferProgressBar.Value = _processedCount;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var prefix =
            _isStopped
                ? "Trasferimento interrotto - "
                : string.Empty;

        SummaryText.Text =
            prefix +
            $"{_processedCount}/{_totalCount} processati - {_uploadedCount} caricati, {_alreadyImportedCount} gia presenti, {_duplicateFlightCount} voli gia noti, {_reviewRequiredCount} in revisione, {_failedCount} errori";
    }

    private void StopTransferButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        RequestStop(
            "Interruzione richiesta. Il trasferimento si fermera al termine del file corrente.");
    }

    private void CopyReportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Clipboard.SetText(BuildReport());
        FooterText.Text = "Report copiato negli appunti.";
    }

    private void SaveReportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Salva report trasferimento",
            FileName = $"dsc-transfer-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            Filter = "File di testo (*.txt)|*.txt|Tutti i file (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        File.WriteAllText(
            dialog.FileName,
            BuildReport(),
            Encoding.UTF8);

        FooterText.Text = $"Report salvato: {dialog.FileName}";
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private void TransferProgressWindow_Closing(
        object? sender,
        System.ComponentModel.CancelEventArgs e)
    {
        if (_isComplete)
            return;

        RequestStop(
            "Chiusura richiesta. Il trasferimento si fermera al termine del file corrente.");

        e.Cancel = true;
    }

    private void RequestStop(string message)
    {
        IsStopRequested = true;
        StopTransferButton.IsEnabled = false;
        FooterText.Text = message;
    }
}
