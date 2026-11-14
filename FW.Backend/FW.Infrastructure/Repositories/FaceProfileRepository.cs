using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Infrastructure.Database;

namespace FW.Infrastructure.Repositories;

public class FaceProfileRepository : BaseRepository<FaceProfile>, IFaceProfileRepository
{
    // Cần FloatArrayJsonTypeHandler để đọc/ghi cột face_encoding (kể cả khi repository được tạo ngoài DI, VD trong test)
    static FaceProfileRepository() => DapperConfig.Configure();

    public FaceProfileRepository(DbSession session) : base(session)
    {
    }

    protected override string TableName => "face_profiles";

    public Task<FaceProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<FaceProfile>($"{SelectFrom} WHERE `user_id` = @UserId", new { UserId = userId }, cancellationToken);

    public async Task<int> UpsertAsync(FaceProfile profile, CancellationToken cancellationToken = default)
    {
        // user_id là UNIQUE → đăng ký lại sẽ ghi đè vector & ảnh mẫu; updated_at tự cập nhật
        const string sql = """
            INSERT INTO `face_profiles` (`user_id`, `face_encoding`, `sample_image_url`, `sample_image_public_id`, `model_name`)
            VALUES (@UserId, @FaceEncoding, @SampleImageUrl, @SampleImagePublicId, @ModelName)
            ON DUPLICATE KEY UPDATE
                `face_encoding` = VALUES(`face_encoding`),
                `sample_image_url` = VALUES(`sample_image_url`),
                `sample_image_public_id` = VALUES(`sample_image_public_id`),
                `model_name` = VALUES(`model_name`);
            SELECT `id` FROM `face_profiles` WHERE `user_id` = @UserId;
            """;
        var id = await ExecuteScalarAsync<int>(sql, profile, cancellationToken);
        profile.Id = id;
        return id;
    }

    public async Task<bool> DeleteByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("DELETE FROM `face_profiles` WHERE `user_id` = @UserId", new { UserId = userId }, cancellationToken) > 0;

    public async Task<IReadOnlySet<int>> GetRegisteredUserIdsAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return new HashSet<int>();

        var rows = await QueryAsync<int>("SELECT `user_id` FROM `face_profiles` WHERE `user_id` IN @UserIds", new { UserIds = ids }, cancellationToken);
        return rows.ToHashSet();
    }
}
