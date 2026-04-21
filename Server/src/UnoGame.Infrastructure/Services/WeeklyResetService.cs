using MongoDB.Driver;
using UnoGame.Infrastructure.Repositories;

namespace UnoGame.Infrastructure.Services;

/// <summary>
/// Reset weeklyScore về 0 mỗi thứ Hai 00:00 UTC.
/// Chạy như BackgroundService (Hosted Service) trong ASP.NET Core.
/// </summary>
public class WeeklyResetService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WeeklyResetService> _log;

    public WeeklyResetService(IServiceProvider services, ILogger<WeeklyResetService> log)
    {
        _services = services;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextMonday();
            _log.LogInformation("Weekly reset in {Hours:F1}h", delay.TotalHours);
            await Task.Delay(delay, stoppingToken);

            await ResetWeeklyScoresAsync();
        }
    }

    private async Task ResetWeeklyScoresAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var col = db.GetCollection<UserDocument>("users");

        var result = await col.UpdateManyAsync(
            Builders<UserDocument>.Filter.Gt(u => u.WeeklyScore, 0),
            Builders<UserDocument>.Update.Set(u => u.WeeklyScore, 0));

        _log.LogInformation("Weekly reset: {Count} users reset", result.ModifiedCount);
    }

    private static TimeSpan TimeUntilNextMonday()
    {
        var now = DateTime.UtcNow;
        int days = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
        if (days == 0 && now.TimeOfDay > TimeSpan.Zero) days = 7;
        var next = now.Date.AddDays(days);
        return next - now;
    }
}