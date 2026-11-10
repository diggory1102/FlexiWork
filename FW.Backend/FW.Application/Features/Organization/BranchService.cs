using FW.Application.Common.Exceptions;
using FW.Application.Common.Interfaces;
using FW.Application.Common.Interfaces.Persistence;
using FW.Application.Common.Models;
using FW.Application.Common.Security;
using FW.Domain.Entities;
using FW.Domain.Enums;

namespace FW.Application.Features.Organization;

public interface IBranchService
{
    Task<PaginatedResult<BranchDto>> GetPagedAsync(BranchFilter filter, CancellationToken cancellationToken = default);
    Task<BranchDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<BranchDto> CreateAsync(BranchRequest request, CancellationToken cancellationToken = default);
    Task<BranchDto> UpdateAsync(int id, BranchRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchWifiDto>> GetWifisAsync(int branchId, CancellationToken cancellationToken = default);
    Task<BranchWifiDto> AddWifiAsync(int branchId, BranchWifiRequest request, CancellationToken cancellationToken = default);
    Task<BranchWifiDto> UpdateWifiAsync(int branchId, int wifiId, BranchWifiRequest request, CancellationToken cancellationToken = default);
    Task DeleteWifiAsync(int branchId, int wifiId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quản lý chi nhánh, tọa độ Geofence và danh sách Wi-Fi BSSID hợp lệ.
/// Phạm vi dữ liệu: Admin/HR xem mọi chi nhánh, Manager chỉ xem chi nhánh của mình
/// (quyền ghi do Controller giới hạn cho Admin).
/// </summary>
public class BranchService : IBranchService
{
    private readonly IBranchRepository _branches;
    private readonly IBranchWifiRepository _wifis;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public BranchService(
        IBranchRepository branches,
        IBranchWifiRepository wifis,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _branches = branches;
        _wifis = wifis;
        _users = users;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    // ------------------------------------------------------------------
    // Chi nhánh
    // ------------------------------------------------------------------

    public async Task<PaginatedResult<BranchDto>> GetPagedAsync(BranchFilter filter, CancellationToken cancellationToken = default)
    {
        // Manager chỉ thấy chi nhánh của mình, bất kể tham số lọc gửi lên
        var scope = _currentUser.GetBranchScope();
        if (scope.HasValue) filter.BranchId = scope;

        var page = await _branches.GetPagedAsync(filter, cancellationToken);
        return page.Map(BranchDto.From);
    }

    public async Task<BranchDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var branch = await GetAccessibleBranchAsync(id, cancellationToken);
        var wifis = await _wifis.GetByBranchIdAsync(id, cancellationToken: cancellationToken);
        return new BranchDetailDto(BranchDto.From(branch), wifis.Select(BranchWifiDto.From).ToList());
    }

    public async Task<BranchDto> CreateAsync(BranchRequest request, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.BranchCode);
        if (await _branches.ExistsByCodeAsync(code, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Mã chi nhánh '{code}' đã tồn tại.");
        }

        var branch = new Branch { BranchCode = code };
        Apply(branch, request, defaultStatus: BranchStatus.Active);

        await _branches.AddAsync(branch, cancellationToken);
        return BranchDto.From((await _branches.GetByIdAsync(branch.Id, cancellationToken))!);
    }

    public async Task<BranchDto> UpdateAsync(int id, BranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await GetAccessibleBranchAsync(id, cancellationToken);

        var code = NormalizeCode(request.BranchCode);
        if (await _branches.ExistsByCodeAsync(code, excludeId: id, cancellationToken))
        {
            throw new BadRequestException($"Mã chi nhánh '{code}' đã tồn tại.");
        }

        branch.BranchCode = code;
        Apply(branch, request, defaultStatus: branch.Status);

        await _branches.UpdateAsync(branch, cancellationToken);
        return BranchDto.From((await _branches.GetByIdAsync(id, cancellationToken))!);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await GetAccessibleBranchAsync(id, cancellationToken);

        var staff = await _users.GetPagedAsync(new UserFilter { BranchId = id, PageSize = 1 }, cancellationToken);
        if (staff.TotalCount > 0)
        {
            throw new BadRequestException(
                $"Không thể xóa: chi nhánh đang có {staff.TotalCount} nhân sự. Hãy chuyển nhân sự sang chi nhánh khác hoặc chuyển chi nhánh sang trạng thái ngừng hoạt động.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _wifis.DeleteByBranchIdAsync(id, ct);
            await _branches.DeleteAsync(id, ct);
        }, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Wi-Fi BSSID
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<BranchWifiDto>> GetWifisAsync(int branchId, CancellationToken cancellationToken = default)
    {
        await GetAccessibleBranchAsync(branchId, cancellationToken);
        var wifis = await _wifis.GetByBranchIdAsync(branchId, cancellationToken: cancellationToken);
        return wifis.Select(BranchWifiDto.From).ToList();
    }

    public async Task<BranchWifiDto> AddWifiAsync(int branchId, BranchWifiRequest request, CancellationToken cancellationToken = default)
    {
        await GetAccessibleBranchAsync(branchId, cancellationToken);
        var bssid = await ValidateBssidAsync(branchId, request.Bssid, excludeId: null, cancellationToken);

        var wifi = new BranchWifi
        {
            BranchId = branchId,
            Ssid = request.Ssid.Trim(),
            Bssid = bssid,
            IsActive = request.IsActive
        };
        await _wifis.AddAsync(wifi, cancellationToken);
        return BranchWifiDto.From((await _wifis.GetByIdAsync(wifi.Id, cancellationToken))!);
    }

    public async Task<BranchWifiDto> UpdateWifiAsync(int branchId, int wifiId, BranchWifiRequest request, CancellationToken cancellationToken = default)
    {
        var wifi = await GetWifiOfBranchAsync(branchId, wifiId, cancellationToken);

        wifi.Ssid = request.Ssid.Trim();
        wifi.Bssid = await ValidateBssidAsync(branchId, request.Bssid, excludeId: wifiId, cancellationToken);
        wifi.IsActive = request.IsActive;

        await _wifis.UpdateAsync(wifi, cancellationToken);
        return BranchWifiDto.From((await _wifis.GetByIdAsync(wifiId, cancellationToken))!);
    }

    public async Task DeleteWifiAsync(int branchId, int wifiId, CancellationToken cancellationToken = default)
    {
        await GetWifiOfBranchAsync(branchId, wifiId, cancellationToken);
        await _wifis.DeleteAsync(wifiId, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Hàm hỗ trợ
    // ------------------------------------------------------------------

    private async Task<Branch> GetAccessibleBranchAsync(int id, CancellationToken cancellationToken)
    {
        var branch = await _branches.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Chi nhánh", id);
        _currentUser.EnsureCanAccessBranch(branch.Id);
        return branch;
    }

    private async Task<BranchWifi> GetWifiOfBranchAsync(int branchId, int wifiId, CancellationToken cancellationToken)
    {
        await GetAccessibleBranchAsync(branchId, cancellationToken);
        var wifi = await _wifis.GetByIdAsync(wifiId, cancellationToken);
        if (wifi == null || wifi.BranchId != branchId)
        {
            throw new NotFoundException("Wi-Fi", wifiId);
        }
        return wifi;
    }

    private async Task<string> ValidateBssidAsync(int branchId, string input, int? excludeId, CancellationToken cancellationToken)
    {
        if (!BssidFormat.TryNormalize(input, out var bssid))
        {
            throw new BadRequestException("BSSID không hợp lệ. Định dạng đúng: AA:BB:CC:DD:EE:FF.");
        }
        if (await _wifis.ExistsBssidAsync(branchId, bssid, excludeId, cancellationToken))
        {
            throw new BadRequestException($"BSSID {bssid} đã được khai báo cho chi nhánh này.");
        }
        return bssid;
    }

    private static void Apply(Branch branch, BranchRequest request, string defaultStatus)
    {
        var status = string.IsNullOrWhiteSpace(request.Status) ? defaultStatus : request.Status.Trim().ToLowerInvariant();
        if (!BranchStatus.IsValid(status))
        {
            throw new BadRequestException($"Trạng thái chi nhánh không hợp lệ. Giá trị cho phép: {string.Join(", ", BranchStatus.All)}.");
        }

        branch.Name = request.Name.Trim();
        branch.Address = request.Address.Trim();
        branch.Phone = NullIfEmpty(request.Phone);
        branch.Email = NullIfEmpty(request.Email);
        branch.Latitude = request.Latitude;
        branch.Longitude = request.Longitude;
        branch.Radius = request.Radius;
        branch.Status = status;
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
