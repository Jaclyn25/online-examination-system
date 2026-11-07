using Aperture.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace Aperture.Api.Attempts;

public class ExpirationWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ExpirationWorker> logger) : BackgroundService
{
    public async Task Sweep(CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var service = scope.ServiceProvider.GetRequiredService<AttemptService>();
        var ids = await db.Attempts.AsNoTracking().Where(a => a.Status == "Active" && a.Deadline <= clock.GetUtcNow()).OrderBy(a => a.Deadline).Select(a => a.Id).Take(100).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            await service.Finalize(await service.Locked(id));
            await tx.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Sweep(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Attempt expiration sweep failed; retrying next sweep"); }
            await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken);
        }
    }
}
