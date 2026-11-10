using System.Text.Json;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Domain.Entities;

namespace FW.UnitTests.Fakes;

/// <summary>Đồng hồ giả: điều khiển thời gian trong test.</summary>
public class FakeClock : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 10, 2, 0, 0, DateTimeKind.Utc);
    public DateTime Now => UtcNow.AddHours(7);
    public DateOnly Today => DateOnly.FromDateTime(Now);
    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}

/// <summary>Cache trong bộ nhớ có hết hạn theo <see cref="FakeClock"/>, tuần tự hóa JSON như cài đặt thật.</summary>
public class FakeCache : ICacheService
{
    private readonly Dictionary<string, (string Json, DateTime? ExpiresAt)> _items = new();
    private readonly FakeClock _clock;

    public FakeCache(FakeClock clock) => _clock = clock;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGet(key, out var json) ? JsonSerializer.Deserialize<T>(json) : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        _items[key] = (JsonSerializer.Serialize(value), expiry.HasValue ? _clock.UtcNow.Add(expiry.Value) : null);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _items.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGet(key, out _));

    private bool TryGet(string key, out string json)
    {
        json = string.Empty;
        if (!_items.TryGetValue(key, out var item)) return false;
        if (item.ExpiresAt.HasValue && item.ExpiresAt <= _clock.UtcNow)
        {
            _items.Remove(key);
            return false;
        }
        json = item.Json;
        return true;
    }
}

/// <summary>Email giả: ghi lại thư đã gửi để test đọc OTP.</summary>
public class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        Sent.Add((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}

/// <summary>Băm mật khẩu giả (không dùng BCrypt cho test nhanh).</summary>
public class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;
    public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
}

public class FakeTokenGenerator : IJwtTokenGenerator
{
    public AccessToken Generate(User user, IReadOnlyList<string> roles) =>
        new($"token-for-{user.Id}", new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc));
}

public class FakeCurrentUser : ICurrentUserService
{
    public FakeCurrentUser(int? branchId, params string[] roles)
    {
        BranchId = branchId;
        Roles = roles;
    }

    public int? UserId { get; init; } = 1;
    public int? BranchId { get; }
    public IReadOnlyList<string> Roles { get; }
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => Roles.Contains(role);
}

/// <summary>Repository người dùng trong bộ nhớ — chỉ cài đặt các hàm mà nghiệp vụ xác thực sử dụng.</summary>
public class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = new();
    public Dictionary<int, List<string>> Roles { get; } = new();

    public User Add(User user, params string[] roles)
    {
        user.Id = Users.Count + 1;
        Users.Add(user);
        Roles[user.Id] = roles.ToList();
        return user;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => !u.IsDeleted && u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => !u.IsDeleted && u.Id == id));

    public Task<IReadOnlyList<string>> GetRoleNamesAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Roles.GetValueOrDefault(userId) ?? new List<string>());

    public Task<bool> UpdateAsync(User entity, CancellationToken cancellationToken = default) => Task.FromResult(true);

    // Không dùng trong test xác thực
    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PaginatedResult<User>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PaginatedResult<User>> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<int> AddAsync(User entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ExistsByEmailAsync(string email, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ExistsByEmployeeCodeAsync(string employeeCode, int? excludeId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SetRolesAsync(int userId, IEnumerable<int> roleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> GetRoleNamesByUserIdsAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
