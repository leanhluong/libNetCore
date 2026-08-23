using FluentValidation;
using Luong.Kernel.Application;
using Luong.Kernel.Application.Messaging;
using Luong.Kernel.Abstractions;
using Luong.Kernel.Primitives;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Luong.Kernel.Application.Tests;

// ── Use case giả để kiểm đường ống ────────────────────────────────────────────

public sealed record CreateEmployee(string FullName, string Email) : ICommand<Guid>;

public sealed record DeleteEmployee(Guid Id) : ICommand;

public sealed record GetEmployeeName(Guid Id) : IQuery<string>;

internal sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployee>
{
    public CreateEmployeeValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().WithMessage("Họ tên không được để trống.");
        RuleFor(c => c.Email).EmailAddress().WithMessage("Email không hợp lệ.");
    }
}

internal sealed class CreateEmployeeHandler : ICommandHandler<CreateEmployee, Guid>
{
    public static int Calls { get; set; }

    public Task<Result<Guid>> Handle(CreateEmployee request, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(Result.Success(Guid.NewGuid()));
    }

}

internal sealed class DeleteEmployeeHandler : ICommandHandler<DeleteEmployee>
{
    public Task<Result> Handle(DeleteEmployee request, CancellationToken ct) =>
        Task.FromResult(Result.Failure(Error.NotFound("Employee.NotFound", "Không tìm thấy nhân viên.")));
}

internal sealed class GetEmployeeNameHandler : IQueryHandler<GetEmployeeName, string>
{
    public Task<Result<string>> Handle(GetEmployeeName request, CancellationToken ct) =>
        Task.FromResult(Result.Success("Lê Anh Lượng"));
}

internal sealed class SpyUnitOfWork : IUnitOfWork
{
    public int SaveCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        return Task.FromResult(0);
    }
}

// ── Test ──────────────────────────────────────────────────────────────────────

public class PipelineTests
{
    private readonly SpyUnitOfWork _unitOfWork = new();

    private ISender BuildSender()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IUnitOfWork>(_unitOfWork);
        services.AddApplicationLayer(typeof(PipelineTests).Assembly);

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Command_ReachesItsHandler()
    {
        CreateEmployeeHandler.Calls = 0;

        var result = await BuildSender().Send(new CreateEmployee("Lê Anh Lượng", "a@b.com"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, CreateEmployeeHandler.Calls);
    }

    [Fact]
    public async Task Query_ReachesItsHandler()
    {
        var result = await BuildSender().Send(new GetEmployeeName(Guid.NewGuid()));

        Assert.Equal("Lê Anh Lượng", result.Value);
    }

    // Dữ liệu hỏng thì handler KHÔNG được chạy một dòng nào.
    [Fact]
    public async Task InvalidCommand_NeverReachesTheHandler()
    {
        CreateEmployeeHandler.Calls = 0;

        var result = await BuildSender().Send(new CreateEmployee("", "không-phải-email"));

        Assert.True(result.IsFailure);
        Assert.Equal(0, CreateEmployeeHandler.Calls);
    }

    // Gom HẾT lỗi rồi báo một lượt, không dừng ở luật sai đầu tiên.
    [Fact]
    public async Task InvalidCommand_ReportsEveryBrokenRule()
    {
        var result = await BuildSender().Send(new CreateEmployee("", "không-phải-email"));

        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(2, validation.Errors.Length);
        Assert.Contains(validation.Errors, e => e.Code == nameof(CreateEmployee.FullName));
        Assert.Contains(validation.Errors, e => e.Code == nameof(CreateEmployee.Email));
    }

    [Fact]
    public async Task SuccessfulCommand_CommitsTheTransaction()
    {
        await BuildSender().Send(new CreateEmployee("Lê Anh Lượng", "a@b.com"));

        Assert.Equal(1, _unitOfWork.SaveCalls);
    }

    // Handler trả về thất bại nghĩa là nghiệp vụ đã từ chối - những thay đổi
    // lỡ nằm trong bộ theo dõi KHÔNG được lén ghi xuống.
    [Fact]
    public async Task FailedCommand_DoesNotCommit()
    {
        await BuildSender().Send(new DeleteEmployee(Guid.NewGuid()));

        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task InvalidCommand_DoesNotCommitEither()
    {
        await BuildSender().Send(new CreateEmployee("", "sai"));

        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    // Truy vấn chỉ đọc thì không có gì để chốt - mở transaction cho nó là
    // giữ kết nối lâu hơn cần thiết mà chẳng được gì.
    [Fact]
    public async Task Query_DoesNotOpenATransaction()
    {
        await BuildSender().Send(new GetEmployeeName(Guid.NewGuid()));

        Assert.Equal(0, _unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Registration_RefusesAnEmptyAssemblyList()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddApplicationLayer());
        await Task.CompletedTask;
    }
}
