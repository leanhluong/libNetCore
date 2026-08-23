using System.Reflection;
using FluentValidation;
using LibNetCore.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace LibNetCore.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký MediatR, mọi handler và validator tìm thấy trong các assembly được nêu,
    /// cùng ba behavior dùng chung.
    ///
    /// <b>Thứ tự đăng ký behavior CHÍNH LÀ thứ tự lồng nhau</b> — cái đăng ký trước nằm
    /// ngoài cùng. Thứ tự dưới đây có lý do cho từng nấc:
    ///
    /// <code>
    ///   Logging      ← ngoài cùng: thấy CẢ request bị chặn ở khâu kiểm dữ liệu
    ///     Validation ← chặn sớm: dữ liệu hỏng thì không đụng tới database
    ///       Transaction ← trong cùng: chỉ mở transaction quanh phần thật sự chạy
    ///         Handler
    /// </code>
    ///
    /// Đảo Logging vào trong cùng thì đúng những ca hỏng lại là những ca không có log.
    /// Đảo Transaction ra ngoài Validation thì mở transaction cho cả những request
    /// chắc chắn sẽ bị từ chối.
    /// </summary>
    public static IServiceCollection AddLibNetCoreApplication(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies.Length == 0)
        {
            throw new ArgumentException(
                "Phải nêu ít nhất một assembly chứa handler, nếu không sẽ không có handler nào được đăng ký.",
                nameof(assemblies));
        }

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblies(assemblies);

            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        // includeInternalTypes: validator nên là internal — nó là chuyện nội bộ của
        // module, không phải thứ module khác gọi tới.
        services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);

        return services;
    }
}
