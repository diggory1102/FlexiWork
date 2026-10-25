using App.Application.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BranchesController : ControllerBase
    {
        private readonly IBranchRepository _branchRepository;

        public BranchesController(IBranchRepository branchRepository)
        {
            _branchRepository = branchRepository;
        }

        /// <summary>
        /// Lấy toàn bộ danh sách chi nhánh đang hoạt động (Sử dụng Dapper tốc độ cao)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllActive()
        {
            var branches = await _branchRepository.GetAllActiveBranchesAsync();
            return Ok(new
            {
                Success = true,
                Message = "Lấy danh sách chi nhánh thành công",
                Data = branches
            });
        }

        /// <summary>
        /// Lấy chi tiết chi nhánh theo mã chi nhánh (ví dụ: CN-HN-01)
        /// </summary>
        [HttpGet("{branchCode}")]
        public async Task<IActionResult> GetByCode(string branchCode)
        {
            var branch = await _branchRepository.GetByCodeAsync(branchCode);
            if (branch == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = $"Không tìm thấy chi nhánh với mã '{branchCode}'"
                });
            }

            return Ok(new
            {
                Success = true,
                Data = branch
            });
        }

        /// <summary>
        /// Lấy danh sách Access Point Wi-Fi (BSSID) hợp lệ của chi nhánh
        /// </summary>
        [HttpGet("{id:int}/wifis")]
        public async Task<IActionResult> GetWifis(int id)
        {
            var wifis = await _branchRepository.GetActiveWifisByBranchIdAsync(id);
            return Ok(new
            {
                Success = true,
                Data = wifis
            });
        }
    }
}
