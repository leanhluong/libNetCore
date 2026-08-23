using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace Luong.Kernel.Jobs;

public static class JobsServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký Hangfire cho <b>việc nghiệp vụ có lịch</b> — "8h sáng gửi nhắc chấm công",
    /// "mùng 1 chốt báo cáo tháng".
    ///
    /// KHÔNG dùng cho điều phối outbox: cron của Hangfire nhỏ nhất là một phút, quá thưa.
    /// Việc đó do <c>OutboxDispatcherHostedService</c> ở <c>Luong.Kernel.Messaging</c> lo,
    /// chạy chu kỳ tính bằng giây.
    ///
    /// Dùng chung database với dữ liệu nghiệp vụ (schema <c>hangfire</c> riêng): job có
    /// lịch sử chạy, xem lại được, bấm chạy lại được — đó là thứ bảng outbox không có,
    /// và cũng là lý do hai thứ này KHÔNG thay thế nhau.
    /// </summary>
    public static IServiceCollection AddHangfireJobs(
        this IServiceCollection services,
        string connectionString,
        int workerCount = 5)
    {
        services.AddHangfire(configuration => configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(options =>
        {
            // Số luồng chạy job. Mặc định của Hangfire là số nhân CPU × 5 — trên máy
            // 16 nhân là 80 luồng cùng mở kết nối database, đủ để vét sạch connection
            // pool và làm chết luôn phần phục vụ request. Đặt tay một con số vừa phải.
            options.WorkerCount = workerCount;

            options.ServerName = $"{Environment.MachineName}:{Environment.ProcessId}";
        });

        return services;
    }
}
