using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NewsWebApp.Services
{
    /// <summary>
    /// Background service that periodically polls and synchronizes live breaking news from external feeds.
    /// Runs seamlessly in the background without blocking client requests.
    /// </summary>
    public class LiveNewsSyncBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LiveNewsSyncBackgroundService> _logger;

        public LiveNewsSyncBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<LiveNewsSyncBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var isAutoSyncEnabled = _configuration.GetValue<bool>("LiveNews:AutoSyncEnabled", true);
            if (!isAutoSyncEnabled)
            {
                _logger.LogInformation("Automatic live news background synchronization is disabled via configuration.");
                return;
            }

            int intervalMinutes = _configuration.GetValue<int>("LiveNews:SyncIntervalMinutes", 60);
            if (intervalMinutes < 5) intervalMinutes = 5;

            _logger.LogInformation("LiveNewsSyncBackgroundService initialized with an interval of {Interval} minutes.", intervalMinutes);

            // Initial delay of 8 seconds to allow database migration/seeding on startup
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting scheduled live news background synchronization...");
                    using var scope = _scopeFactory.CreateScope();
                    var liveNewsService = scope.ServiceProvider.GetRequiredService<ILiveNewsService>();
                    var result = await liveNewsService.SyncAllFeedsAsync();
                    _logger.LogInformation("Live news sync completed. Added: {Added}, Duplicates skipped: {Skipped}", 
                        result.NewArticlesAdded, result.DuplicatesSkipped);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error occurred during live news background sync.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("LiveNewsSyncBackgroundService has stopped.");
        }
    }
}
