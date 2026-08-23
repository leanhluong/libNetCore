# libNetCore

Bộ mảnh dùng chung, **không dính nghiệp vụ**, cho dịch vụ .NET.

Sinh ra từ dự án [ONoOffice](https://github.com/leanhluong/ONoOffice) nhưng cố tình tách repo riêng để không bị lây nghiệp vụ vào.

## Ba package, chia theo phụ thuộc

| Package | Chứa gì | Phụ thuộc |
|---|---|---|
| `LibNetCore.Core` | `Result<T>`, `Error`, `Entity`, `AggregateRoot`, `IDateTimeProvider` | **Không gì cả** ngoài .NET gốc |
| `LibNetCore.AspNetCore` | Middleware đổi `Error` → Problem Details (RFC 7807), `ICurrentUser` | ASP.NET Core |
| `LibNetCore.EntityFrameworkCore` | Quy ước đặt tên snake_case, repository gốc | EF Core |

**Vì sao không gộp làm một cho gọn:** tầng `Domain` của ứng dụng sẽ tham chiếu `LibNetCore.Core`. Nếu gộp, `Domain` kéo theo cả ASP.NET Core lẫn EF Core — đúng thứ Clean Architecture cấm. Gộp cho tiện hôm nay là tự tay phá luật tầng ngày mai.

## Luật của repo này

1. **Không có từ nghiệp vụ.** Thấy chữ `Employee`, `Department`, `Leave`… trong đây là đã đặt sai chỗ.
2. **Chỉ thêm khi có chỗ dùng thật.** Một lớp chưa có ai gọi thì chưa được vào.
3. **Ba package chung một version** (lockstep). Phát hành = sửa `<Version>` trong `Directory.Build.props` một lần rồi tag `v{version}`.

## Chạy

```bash
dotnet build
dotnet test
```

## Version

`0.1.0` — đang dựng khung.
