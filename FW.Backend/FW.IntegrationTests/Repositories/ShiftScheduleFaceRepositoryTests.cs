using FW.Application.Common.Interfaces.Persistence;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.Infrastructure.Repositories;

namespace FW.IntegrationTests.Repositories;

/// <summary>
/// Repository khung ca, lịch phân ca và dữ liệu khuôn mặt (Task 2.1) trên MySQL thật:
/// cột TIME ↔ TimeSpan, ca theo chi nhánh / dùng chung, INSERT nhiều dòng, công bố lịch, vector JSON 512 chiều.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ShiftScheduleFaceRepositoryTests
{
    // Dữ liệu seed: chi nhánh 1 (Cầu Giấy), 2 (Hoàn Kiếm); người dùng 2 (Quản lý), 3 (Nhân viên, đã có Face ID mẫu)
    private const int BranchId = 1;
    private const int OtherBranchId = 2;
    private const int ManagerId = 2;
    private const int EmployeeId = 3;

    private readonly DatabaseFixture _db;

    public ShiftScheduleFaceRepositoryTests(DatabaseFixture db)
    {
        _db = db;
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..20];

    private static Shift NewShift(int? branchId, int startHour, int endHour) => new()
    {
        ShiftCode = Unique("S-"),
        Name = "Ca kiểm thử",
        BranchId = branchId,
        StartTime = TimeSpan.FromHours(startHour),
        EndTime = TimeSpan.FromHours(endHour),
        BreakMinutes = 30,
        WorkUnits = 1.25m,
        IsCrossDay = Shift.SpansMidnight(TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour)),
        LateGraceMinutes = 10,
        EarlyLeaveGraceMinutes = 15
    };

    [SkippableFact]
    public async Task Shift_round_trips_time_columns_and_filters_by_branch()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var shifts = new ShiftRepository(session);

        var night = NewShift(BranchId, 22, 6);
        night.StartTime = new TimeSpan(22, 30, 0);
        var otherBranch = NewShift(OtherBranchId, 8, 12);
        var id = await shifts.AddAsync(night);
        await shifts.AddAsync(otherBranch);

        var saved = await shifts.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(new TimeSpan(22, 30, 0), saved!.StartTime);
        Assert.Equal(TimeSpan.FromHours(6), saved.EndTime);
        Assert.True(saved.IsCrossDay);
        Assert.Equal(BranchId, saved.BranchId);
        Assert.Equal(1.25m, saved.WorkUnits);
        Assert.Equal(10, saved.LateGraceMinutes);
        Assert.Equal(15, saved.EarlyLeaveGraceMinutes);
        Assert.Equal(420, saved.WorkingMinutes); // 22:30 → 06:00 = 450 phút − 30 phút nghỉ

        // Chi nhánh 1 thấy ca dùng chung (seed CA-SANG...) + ca riêng của mình, không thấy ca của chi nhánh 2
        var available = await shifts.GetAvailableForBranchAsync(BranchId);
        Assert.Contains(available, s => s.Id == id);
        Assert.Contains(available, s => s.ShiftCode == "CA-SANG" && s.BranchId == null);
        Assert.DoesNotContain(available, s => s.Id == otherBranch.Id);

        var ownOnly = await shifts.GetPagedAsync(new ShiftFilter { BranchId = BranchId, IncludeShared = false, PageSize = 100 });
        Assert.All(ownOnly.Items, s => Assert.Equal(BranchId, s.BranchId));

        Assert.True(await shifts.ExistsByCodeAsync(night.ShiftCode));
        Assert.False(await shifts.ExistsByCodeAsync(night.ShiftCode, excludeId: id));
        Assert.Equal(new[] { id }, (await shifts.GetByIdsAsync(new[] { id, id, -1 })).Select(s => s.Id));

        // Xóa mềm: không còn trong danh sách nhưng mã vẫn bị giữ (UNIQUE)
        Assert.True(await shifts.DeleteAsync(id));
        Assert.Null(await shifts.GetByIdAsync(id));
        Assert.True(await shifts.ExistsByCodeAsync(night.ShiftCode));
    }

    [SkippableFact]
    public async Task Schedules_are_bulk_inserted_as_drafts_then_published_and_cancelled()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var shifts = new ShiftRepository(session);
        var schedules = new ScheduleRepository(session);

        var shift = NewShift(BranchId, 8, 12);
        var shiftId = await shifts.AddAsync(shift);
        Assert.False(await shifts.IsInUseAsync(shiftId));

        var monday = new DateTime(2031, 1, 6);
        var week = Enumerable.Range(0, 3).Select(day => new Schedule
        {
            UserId = EmployeeId, ShiftId = shiftId, BranchId = BranchId, WorkDate = monday.AddDays(day), CreatedBy = ManagerId
        }).ToList();

        await session.ExecuteInTransactionAsync(async ct => Assert.Equal(3, await schedules.AddRangeAsync(week, ct)));
        Assert.True(await shifts.IsInUseAsync(shiftId));
        Assert.True(await schedules.ExistsAsync(EmployeeId, shiftId, monday.AddDays(1)));

        var query = new ScheduleQuery { FromDate = monday, ToDate = monday.AddDays(6), BranchId = BranchId };
        var drafts = await schedules.GetDetailsAsync(query);
        Assert.Equal(3, drafts.Count);
        Assert.All(drafts, d =>
        {
            Assert.False(d.IsPublished);
            Assert.Equal(ScheduleStatus.Scheduled, d.Status);
            Assert.Equal(shift.ShiftCode, d.ShiftCode);
            Assert.Equal(TimeSpan.FromHours(8), d.StartTime);
            Assert.Equal("EMP003", d.EmployeeCode);
            Assert.False(string.IsNullOrEmpty(d.FullName));
        });
        Assert.Equal(new[] { monday, monday.AddDays(1), monday.AddDays(2) }, drafts.Select(d => d.WorkDate));

        // Nhân viên chưa thấy lịch nháp
        Assert.Empty(await schedules.GetDetailsAsync(new ScheduleQuery { FromDate = monday, ToDate = monday.AddDays(6), UserId = EmployeeId, PublishedOnly = true }));

        var publishedAt = new DateTime(2031, 1, 3, 9, 0, 0);
        Assert.Equal(3, await schedules.PublishAsync(BranchId, monday, monday.AddDays(6), publishedAt));
        Assert.Equal(0, await schedules.PublishAsync(BranchId, monday, monday.AddDays(6), publishedAt)); // công bố lại không đổi gì
        var published = await schedules.GetDetailsAsync(new ScheduleQuery { FromDate = monday, ToDate = monday.AddDays(6), UserId = EmployeeId, PublishedOnly = true });
        Assert.Equal(3, published.Count);
        Assert.All(published, d => Assert.Equal(publishedAt, d.PublishedAt));

        // Hủy một ca đã công bố → không còn tính là lịch đang hiệu lực
        var first = await schedules.GetByIdAsync(published[0].Id);
        first!.Status = ScheduleStatus.Cancelled;
        Assert.True(await schedules.UpdateAsync(first));
        var active = await schedules.GetActiveByUserAsync(EmployeeId, monday, monday.AddDays(6));
        Assert.Equal(2, active.Count);
        Assert.DoesNotContain(active, s => s.Id == first.Id);
        Assert.Equal(ManagerId, active[0].CreatedBy);

        // Không xóa mềm: xóa là xóa hẳn
        Assert.True(await schedules.DeleteAsync(first.Id));
        Assert.Null(await schedules.GetByIdAsync(first.Id));
    }

    [SkippableFact]
    public async Task Face_profile_upsert_round_trips_512_dimension_vector()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var session = _db.CreateSession();
        var faces = new FaceProfileRepository(session);

        var random = new Random(42);
        float[] Vector() => Enumerable.Range(0, FaceProfile.EmbeddingSize).Select(_ => (float)(random.NextDouble() * 2 - 1)).ToArray();

        var first = new FaceProfile { UserId = ManagerId, FaceEncoding = Vector(), SampleImageUrl = "https://example.test/a.jpg", SampleImagePublicId = "flexiwork/faces/a" };
        var id = await faces.UpsertAsync(first);

        var saved = await faces.GetByUserIdAsync(ManagerId);
        Assert.NotNull(saved);
        Assert.Equal(id, saved!.Id);
        Assert.Equal(first.FaceEncoding, saved.FaceEncoding); // JSON giữ nguyên giá trị float
        Assert.Equal(FaceProfile.DefaultModelName, saved.ModelName);
        Assert.Equal("flexiwork/faces/a", saved.SampleImagePublicId);

        // Đăng ký lại: cùng bản ghi, dữ liệu mới
        var second = new FaceProfile { UserId = ManagerId, FaceEncoding = Vector(), SampleImageUrl = "https://example.test/b.jpg", SampleImagePublicId = "flexiwork/faces/b" };
        Assert.Equal(id, await faces.UpsertAsync(second));
        var replaced = await faces.GetByUserIdAsync(ManagerId);
        Assert.Equal(second.FaceEncoding, replaced!.FaceEncoding);
        Assert.Equal("https://example.test/b.jpg", replaced.SampleImageUrl);

        // Bản ghi mẫu trong seed (vector rút gọn) vẫn đọc được
        Assert.NotEmpty((await faces.GetByUserIdAsync(EmployeeId))!.FaceEncoding);

        var registered = await faces.GetRegisteredUserIdsAsync(new[] { 1, ManagerId, EmployeeId });
        Assert.Equal(new HashSet<int> { ManagerId, EmployeeId }, registered.ToHashSet());

        Assert.True(await faces.DeleteByUserIdAsync(ManagerId));
        Assert.Null(await faces.GetByUserIdAsync(ManagerId));
        Assert.False(await faces.DeleteByUserIdAsync(ManagerId));
    }
}
