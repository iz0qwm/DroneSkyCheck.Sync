using System;

namespace DroneSkyCheck.Sync.Models.FlightLogs
{
    public class FlightLogFile
    {
        public string FileName { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public string SourceObjectId { get; set; } = string.Empty;

        public long Size { get; set; }

        public DateTime LastModifiedUtc { get; set; }
    }
}
