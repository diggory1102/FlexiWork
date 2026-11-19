<#
.SYNOPSIS
  Kiểm tra nhanh hệ thống FlexiWork đã triển khai (Task 6.3). Chỉ ĐỌC: không đăng nhập, không ghi dữ liệu, không cần mật khẩu.

.DESCRIPTION
  Kiểm tra: API còn sống ở chế độ Production; CORS chỉ nhận đúng domain Web; Swagger đã tắt; API và các hub SignalR đòi đăng nhập; Web trả về trang.
  Render gói free ngủ sau ~15 phút nên lần gọi đầu có thể mất ~50 giây.

.EXAMPLE
  powershell -File FW.Backend\scripts\smoke-prod.ps1
  powershell -File FW.Backend\scripts\smoke-prod.ps1 -Api https://flexiwork-api.onrender.com -Web https://flexiwork-tlu.vercel.app
#>
param(
    [string]$Api = 'https://flexiwork-api.onrender.com',
    [string]$Web = 'https://flexiwork-tlu.vercel.app'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$failed = 0

function Invoke-Raw([string]$Method, [string]$Url, [hashtable]$Headers = @{}) {
    try {
        $r = Invoke-WebRequest -Method $Method -Uri $Url -Headers $Headers -UseBasicParsing -TimeoutSec 120 -MaximumRedirection 0
        return @{ Status = [int]$r.StatusCode; Headers = $r.Headers; Body = $r.Content }
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { return @{ Status = 0; Headers = @{}; Body = $_.Exception.Message } }
        return @{ Status = [int]$resp.StatusCode; Headers = $resp.Headers; Body = '' }
    }
}

function Check([string]$Name, [bool]$Ok, [string]$Detail) {
    if ($Ok) { Write-Host ("  [ĐẠT] {0}" -f $Name) -ForegroundColor Green }
    else { Write-Host ("  [LỖI] {0} — {1}" -f $Name, $Detail) -ForegroundColor Red; $script:failed++ }
}

Write-Host "API: $Api"
Write-Host "Web: $Web"
Write-Host ''

$health = Invoke-Raw 'GET' "$Api/api/healthcheck"
Check 'API còn sống (HTTP 200)' ($health.Status -eq 200) "HTTP $($health.Status) — Render có thể đang khởi động, thử lại sau 1 phút"
Check 'Chạy ở chế độ Production' ($health.Body -match '"environment":"Production"') 'biến ASPNETCORE_ENVIRONMENT trên Render chưa là Production'

$cors = Invoke-Raw 'OPTIONS' "$Api/api/auth/login" @{ Origin = $Web; 'Access-Control-Request-Method' = 'POST'; 'Access-Control-Request-Headers' = 'content-type,authorization' }
Check 'CORS cho phép domain Web' ($cors.Headers['Access-Control-Allow-Origin'] -eq $Web) "Cors__AllowedOrigins__0 trên Render phải đúng $Web (không có dấu / cuối)"

$evil = Invoke-Raw 'OPTIONS' "$Api/api/auth/login" @{ Origin = 'https://evil.example'; 'Access-Control-Request-Method' = 'POST' }
Check 'CORS từ chối domain lạ' (-not $evil.Headers['Access-Control-Allow-Origin']) 'API cho phép mọi domain — kiểm tra Cors__AllowedOrigins'

$swagger = Invoke-Raw 'GET' "$Api/swagger/index.html"
Check 'Swagger đã tắt' ($swagger.Status -eq 404) "HTTP $($swagger.Status) — đặt Swagger__Enabled = false trên Render"

$users = Invoke-Raw 'GET' "$Api/api/users"
Check 'API đòi đăng nhập (401)' ($users.Status -eq 401) "HTTP $($users.Status)"

foreach ($hub in 'chat', 'notifications') {
    $neg = Invoke-Raw 'POST' "$Api/hubs/$hub/negotiate?negotiateVersion=1"
    Check "Hub SignalR /$hub đòi đăng nhập (401)" ($neg.Status -eq 401) "HTTP $($neg.Status)"
}

$site = Invoke-Raw 'GET' "$Web/"
Check 'Web trả về trang (HTTP 200)' ($site.Status -eq 200) "HTTP $($site.Status)"
$route = Invoke-Raw 'GET' "$Web/schedules"
Check 'Web mở thẳng được đường dẫn con (rewrite SPA)' ($route.Status -eq 200) "HTTP $($route.Status) — kiểm tra vercel.json"

Write-Host ''
if ($failed -eq 0) { Write-Host 'Tất cả kiểm tra đều đạt.' -ForegroundColor Green; exit 0 }
Write-Host "$failed kiểm tra lỗi." -ForegroundColor Red
exit 1
