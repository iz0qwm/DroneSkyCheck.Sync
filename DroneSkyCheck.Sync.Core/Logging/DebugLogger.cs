using System.Diagnostics;

namespace DroneSkyCheck.Sync.Core.Logging
{
    public class DebugLogger : ILogger
    {
        public void Write(string message)
        {
            Debug.WriteLine($"[DroneSkyCheck.Sync] {message}");
        }
    }
}