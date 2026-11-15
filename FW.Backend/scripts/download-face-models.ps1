# Tải bộ mô hình nhận diện khuôn mặt InsightFace buffalo_sc (det_500m.onnx + w600k_mbf.onnx) cho môi trường phát triển.
# Chạy từ thư mục FW.Backend:  powershell -ExecutionPolicy Bypass -File scripts\download-face-models.ps1
# Mô hình của InsightFace chỉ dùng cho mục đích nghiên cứu / phi thương mại — phù hợp phạm vi khóa luận.
$ErrorActionPreference = 'Stop'
$url = 'https://github.com/deepinsight/insightface/releases/download/v0.7/buffalo_sc.zip'
$sha256 = '57D31B56B6FFA911C8A73CFC1707C73CAB76EFE7F13B675A05223BF42DE47C72'
$target = Join-Path $PSScriptRoot '..\FW.API\models\buffalo_sc'
$zip = Join-Path ([IO.Path]::GetTempPath()) 'buffalo_sc.zip'

New-Item -ItemType Directory -Force $target | Out-Null
Invoke-WebRequest -Uri $url -OutFile $zip
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $sha256) { throw 'Sai mã băm SHA-256, file tải về không đúng.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    foreach ($name in 'det_500m.onnx', 'w600k_mbf.onnx') {
        $entry = $archive.Entries | Where-Object Name -eq $name
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $target $name), $true)
    }
} finally { $archive.Dispose() }
Remove-Item $zip
Write-Host "Đã tải mô hình vào $((Resolve-Path $target).Path)"
