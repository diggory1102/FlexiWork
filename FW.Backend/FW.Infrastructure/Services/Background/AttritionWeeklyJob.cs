using FW.Application.Common.Interfaces;
using FW.Application.Features.Attrition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FW.Infrastructure.Services.Background;

/// <summary>Chạy chấm điểm hằng tuần (mặc định Thứ Hai 02:00 giờ VN).</summary>
public sealed class AttritionWeeklyJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IDateTimeProvider _clock;
    private readonly AttritionOptions _options;
    private readonly ILogger<AttritionWeeklyJob> _logger;

    public AttritionWeeklyJob(
        IServiceScopeFactory scopes,
        IDateTimeProvider clock,
        IOptions<AttritionOptions> options,
        ILogger<AttritionWeeklyJob> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Lần chạy kế tiếp (nghiêm ngặt sau <paramref name="now"/>).</summary>
    public static DateTime NextRun(DateTime now, DayOfWeek day, int hour)
    {
        var candidate = now.Date.AddHours(hour);
        var daysAhead = ((int)day - (int)now.DayOfWeek + 7) % 7;
        candidate = candidate.AddDays(daysAhead);
        return candidate <= now ? candidate.AddDays(7) : candidate;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.JobEnabled)
        {
            _logger.LogInformation("Tác vụ chấm điểm nguy cơ nghỉ việc đang tắt (Attrition:JobEnabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.Now;
            var next = NextRun(now, _options.JobDay, _options.JobHour);
            _logger.LogInformation("Chấm điểm nguy cơ nghỉ việc kế tiếp lúc {Next:yyyy-MM-dd HH:mm} (giờ VN).", next);

            try { await Task.Delay(next - now, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var summary = await scope.ServiceProvider.GetRequiredService<AttritionScoringService>().RunAsync(null, stoppingToken);
                _logger.LogInformation(
                    "Chấm điểm tuần {Week}: {Scored}/{Total} nhân viên đủ dữ liệu, {High} nguy cơ cao, {Medium} trung bình.",
                    summary.WeekStart, summary.Scored, summary.Employees, summary.High, summary.Medium);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Chấm điểm nguy cơ nghỉ việc thất bại; sẽ thử lại vào tuần sau.");
            }
        }
    }
}
