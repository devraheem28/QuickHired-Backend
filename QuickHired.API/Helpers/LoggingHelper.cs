using Microsoft.Extensions.Logging;

namespace QuickHired.API.Helpers
{
    public static class LoggingHelper
    {
        public static void LogError(string functionalArea, string operation, string message, Exception ex = null, ILogger logger = null)
        {
            try
            {
                var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                var logFile = Path.Combine(logDirectory, $"{functionalArea}_logs_{DateTime.Now:yyyy-MM-dd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] [{operation}] {message}";

                if (ex != null)
                {
                    logEntry += $" | Exception: {ex.Message} | StackTrace: {ex.StackTrace}";
                }

                logEntry += Environment.NewLine;
                File.AppendAllText(logFile, logEntry);

                // Also log to ILogger if provided
                logger?.LogError(ex, "{Operation} - {Message}", operation, message);
            }
            catch
            {
                // Silent fail for logging to prevent infinite loops
            }
        }

        public static void LogWarning(string functionalArea, string operation, string message, ILogger logger = null)
        {
            try
            {
                var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                var logFile = Path.Combine(logDirectory, $"{functionalArea}_logs_{DateTime.Now:yyyy-MM-dd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WARNING] [{operation}] {message}{Environment.NewLine}";

                File.AppendAllText(logFile, logEntry);

                // Also log to ILogger if provided
                logger?.LogWarning("{Operation} - {Message}", operation, message);
            }
            catch
            {
                // Silent fail for logging to prevent infinite loops
            }
        }

        public static void LogInfo(string functionalArea, string operation, string message, ILogger logger = null)
        {
            try
            {
                var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                var logFile = Path.Combine(logDirectory, $"{functionalArea}_logs_{DateTime.Now:yyyy-MM-dd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [{operation}] {message}{Environment.NewLine}";

                File.AppendAllText(logFile, logEntry);

                // Also log to ILogger if provided
                logger?.LogInformation("{Operation} - {Message}", operation, message);
            }
            catch
            {
                // Silent fail for logging to prevent infinite loops
            }
        }
    }
}