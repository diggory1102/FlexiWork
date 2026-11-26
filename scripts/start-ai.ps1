<#
.SYNOPSIS
    Khởi động trợ lý AI trên máy cá nhân: Ollama -> Caddy (kiểm tra khóa API) -> Cloudflare Tunnel (docs/DEPLOY.md, Bước 8).
.DESCRIPTION
    Chạy lại được nhiều lần: thành phần nào đang chạy thì giữ nguyên (địa chỉ đường hầm không đổi).
    Cần làm một lần trước đó: cài Ollama / Caddy / cloudflared, đặt biến môi trường người dùng OLLAMA_PROXY_KEY,
    tạo file %USERPROFILE%\ollama-proxy\Caddyfile. Script không in khóa ra màn hình.
.EXAMPLE
    scripts\start-ai.cmd            Khởi động và chép địa chỉ đường hầm vào clipboard
.EXAMPLE
    scripts\start-ai.cmd -Stop      Tắt Caddy và đường hầm (Ollama giữ nguyên)
#>
[CmdletBinding()]
param(
    [switch]$Stop,
    [string]$ProxyDir = (Join-Path $env:USERPROFILE 'ollama-proxy'),
    [int]$ProxyPort = 11435,
    [int]$MetricsPort = 20241,
    [string]$Model = 'qwen2.5:7b',
    [switch]$NoClipboard
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
try { [Console]::OutputEncoding = [Text.Encoding]::UTF8 } catch {}   # tiếng Việt hiển thị đúng
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$OllamaUrl = 'http://127.0.0.1:11434'
$ProxyUrl = "http://127.0.0.1:$ProxyPort"

function Write-Step($Text) { Write-Host "`n== $Text" -ForegroundColor Cyan }
function Write-Ok($Text) { Write-Host "   [OK] $Text" -ForegroundColor Green }
function Write-Note($Text) { Write-Host "   [!]  $Text" -ForegroundColor Yellow }
function Stop-WithError($Text) { Write-Host "`n[LỖI] $Text" -ForegroundColor Red; exit 1 }

function Test-Port($Port) {
    [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

# Mã HTTP của một yêu cầu GET (0 = không kết nối được)
function Get-HttpStatus($Url, $Key) {
    $headers = @{}
    if ($Key) { $headers['Authorization'] = "Bearer $Key" }
    try {
        return [int](Invoke-WebRequest -Uri $Url -Headers $headers -UseBasicParsing -TimeoutSec 15).StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

function Wait-Until([scriptblock]$Condition, [int]$Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        if (& $Condition) { return $true }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    return $false
}

function Get-TunnelHost {
    try { return (Invoke-RestMethod "http://127.0.0.1:$MetricsPort/quicktunnel" -TimeoutSec 3).hostname } catch { return $null }
}

if ($Stop) {
    Write-Step 'Tắt đường hầm và Caddy'
    foreach ($item in @(@{ Port = $MetricsPort; Name = 'cloudflared' }, @{ Port = $ProxyPort; Name = 'caddy' })) {
        $owners = @(Get-NetTCPConnection -LocalPort $item.Port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        $found = @($owners | ForEach-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue } | Where-Object { $_.ProcessName -eq $item.Name })
        if ($found.Count -eq 0) { Write-Note "$($item.Name) không chạy."; continue }
        $found | Stop-Process -Force
        Write-Ok "Đã tắt $($item.Name)."
    }
    exit 0
}

# ------------------------------------------------------------------ 0. Chuẩn bị
Write-Step 'Kiểm tra cấu hình'
# Cửa sổ mở từ trước khi cài đặt có thể còn PATH cũ -> đọc lại từ hệ thống
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
foreach ($name in 'caddy', 'cloudflared') {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { Stop-WithError "Chưa cài $name (docs/DEPLOY.md, Bước 8)." }
}
$key = [Environment]::GetEnvironmentVariable('OLLAMA_PROXY_KEY', 'User')
if (-not $key) { Stop-WithError 'Chưa có biến môi trường người dùng OLLAMA_PROXY_KEY (docs/DEPLOY.md, Bước 8).' }
$env:OLLAMA_PROXY_KEY = $key   # tiến trình Caddy mở từ đây nhận đúng khóa hiện tại
$caddyfile = Join-Path $ProxyDir 'Caddyfile'
if (-not (Test-Path $caddyfile)) { Stop-WithError "Không thấy $caddyfile (docs/DEPLOY.md, Bước 8)." }
Write-Ok 'Caddy, cloudflared, khóa API và Caddyfile đã sẵn sàng.'

# ------------------------------------------------------------------ 1. Ollama
Write-Step 'Ollama'
if ((Get-HttpStatus "$OllamaUrl/api/tags") -ne 200) {
    $app = Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama app.exe'
    if (-not (Test-Path $app)) { Stop-WithError 'Ollama chưa chạy và không tìm thấy ứng dụng. Mở Ollama từ menu Start rồi chạy lại.' }
    Start-Process $app
    if (-not (Wait-Until { (Get-HttpStatus "$OllamaUrl/api/tags") -eq 200 } 60)) { Stop-WithError 'Ollama không phản hồi sau 60 giây.' }
}
$models = @((Invoke-RestMethod "$OllamaUrl/api/tags" -TimeoutSec 15).models.name)
if ($models -notcontains $Model) { Stop-WithError "Chưa có mô hình $Model. Chạy: ollama pull $Model" }
Write-Ok "Đang chạy, đã có mô hình $Model."

# ------------------------------------------------------------------ 2. Caddy
Write-Step "Caddy (cổng $ProxyPort)"
if (Test-Port $ProxyPort) {
    Write-Ok 'Đã chạy sẵn.'
} else {
    Start-Process caddy -ArgumentList 'run', '--config', "`"$caddyfile`"", '--adapter', 'caddyfile' -WorkingDirectory $ProxyDir -WindowStyle Minimized
    if (-not (Wait-Until { Test-Port $ProxyPort } 20)) { Stop-WithError "Caddy không mở được cổng $ProxyPort. Chạy tay 'caddy run' trong $ProxyDir để xem lỗi." }
    Write-Ok 'Đã khởi động (cửa sổ thu nhỏ).'
}
$withoutKey = Get-HttpStatus "$ProxyUrl/api/tags"
$withKey = Get-HttpStatus "$ProxyUrl/api/tags" $key
if ($withoutKey -ne 401) { Stop-WithError "Cổng $ProxyPort trả $withoutKey khi không có khóa (phải là 401). KHÔNG mở đường hầm - kiểm tra lại Caddyfile." }
if ($withKey -ne 200) { Stop-WithError "Cổng $ProxyPort trả $withKey khi có khóa (phải là 200). Nếu vừa đổi khóa: chạy 'start-ai.cmd -Stop' rồi chạy lại." }
Write-Ok 'Không khóa -> 401, đúng khóa -> 200.'

# ------------------------------------------------------------------ 3. Đường hầm
Write-Step 'Cloudflare Tunnel'
$tunnelHost = Get-TunnelHost
if ($tunnelHost) {
    Write-Ok 'Đã chạy sẵn, giữ nguyên địa chỉ.'
} else {
    if (Test-Port $MetricsPort) { Stop-WithError "Cổng $MetricsPort đang bị chiếm nhưng không trả địa chỉ đường hầm. Đóng cửa sổ cloudflared cũ rồi chạy lại." }
    Start-Process cloudflared -ArgumentList 'tunnel', '--url', $ProxyUrl, '--metrics', "127.0.0.1:$MetricsPort" -WindowStyle Minimized
    if (-not (Wait-Until { [bool](Get-TunnelHost) -and (Get-HttpStatus "http://127.0.0.1:$MetricsPort/ready") -eq 200 } 60)) {
        Stop-WithError 'Không tạo được đường hầm sau 60 giây. Kiểm tra mạng, xem cửa sổ cloudflared.'
    }
    $tunnelHost = Get-TunnelHost
    Write-Ok 'Đã khởi động (cửa sổ thu nhỏ).'
    Start-Sleep -Seconds 8   # tên miền mới cần vài giây mới phân giải được
}
$publicUrl = "https://$tunnelHost"
if (Wait-Until { (Get-HttpStatus "$publicUrl/api/tags" $key) -eq 200 } 45) {
    Write-Ok 'Gọi từ ngoài vào thành công.'
} else {
    Write-Note 'Máy này chưa gọi được địa chỉ mới (DNS có thể chậm). Đợi 1-2 phút rồi thử trợ lý trên Web.'
}

# ------------------------------------------------------------------ 4. Nạp sẵn mô hình
Write-Step 'Nạp mô hình vào GPU (lần đầu có thể mất 1-2 phút)'
try {
    $body = @{ model = $Model; keep_alive = '30m' } | ConvertTo-Json
    Invoke-RestMethod "$OllamaUrl/api/generate" -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 240 | Out-Null
    Write-Ok 'Mô hình đã sẵn sàng.'
} catch {
    Write-Note "Chưa nạp được: $($_.Exception.Message). Câu hỏi đầu tiên sẽ chậm."
}

# ------------------------------------------------------------------ 5. Kết quả
$lastFile = Join-Path $ProxyDir 'last-url.txt'
$last = if (Test-Path $lastFile) { (Get-Content $lastFile -Raw).Trim() } else { '' }
if (-not $NoClipboard) { Set-Clipboard -Value $publicUrl }

Write-Host "`n------------------------------------------------------------"
Write-Host "Địa chỉ đường hầm: $publicUrl" -ForegroundColor White
if ($publicUrl -eq $last) {
    Write-Host 'Địa chỉ KHÔNG đổi so với lần trước - không cần sửa Render (nếu lần trước bạn đã cập nhật).' -ForegroundColor Green
} else {
    Write-Host 'Địa chỉ MỚI - cần cập nhật Render:' -ForegroundColor Yellow
    if (-not $NoClipboard) { Write-Host '  (đã chép sẵn, chỉ việc dán bằng Ctrl+V)' }
    Write-Host '  1. https://dashboard.render.com  ->  flexiwork-api  ->  Environment'
    Write-Host '  2. Sửa Ollama__BaseUrl = địa chỉ trên, lưu và deploy, chờ trạng thái Live'
}
Write-Host 'Giữ hai cửa sổ caddy và cloudflared (đang thu nhỏ) và không để máy ngủ.'
Write-Host 'Tắt khi dùng xong: scripts\start-ai.cmd -Stop'
Set-Content -Path $lastFile -Value $publicUrl -Encoding ascii
