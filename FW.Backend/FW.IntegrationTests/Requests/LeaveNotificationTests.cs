using FW.Application.Features.Requests;
using FW.Domain.Entities;
using FW.Domain.Enums;
using FW.IntegrationTests.Notifications;

namespace FW.IntegrationTests.Requests;

/// <summary>Thông báo của đơn từ (Task 5.1): ai được báo khi tạo đơn, đồng nghiệp trả lời đổi ca và khi duyệt / từ chối.</summary>
public partial class LeaveRequestServiceTests
{
    [SkippableFact]
    public async Task Request_events_notify_reviewers_requester_and_colleagues()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason);
        await using var s = _db.CreateSession();
        var notes = new RecordingNotifier();
        var a = await NewEmployeeAsync(s);
        var b = await NewEmployeeAsync(s);

        // Tạo đơn nghỉ → người duyệt của chi nhánh (không báo cho ai khác)
        var leave = await As(s, a, notes: notes).CreateAsync(Leave(AnnualLeave, Day.AddDays(40), Day.AddDays(41)), null);
        var toReviewers = Assert.Single(notes.ToReviewers);
        Assert.Equal((Branch1, a.Id, NotificationType.LeaveRequest, leave.Id.ToString()), (toReviewers.BranchId, toReviewers.Requester, toReviewers.Type, toReviewers.Reference));
        Assert.Contains(a.FullName, toReviewers.Content);
        Assert.Contains("nghỉ", toReviewers.Content);
        Assert.Single(notes.Items);

        // Duyệt → người tạo đơn nhận kết quả kèm ghi chú
        notes.Items.Clear();
        await Approval(s, Manager1, notes).ReviewAsync(leave.Id, Approve("OK nhé"));
        var approved = Assert.Single(notes.To(a.Id));
        Assert.Equal((NotificationType.LeaveApproval, leave.Id.ToString()), (approved.Type, approved.Reference));
        Assert.Contains("đã được duyệt", approved.Title);
        Assert.Contains("OK nhé", approved.Content);
        Assert.Single(notes.Items);

        // Từ chối kèm lý do
        notes.Items.Clear();
        var other = await As(s, a, notes: notes).CreateAsync(Leave(AnnualLeave, Day.AddDays(45), Day.AddDays(45)), null);
        notes.Items.Clear();
        await Approval(s, Manager1, notes).ReviewAsync(other.Id, Reject("Cuối tháng thiếu người"));
        var rejected = Assert.Single(notes.To(a.Id));
        Assert.Contains("bị từ chối", rejected.Title);
        Assert.Contains("Cuối tháng thiếu người", rejected.Content);

        // Đổi ca: báo đồng nghiệp trước, chưa báo Quản lý
        notes.Items.Clear();
        var morning = await NewShiftAsync(s, 8, 12);
        var afternoon = await NewShiftAsync(s, 13, 17);
        var aMorning = await NewScheduleAsync(s, a, morning, Day.AddDays(50));
        var bAfternoon = await NewScheduleAsync(s, b, afternoon, Day.AddDays(51));
        var swap = await As(s, a, notes: notes).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "Đi thi", SourceScheduleId = aMorning.Id, TargetScheduleId = bAfternoon.Id
        }, null);
        var offer = Assert.Single(notes.To(b.Id));
        Assert.Contains("đề nghị đổi ca", offer.Title);
        Assert.Empty(notes.ToReviewers);

        // Đồng nghiệp đồng ý → báo người tạo và người duyệt
        notes.Items.Clear();
        await As(s, b, notes: notes).RespondAsPeerAsync(swap.Id, new PeerResponseRequest { Accepted = true, Note = "Ok bạn" });
        var accepted = Assert.Single(notes.To(a.Id));
        Assert.Contains("đã đồng ý", accepted.Title);
        Assert.Contains("Ok bạn", accepted.Content);
        Assert.Equal(a.Id, Assert.Single(notes.ToReviewers).Requester);

        // Duyệt đổi ca → cả hai người đều được báo
        notes.Items.Clear();
        await Approval(s, Manager1, notes).ReviewAsync(swap.Id, Approve());
        Assert.Contains("đã được duyệt", Assert.Single(notes.To(a.Id)).Title);
        Assert.Contains("Đổi ca đã được duyệt", Assert.Single(notes.To(b.Id)).Title);

        // Đồng nghiệp từ chối → chỉ báo người tạo, Quản lý không bị làm phiền
        notes.Items.Clear();
        var aLater = await NewScheduleAsync(s, a, morning, Day.AddDays(60));
        var declined = await As(s, a, notes: notes).CreateAsync(new CreateLeaveRequestForm
        {
            LeaveTypeId = Swap, Reason = "Đi khám", SourceScheduleId = aLater.Id, TargetUserId = b.Id
        }, null);
        notes.Items.Clear();
        await As(s, b, notes: notes).RespondAsPeerAsync(declined.Id, new PeerResponseRequest { Accepted = false });
        Assert.Contains("từ chối đổi ca", Assert.Single(notes.To(a.Id)).Title);
        Assert.Empty(notes.ToReviewers);
    }
}
