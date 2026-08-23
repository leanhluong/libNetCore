# libNetCore

Bộ mảnh dùng chung, **không dính nghiệp vụ**, cho dịch vụ .NET 10.

Sinh ra từ dự án [ONoOffice](https://github.com/leanhluong/ONoOffice) nhưng cố tình tách repo riêng để không bị lây nghiệp vụ vào.

## Năm package, chia theo phụ thuộc

```
                    ┌─────────────────────┐
                    │   LibNetCore.Core   │  ← không phụ thuộc gì ngoài .NET
                    └──────────┬──────────┘
        ┌──────────────┬───────┴───────┬──────────────┐
        ▼              ▼               ▼              ▼
   AspNetCore   EntityFramework    Messaging        Jobs
```

| Package | Chứa gì | Phụ thuộc |
|---|---|---|
| `LibNetCore.Core` | `Result`/`Error` · `Entity`/`AggregateRoot`/domain event · `IDateTimeProvider` · `ICurrentUser` · `PagedList` · cổng Outbox/Inbox/Publisher | **Không gì cả** |
| `LibNetCore.AspNetCore` | `Error` → Problem Details (RFC 7807) · middleware correlation-id · middleware bắt exception · `Result` → `IResult` | ASP.NET Core |
| `LibNetCore.EntityFrameworkCore` | Quy ước snake_case · interceptor audit · xoá mềm + bộ lọc toàn cục · bảng Outbox/Inbox + interceptor ghi cùng transaction | EF Core |
| `LibNetCore.Messaging` | `OutboxDispatcher` · `InboxGuard` · RabbitMQ publisher/consumer · hosted service điều phối outbox | RabbitMQ.Client |
| `LibNetCore.Jobs` | Hangfire cho việc nghiệp vụ có lịch · chặn cửa bảng điều khiển | Hangfire |

**Vì sao không gộp làm một:** tầng `Domain` của ứng dụng tham chiếu `LibNetCore.Core`. Gộp lại thì `Domain` kéo theo cả ASP.NET, EF, RabbitMQ và Hangfire — đúng thứ Clean Architecture cấm. Chia package biến luật đó từ **lời hứa** thành **hàng rào compiler**.

## Outbox + Inbox

```
[nghiệp vụ] Raise(EmployeeHired)          ← không biết RabbitMQ là gì
     ↓
[interceptor] ghi hàng outbox ── CÙNG TRANSACTION với dữ liệu nghiệp vụ
     ↓
[hosted service] mỗi 10 giây: đọc → gửi → đánh dấu (SAU khi gửi)
     ↓
[RabbitMQ]
     ↓
[consumer] InboxGuard: đã xử lý rồi thì bỏ qua, chưa thì chạy rồi ghi sổ
```

Outbox bảo đảm **gửi ít nhất một lần**, Inbox bỏ qua cái đã xử lý → **hiệu quả đúng một lần**. Đây là cách duy nhất đạt được điều đó, vì "gửi đúng một lần" giữa hai hệ thống là chuyện không làm được.

## Luật của repo này

1. **Không có từ nghiệp vụ.** Thấy `Employee`, `Department`, `Leave`… trong đây là đã đặt sai chỗ.
2. **Chỉ thêm khi có chỗ dùng thật.**
3. **Năm package chung một version** (lockstep). Phát hành = sửa `<Version>` trong `Directory.Build.props` một lần rồi tag `v{version}`.
4. **Không có warning.** `TreatWarningsAsErrors` đang bật — kể cả cảnh báo lỗ hổng của package phụ thuộc.

## Chạy

```bash
dotnet build      # 0 warning
dotnet test       # 127 test
dotnet pack -c Release -o nupkg
```

## Chỗ nào KHÔNG có test che

Cần một broker thật mới kiểm được, nên các lớp này cố tình mỏng — chỉ nối dây, không chứa luật:

- `RabbitMqConnectionProvider` · `RabbitMqEventPublisher` · `RabbitMqConsumerBase`
- `OutboxDispatcherHostedService` (vòng lặp hẹn giờ — luật nằm ở `OutboxDispatcher`, đã có test)
- `JobsServiceCollectionExtensions` · `HangfireDashboardAuthorizationFilter`

## Version

`0.1.0`
