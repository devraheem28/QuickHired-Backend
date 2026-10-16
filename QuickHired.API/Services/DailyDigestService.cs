using QuickHired.API.Helpers;
using QuickHired.API.Services;

namespace QuickHired.API.Services
{
    public class DailyDigestService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyDigestService> _logger;
        private readonly IConfiguration _configuration;

        public DailyDigestService(
            IServiceProvider serviceProvider,
            ILogger<DailyDigestService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var targetTime = new DateTime(now.Year, now.Month, now.Day, 21, 0, 0); // 9:00 PM

                // If it's past 9 PM today, schedule for tomorrow
                if (now > targetTime)
                {
                    targetTime = targetTime.AddDays(1);
                }

                var delay = targetTime - now;

                _logger.LogInformation($"Daily Digest scheduled for: {targetTime:yyyy-MM-dd HH:mm:ss}");

                try
                {
                    await Task.Delay(delay, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await SendDailyDigest();
                    }
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("Daily Digest service was cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Daily Digest service");
                }

                // Wait until the next day to run again
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }

        private async Task SendDailyDigest()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var statisticsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();

                var yesterday = DateTime.Now.AddDays(-1);
                var digestData = await statisticsService.GetDailyDigestDataAsync(yesterday);

                var adminEmail = _configuration["AdminEmail"];
                if (string.IsNullOrEmpty(adminEmail))
                {
                    _logger.LogWarning("Admin email not configured. Skipping daily digest.");
                    return;
                }

                var sendGridApiKey = _configuration["SendGrid:ApiKey"];
                await Helpers.EmailHelper.SendDailyDigestEmail(sendGridApiKey, adminEmail, digestData);

                _logger.LogInformation($"Daily digest email sent successfully for {yesterday:yyyy-MM-dd}");

                // Log digest summary
                LoggingHelper.LogInfo(
                    "Reports_Analytics",
                    "DAILY_DIGEST_SENT",
                    $"Sent for {yesterday:yyyy-MM-dd} - Users: {digestData.TotalUsers}, New Today: {digestData.DailySignups}, Active Subs: {digestData.ActiveSubscriptions}",
                    _logger
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send daily digest email");
                LoggingHelper.LogError("Reports_Analytics", "DAILY_DIGEST_FAILED", "Failed to send daily digest email", ex, _logger);
            }
        }
    }
}