using LibNetCore.Core.Domain;

namespace LibNetCore.Core.Tests.Domain;

// Hai lớp giả lập để kiểm hành vi của lớp gốc.
file sealed class Employee(Guid id) : Entity<Guid>(id);
file sealed class Department(Guid id) : Entity<Guid>(id);

public class EntityTests
{
    // Cốt lõi của khái niệm "thực thể": SO SÁNH BẰNG DANH TÍNH, không bằng nội dung
    // và cũng không bằng ô nhớ. Cùng một nhân viên đọc từ DB ở hai chỗ khác nhau
    // sẽ là hai đối tượng khác ô nhớ - nhưng vẫn phải là MỘT người.
    [Fact]
    public void TwoEntities_WithTheSameId_AreEqual()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new Employee(id), new Employee(id));
    }

    [Fact]
    public void TwoEntities_WithDifferentIds_AreNotEqual()
    {
        Assert.NotEqual(new Employee(Guid.NewGuid()), new Employee(Guid.NewGuid()));
    }

    // Bẫy hay gặp: nhân viên và phòng ban lỡ trùng Guid thì KHÔNG được coi là một.
    [Fact]
    public void EntitiesOfDifferentTypes_WithTheSameId_AreNotEqual()
    {
        var id = Guid.NewGuid();

        Assert.False(new Employee(id).Equals(new Department(id)));
    }

    [Fact]
    public void Entity_IsNotEqualToNull()
    {
        Assert.False(new Employee(Guid.NewGuid()).Equals(null));
    }

    // Thiếu cái này thì hai thực thể "bằng nhau" vẫn nằm ở hai ô khác nhau
    // trong Dictionary/HashSet - lỗi rất khó lần ra.
    [Fact]
    public void EqualEntities_ShareTheSameHashCode()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new Employee(id).GetHashCode(), new Employee(id).GetHashCode());
    }
}
