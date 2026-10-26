using App.Application.DTOs.Attendance;
using System.Threading.Tasks;

namespace App.Application.Interfaces.Services
{
    public interface IAttendanceService
    {
        Task<AttendanceResponseDto> ProcessCheckinAsync(CheckinRequestDto request);
        Task<AttendanceResponseDto> ProcessCheckoutAsync(CheckoutRequestDto request);
    }
}
