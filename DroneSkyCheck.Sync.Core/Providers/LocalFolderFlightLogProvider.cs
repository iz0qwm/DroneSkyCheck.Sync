using DroneSkyCheck.Sync.Core.Logging;
using DroneSkyCheck.Sync.Models.FlightLogs;

namespace DroneSkyCheck.Sync.Core.Providers
{
    public class LocalFolderFlightLogProvider : IFlightLogProvider
    {
        private readonly string _folderPath;
        private readonly ILogger _logger;

        public LocalFolderFlightLogProvider(
            string folderPath,
            ILogger logger)
        {
            _folderPath = folderPath;
            _logger = logger;
        }

        public Task<IReadOnlyList<FlightLogFile>> FindLogsAsync(
            CancellationToken cancellationToken = default)
        {
            _logger.Write($"Ricerca dei Flight Log in: {_folderPath}");

            if (!Directory.Exists(_folderPath))
            {
                _logger.Write("La cartella indicata non esiste.");

                return Task.FromResult<IReadOnlyList<FlightLogFile>>(
                    Array.Empty<FlightLogFile>());
            }

            var files = Directory
                .EnumerateFiles(
                    _folderPath,
                    "*.txt",
                    SearchOption.AllDirectories)
                .Select(filePath =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileInfo = new FileInfo(filePath);

                    return new FlightLogFile
                    {
                        FileName = fileInfo.Name,
                        FullPath = fileInfo.FullName,
                        Size = fileInfo.Length,
                        LastModifiedUtc = fileInfo.LastWriteTimeUtc
                    };
                })
                .ToList();

            _logger.Write($"Trovati {files.Count} Flight Log.");

            return Task.FromResult<IReadOnlyList<FlightLogFile>>(files);
        }
    }
}