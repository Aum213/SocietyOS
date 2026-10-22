using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocietyOS.Data;

namespace SocietyOS.Services
{
    public class VisitorRetentionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VisitorRetentionWorker> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

        public VisitorRetentionWorker(IServiceScopeFactory scopeFactory, ILogger<VisitorRetentionWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Visitor 48-Hour Retention Service activated.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PurgeExpiredRecordsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during 48-hour visitor retention purge cycle.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        public async Task<int> PurgeExpiredRecordsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var photoStorage = scope.ServiceProvider.GetRequiredService<IVisitorPhotoStorageService>();

            var nowUtc = DateTime.UtcNow;

            var expiredList = await context.VisitorRequests
                .Where(v => v.ExpiresAtUtc <= nowUtc)
                .Take(100) // Batch processing to avoid memory spikes
                .ToListAsync(cancellationToken);

            if (!expiredList.Any()) return 0;

            foreach (var req in expiredList)
            {
                // Delete photo file first
                await photoStorage.DeletePhotoAsync(req.PhotoStorageKey, cancellationToken);
            }

            context.VisitorRequests.RemoveRange(expiredList);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Purged {Count} expired visitor records in compliance with the 48-hour privacy rule.", expiredList.Count);
            return expiredList.Count;
        }
    }
}