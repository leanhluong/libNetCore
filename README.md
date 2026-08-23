# Luong.Kernel

Bộ mảnh dùng chung, **không dính nghiệp vụ**, cho dịch vụ .NET 10.

Gói NuGet: `Luong.Kernel.*` · Repo: [`leanhluong/libNetCore`](https://github.com/leanhluong/libNetCore)

Sinh ra từ dự án [ONoOffice](https://github.com/leanhluong/ONoOffice) nhưng cố tình tách repo riêng để không bị lây nghiệp vụ vào.

[![CI](https://github.com/leanhluong/libNetCore/actions/workflows/ci.yml/badge.svg)](https://github.com/leanhluong/libNetCore/actions/workflows/ci.yml)

## Bảy package, chia theo phụ thuộc

```
                       ┌─────────────────────┐
                       │   Luong.Kernel   │  ← không phụ thuộc gì ngoài .NET
                       └──────────┬──────────┘
     ┌──────────┬────────────┬────┴─────┬───────────┬──────────┐
     ▼          ▼            ▼          ▼           ▼          ▼
 AspNetCore  EntityFw    Messaging   Caching    Realtime     Jobs
```

| Package | Chứa gì | Phụ thuộc |
|---|---|---|
| `Luong.Kernel` | `Result`/`Error` · `Entity`/`AggregateRoot`/domain event · `IDateTimeProvider` · `ICurrentUser` · `PagedList` · `CaseConverter` · cổng Outbox/Inbox/Publisher/Cache/Lock | **Không gì cả** |
| `Luong.Kernel.AspNetCore` | `Error` → Problem Details (RFC 7807) · correlation-id · bắt exception lọt lưới · `Result` → `IResult` | ASP.NET Core |
| `Luong.Kernel.EntityFrameworkCore` | snake_case · interceptor audit · xoá mềm + bộ lọc toàn cục · bảng Outbox/Inbox + ghi cùng transaction | EF Core |
| `Luong.Kernel.Messaging` | `OutboxDispatcher` · `InboxGuard` · RabbitMQ publisher/consumer · hosted service điều phối 10 giây/vòng | RabbitMQ.Client |
| `Luong.Kernel.Caching` | `ICacheService` (Redis) · `IDistributedLock` nhả đúng mã bằng Lua · `CacheKey` | StackExchange.Redis |
| `Luong.Kernel.Realtime` | SignalR + backplane Redis · `ClaimsUserIdProvider` nhận cả `sub` lẫn `NameIdentifier` | SignalR |
| `Luong.Kernel.Jobs` | Hangfire cho việc nghiệp vụ **có lịch** · chặn cửa dashboard | Hangfire |

**Vì sao không gộp làm một:** tầng `Domain` của ứng dụng tham chiếu `Luong.Kernel`. Gộp lại thì `Domain` kéo theo cả ASP.NET, EF, RabbitMQ, Redis và Hangfire — đúng thứ Clean Architecture cấm. Chia package biến luật đó từ **lời hứa** thành **hàng rào compiler**.

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
3. **Bảy package chung một version** (lockstep). Nâng version = sửa `<Version>` trong `Directory.Build.props` **một lần**.
4. **Không có warning.** `TreatWarningsAsErrors` đang bật — kể cả cảnh báo lỗ hổng của package phụ thuộc (đã bắt được `Newtonsoft.Json` 11.0.1 do Hangfire kéo theo).

## Chạy tại máy

```bash
dotnet build      # 0 warning
dotnet test       # 149 test
dotnet pack -c Release -o nupkg
```

## CI

| Workflow | Khi nào chạy | Làm gì |
|---|---|---|
| `.github/workflows/ci.yml` | push lên `develop`/`main`, mọi PR | restore → build → test → **pack thử** |
| `.github/workflows/release.yml` | đẩy tag `v*` | đối chiếu tag ↔ `<Version>` → build → test → pack → đẩy GitHub Packages → tạo Release |

CI **pack thử ở mọi lần chạy** dù không phát hành: metadata NuGet hỏng thì phải biết ngay, không phải lúc đang cần phát hành gấp.

## Phát hành một phiên bản

```bash
# 1. Nâng version — MỘT chỗ duy nhất
#    Directory.Build.props:  <Version>0.2.0</Version>

git add Directory.Build.props
git commit -m "chore: bump version to 0.2.0"
git push origin develop

# 2. Gắn tag và đẩy tag — chính bước này kích hoạt phát hành
git tag v0.2.0
git push origin v0.2.0
```

Tag phải khớp `<Version>`, lệch thì workflow **dừng và báo lỗi** — chặn đúng sai sót kinh điển: tag `v0.2.0` nhưng quên sửa file, thế là phát hành ra gói `0.1.0` mang nhãn `0.2.0`.

Không cần tạo secret nào: `GITHUB_TOKEN` do GitHub Actions tự cấp, chỉ cần khai `permissions: packages: write`.

## Cài vào một dự án khác

GitHub Packages **luôn cần xác thực**, kể cả với package công khai. Tạo một Personal Access Token (classic) có quyền `read:packages` rồi thêm `nuget.config` vào dự án dùng:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/leanhluong/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="leanhluong" />
      <!-- Đọc từ biến môi trường, KHÔNG gõ token thẳng vào file rồi commit -->
      <add key="ClearTextPassword" value="%GITHUB_PACKAGES_TOKEN%" />
    </github>
  </packageSourceCredentials>
</configuration>
```

```bash
dotnet add package Luong.Kernel --version 0.2.0
```

## Lúc đang phát triển thì đừng dùng package

Nếu ONoOffice cài qua NuGet ngay từ đầu thì mỗi lần sửa một dòng trong `Result.cs` sẽ phải: nâng version → pack → push → chờ feed → restore bên kia. Sửa 20 lần một buổi là hết ngày.

```
LÚC PHÁT TRIỂN     ONoOffice ──ProjectReference──▶ ../libNetCore/src/...
                   sửa là dùng ngay, gỡ lỗi bước thẳng vào code lib

KHI ĐÃ ỔN ĐỊNH     ONoOffice ──PackageReference──▶ Luong.Kernel 0.2.0
                   ghim version, không bị đổi dưới chân
```

## Chỗ nào KHÔNG có test che

Cần hạ tầng thật mới kiểm được, nên các lớp này cố tình mỏng — chỉ nối dây, không chứa luật:

- `RabbitMqConnectionProvider` · `RabbitMqEventPublisher` · `RabbitMqConsumerBase`
- `OutboxDispatcherHostedService` (vòng lặp hẹn giờ — luật nằm ở `OutboxDispatcher`, đã test)
- `RedisLockStore` (giao thức nằm ở `DistributedLock`, đã test)
- `RealtimeServiceCollectionExtensions` · `JobsServiceCollectionExtensions` · `HangfireDashboardAuthorizationFilter`

## Version

`0.1.0`
