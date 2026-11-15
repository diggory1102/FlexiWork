using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Features.FaceId;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace FW.UnitTests.Users;

/// <summary>
/// Đăng ký Face ID (Task 2.4): chỉ tự đăng ký lần đầu, ảnh không có mặt thì không lưu, Quản lý/HR đặt lại theo phạm vi chi nhánh.
/// </summary>
public class FaceProfileServiceTests
{
    private sealed class FakeRecognition : IFaceRecognitionService
    {
        public FaceExtractResult Result { get; set; } = new(true, "ok", true, new[] { 1, 2, 3, 4 }, Enumerable.Repeat(0.1f, 512).ToArray());

        public Task<FaceExtractResult> ExtractEmbeddingAsync(Stream image, string fileName, CancellationToken cancellationToken = default) => Task.FromResult(Result);

        public Task<FaceVerifyResult> VerifyAsync(Stream image, string fileName, IReadOnlyList<float> targetEmbedding, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeStorage : IFileStorageService
    {
        public List<string> Uploaded { get; } = new();
        public List<string> Deleted { get; } = new();

        public Task<FileUploadResult> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken = default)
        {
            var id = $"flexiwork/{folder}/{Uploaded.Count + 1}";
            Uploaded.Add(id);
            return Task.FromResult(new FileUploadResult($"https://img.test/{id}.jpg", id));
        }

        public Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
        {
            Deleted.Add(publicId);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeProfiles : IFaceProfileRepository
    {
        public Dictionary<int, FaceProfile> Items { get; } = new();

        public Task<FaceProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.GetValueOrDefault(userId));

        public Task<int> UpsertAsync(FaceProfile profile, CancellationToken cancellationToken = default)
        {
            profile.Id = profile.UserId;
            profile.CreatedAt = DateTime.Now;
            Items[profile.UserId] = profile;
            return Task.FromResult(profile.Id);
        }

        public Task<bool> DeleteByUserIdAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Remove(userId));

        public Task<IReadOnlySet<int>> GetRegisteredUserIdsAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<int>>(userIds.Where(Items.ContainsKey).ToHashSet());

        // Không dùng trong nghiệp vụ Face ID
        public Task<FaceProfile?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<FaceProfile>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaginatedResult<FaceProfile>> GetPagedAsync(PagedQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> AddAsync(FaceProfile entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(FaceProfile entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private readonly FakeUserRepository _users = new();
    private readonly FakeProfiles _profiles = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeRecognition _recognition = new();
    private readonly User _employee;

    public FaceProfileServiceTests()
    {
        _employee = _users.Add(new User { EmployeeCode = "EMP1", Email = "nv@test.vn", FullName = "Nhân viên", BranchId = 1 }, RoleNames.Employee);
    }

    private FaceProfileService Service(ICurrentUserService user) =>
        new(_profiles, _users, _recognition, _storage, user, NullLogger<FaceProfileService>.Instance);

    private FaceProfileService AsEmployee() => Service(new FakeCurrentUser(1, RoleNames.Employee) { UserId = _employee.Id });

    private static Task<FaceProfileStatusDto> Register(FaceProfileService service, string type = "image/jpeg", long length = 1000) =>
        service.RegisterMineAsync(new MemoryStream(new byte[16]), "selfie.jpg", type, length);

    [Fact]
    public async Task Employee_registers_once_and_cannot_replace_the_face_alone()
    {
        var service = AsEmployee();
        Assert.False((await service.GetMyStatusAsync()).Registered);

        var status = await Register(service);
        Assert.True(status.Registered);
        Assert.Equal(FaceProfile.DefaultModelName, status.ModelName);
        Assert.Equal(512, _profiles.Items[_employee.Id].FaceEncoding.Length);
        Assert.Equal("flexiwork/faces/1", _profiles.Items[_employee.Id].SampleImagePublicId);

        var again = await Assert.ThrowsAsync<BadRequestException>(() => Register(service));
        Assert.Contains("đặt lại", again.Message);
        Assert.Single(_storage.Uploaded);
    }

    [Fact]
    public async Task Image_without_a_usable_face_is_rejected_and_not_stored()
    {
        _recognition.Result = new FaceExtractResult(false, "Không tìm thấy khuôn mặt trong ảnh.", false, null, null);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => Register(AsEmployee()));
        Assert.Equal("Không tìm thấy khuôn mặt trong ảnh.", error.Message);
        Assert.Empty(_storage.Uploaded);
        Assert.Empty(_profiles.Items);
    }

    [Theory]
    [InlineData("application/pdf", 1000)]
    [InlineData("image/jpeg", 0)]
    [InlineData("image/jpeg", 6 * 1024 * 1024)]
    public async Task Wrong_type_or_size_is_rejected(string type, long length)
    {
        await Assert.ThrowsAsync<BadRequestException>(() => Register(AsEmployee(), type, length));
    }

    [Fact]
    public async Task Manager_of_the_branch_resets_the_face_and_the_old_image_is_deleted()
    {
        await Register(AsEmployee());

        await Assert.ThrowsAsync<ForbiddenException>(() => Service(new FakeCurrentUser(2, RoleNames.Manager)).ResetAsync(_employee.Id));

        var manager = Service(new FakeCurrentUser(1, RoleNames.Manager));
        Assert.True((await manager.GetStatusAsync(_employee.Id)).Registered);
        await manager.ResetAsync(_employee.Id);

        Assert.Empty(_profiles.Items);
        Assert.Equal(new[] { "flexiwork/faces/1" }, _storage.Deleted);
        await Assert.ThrowsAsync<BadRequestException>(() => manager.ResetAsync(_employee.Id)); // chưa đăng ký

        // Đặt lại xong thì nhân viên tự đăng ký lại được
        Assert.True((await Register(AsEmployee())).Registered);
    }
}
