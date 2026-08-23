namespace Luong.Kernel.Abstractions;

/// <summary>
/// Chốt lại mọi thay đổi của một đơn vị công việc thành MỘT transaction.
///
/// Chỉ có đúng một phương thức, và đó là chủ ý. Nó tồn tại để tầng Application ra lệnh
/// "ghi xuống đi" mà KHÔNG phải tham chiếu EF Core — nếu không thì use case sẽ phải
/// nhận thẳng <c>DbContext</c>, và tầng Application lập tức dính hạ tầng.
///
/// <b>Không phải một lớp mới.</b> <c>DbContext</c> của EF vốn ĐÃ là một unit of work:
/// nó theo dõi mọi thay đổi rồi ghi hết trong một transaction khi gọi
/// <c>SaveChanges</c>. Ở đây ta chỉ đặt cho nó một cái tên mà tầng trong gọi được.
/// Bản cài đặt thường chỉ là một dòng:
///
/// <code>
/// internal sealed class HrDbContext : DbContext, IUnitOfWork { }   // xong
/// </code>
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
