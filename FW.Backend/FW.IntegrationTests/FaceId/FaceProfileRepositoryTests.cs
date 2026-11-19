using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Database;
using FW.Infrastructure.Repositories;

namespace FW.IntegrationTests.FaceId;

/// <summary>
/// Truy vấn dữ liệu khuôn mặt của các nhân viên khác trên MySQL thật (dùng để chặn hai tài khoản đăng ký cùng một khuôn mặt):
/// chỉ lấy người đang làm việc, bỏ người khóa / nghỉ việc / đã xóa và chính người đang đăng ký; vector 512 chiều đọc lại nguyên vẹn.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class FaceProfileRepositoryTests
{
    private readonly DatabaseFixture _db;

    public FaceProfileRepositoryTests(DatabaseFixture db)
    {
        _db = db;
    }

    private static async Task<User> NewUserWithFaceAsync(DbSession s, float seed, string status = UserStatus.Active)
    {
        var code = $"F{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var user = new User
        {
            EmployeeCode = code, Email = $"{code.ToLowerInvariant()}@test.flexiwork.vn", PasswordHash = "x", FullName = $"Mặt {code}", Status = status
        };
        await new UserRepository(s).AddAsync(user);
        await new FaceProfileRepository(s).UpsertAsync(new FaceProfile
        {
            UserId = user.Id, FaceEncoding = Enumerable.Range(0, 512).Select(i => seed + i / 1000f).ToArray(), ModelName = FaceProfile.DefaultModelName
        });
        return user;
    }

    [SkippableFact]
    public async Task Other_active_profiles_exclude_myself_and_people_who_are_gone()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var me = await NewUserWithFaceAsync(s, 0.1f);
        var colleague = await NewUserWithFaceAsync(s, 0.2f);
        var suspended = await NewUserWithFaceAsync(s, 0.3f, UserStatus.Suspended);
        var left = await NewUserWithFaceAsync(s, 0.4f, UserStatus.Left);
        var deleted = await NewUserWithFaceAsync(s, 0.5f);
        await new UserRepository(s).DeleteAsync(deleted.Id);

        var others = await new FaceProfileRepository(s).GetOtherActiveProfilesAsync(me.Id);
        var ids = others.Select(p => p.UserId).ToHashSet();

        Assert.Contains(colleague.Id, ids);
        Assert.DoesNotContain(me.Id, ids);
        Assert.DoesNotContain(suspended.Id, ids);
        Assert.DoesNotContain(left.Id, ids);
        Assert.DoesNotContain(deleted.Id, ids);

        var loaded = others.Single(p => p.UserId == colleague.Id);
        Assert.Equal(512, loaded.FaceEncoding.Length);
        Assert.Equal(0.2f + 7 / 1000f, loaded.FaceEncoding[7], 5);
    }
}
