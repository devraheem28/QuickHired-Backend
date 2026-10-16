using Serilog;

namespace QuickHired.API
{
    public static class SerilogConfig
    {
        public static void ConfigureLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File("Logs/quickhired-log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }
    }
}

