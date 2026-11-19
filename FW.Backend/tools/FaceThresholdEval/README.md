# FaceThresholdEval — hiệu chỉnh ngưỡng nhận diện khuôn mặt

Công cụ dòng lệnh ngoại tuyến (Task 6.2) đo **tỉ lệ nhận nhầm / từ chối nhầm** của bộ nhận diện khuôn mặt trên một bộ ảnh có nhãn người, rồi đề xuất
hai ngưỡng cấu hình:

| Cấu hình (`FaceRecognition:`) | Dùng khi | Ý nghĩa |
|---|---|---|
| `MatchThreshold` | chấm công | độ giống tối thiểu để coi ảnh chụp là đúng người đã đăng ký |
| `DuplicateThreshold` | đăng ký Face ID | từ độ giống này trở lên, khuôn mặt mới bị coi là trùng với tài khoản khác (thực tế dùng `min(DuplicateThreshold, MatchThreshold)`) |

Công cụ dùng **đúng các lớp của backend** (phát hiện SCRFD → căn chỉnh → ArcFace) nên số đo phản ánh hệ thống thật. Không nằm trong `FW.Backend.sln`
và không vào ảnh Docker.

## Chạy

```powershell
dotnet run -c Release --project FW.Backend/tools/FaceThresholdEval -- <thư mục ảnh> FW.Backend/FW.API/models/buffalo_sc --out bao-cao.md
```

Thư mục mô hình cần `det_500m.onnx` và `w600k_mbf.onnx` (xem `FW.Backend/scripts/download-face-models.ps1`).

Thư mục ảnh: **mỗi người một thư mục con**, trong đó là các ảnh của người đó.

```
anh-do/
  nguoi-01/  a.jpg  b.jpg  c.jpg ...
  nguoi-02/  a.jpg  b.jpg ...
```

Tùy chọn: `--max-per-person N` (mặc định 12), `--max-persons N`, `--det-size N` (mặc định 640), `--min-face N` (mặc định 64, bằng
`FaceRecognition:MinFaceSize`), `--out bao-cao.md`.

### Chế độ "đăng ký ảnh tốt, chấm công ảnh kém"

```powershell
... -- <ảnh gốc> <mô hình> --probe <ảnh kém> --label "thiếu sáng"
```

`<ảnh kém>` có cùng cấu trúc `người/tên ảnh` nhưng ảnh đã bị làm xấu (hoặc chụp thật trong điều kiện xấu). Chỉ ghép ảnh đăng ký với ảnh thử khác
ảnh gốc, đúng tình huống một nhân viên đăng ký trong điều kiện tốt rồi chấm công lúc ngược sáng, đông người…

## Đọc báo cáo

- **FAR** (nhận nhầm): tỉ lệ cặp ảnh *khác người* có độ giống ≥ ngưỡng — người lạ qua được.
- **FRR** (từ chối nhầm): tỉ lệ cặp ảnh *cùng người* có độ giống < ngưỡng — người thật phải chụp lại.
- **Chặn nhầm / lần đăng ký**: xác suất một nhân viên mới bị coi là trùng với ai đó trong công ty 80 người (≈ 79 · FAR).

## Thu thập ảnh thật ở Task 6.5

- Mỗi người tham gia **8–12 ảnh** chụp bằng điện thoại thật qua màn hình chấm công, thay đổi: ánh sáng (sáng, tối, ngược sáng), khoảng cách, góc mặt
  nhẹ, có / không khẩu trang hoặc kính, các ngày khác nhau.
- Càng nhiều người càng tốt (≥ 20 người cho kết quả đáng tin; cặp *khác người* tăng theo bình phương số người).
- **Không đưa ảnh khuôn mặt vào Git** (repo công khai, ảnh là dữ liệu cá nhân): để ở thư mục ngoài dự án, xin sự đồng ý của người được chụp và xóa
  khi đo xong.
- Chạy một lần cho bộ ảnh tốt, một lần cho mỗi điều kiện kém, rồi chọn ngưỡng có FAR chấp nhận được và FRR thấp nhất; ghi số liệu vào `docs/PROGRESS.md`.

## Kết quả đã có (Task 6.2)

Đo trên bộ ảnh công khai **LFW** (127 người, mỗi người 8–12 ảnh báo chí, ~1.150.000 cặp khác người) — xem `docs/PROGRESS.md`. Đây là ảnh báo chí,
không phải ảnh selfie chấm công, nên cần đo lại bằng ảnh của nhóm ở Task 6.5 trước khi chốt cuối.
