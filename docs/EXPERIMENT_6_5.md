# Task 6.5 — Thực nghiệm chấm công trên thiết bị thật

Tài liệu này là **quy trình** để nhóm đi thử trên điện thoại thật và thu số liệu cho Chương 4. Phần mềm đã sẵn sàng: mọi lượt bấm chấm công (kể cả bị từ chối) được ghi vào bảng `attendance_attempts`, và công cụ `ExperimentReport` biến số liệu đó thành bảng cho báo cáo.

| Việc | Ai làm | Trạng thái |
|---|---|---|
| Ghi nhật ký mọi lượt chấm công + thời gian xử lý (migration 017) | Mã nguồn | Xong |
| Công cụ thống kê `FW.Backend/tools/ExperimentReport` | Mã nguồn | Xong |
| Đi thử bằng điện thoại, nhiều người, nhiều điều kiện | **Bạn / nhóm** | Chưa làm |
| Đo lại ngưỡng khuôn mặt bằng ảnh thật | **Bạn / nhóm** | Chưa làm |
| Đối soát bảng công | **Bạn** (mục 6) | Chưa làm |
| Bộ ~50 câu hỏi cho Trợ lý AI | Chờ Cấp 7 (chưa có Trợ lý AI để đo) | Hoãn |

## 1. Chuẩn bị (làm một lần)

1. **Chạy migration 017 lên TiDB** (`docs/DEPLOY.md`, bảng migration), rồi `git push` để Render triển khai bản backend mới. Thiếu một trong hai thì lượt chấm công vẫn chạy bình thường nhưng **không có số liệu** (lỗi ghi nhật ký chỉ cảnh báo trong log, không làm hỏng chấm công).
2. **Chi nhánh thử nghiệm** có tọa độ và bán kính đúng; khai báo **Wi-Fi nội bộ** (BSSID thật của router tại chỗ). Ghi lại bán kính để tính khoảng cách cho S2–S4.
3. **Tài khoản người thử:** mỗi người một tài khoản thật, **đã đăng ký Face ID** trong điều kiện tốt (đủ sáng, nhìn thẳng), và có **ca làm đã công bố** trong ngày thử (khung check-in mở trước giờ bắt đầu 60 phút). Dùng ca dài để thử được nhiều lượt: sau khi check-in, check-out; muốn thử lại thì nhờ Quản lý/HR tạo ca mới hoặc dùng ca ngày khác.
4. **APK mới nhất** cài trên Android (cần APK, không phải Expo Go, để đọc được Wi-Fi). iPhone chạy Expo Go **không đọc được BSSID** và không có cờ phát hiện Fake GPS — ghi rõ điều này vào báo cáo nếu có thử iPhone.
5. **Đánh thức server** (Render ngủ sau ~15 phút): mở trang web hoặc `/health` khoảng 1 phút trước khi thử, nếu không lượt đầu sẽ chậm cả chục giây và làm lệch số đo thời gian. Muốn đo luôn trường hợp này thì ghi riêng một dòng kịch bản `W1` (xem `note`).
6. **Cỡ mẫu tối thiểu** để số liệu đáng tin: ≥ 3 điện thoại khác hãng, ≥ 8 người; mỗi kịch bản ≥ 5 lần. Riêng khuôn mặt: ≥ 100 lượt người thật, ≥ 50 lượt người lạ. (Nếu 50 lượt người lạ không lọt lượt nào thì chỉ kết luận được “FAR dưới khoảng 6% ở mức tin cậy 95%”, không phải “bằng 0”.)

## 2. Kịch bản thử

`expected` là kết quả **mong đợi** của thiết kế; thực tế khác kỳ vọng chính là phát hiện đáng ghi vào báo cáo. `expected_stage` chỉ điền khi `expected=rejected` (lớp phải chặn).

| Mã | Làm gì | expected | expected_stage |
|---|---|---|---|
| S1 | Đứng trong chi nhánh, bắt đúng Wi-Fi, chính chủ chụp | `accepted` | |
| S2 | Như S1 nhưng đứng sát **mép trong** bán kính (cách tâm ≈ bán kính − 10 m) | `accepted` | |
| S3 | Cách chi nhánh **100–200 m** (ngoài bán kính), còn lại như S1 | `rejected` | `gps` |
| S4 | Đứng sát **mép ngoài** (≈ bán kính + 15 m) — đo độ nhạy, GPS có thể trôi nên ghi lại thực tế | `rejected` | `gps` |
| S5 | Bật **vị trí giả lập** (Android: Cài đặt nhà phát triển → chọn ứng dụng vị trí giả lập, dùng app Fake GPS) đặt đúng tọa độ chi nhánh | `rejected` | `validate` |
| S6 | Tắt định vị hoặc từ chối quyền vị trí | `rejected` | `validate` * |
| S7 | GPS yếu (trong nhà kín / hầm, sai số > 100 m) | `rejected` | `gps` |
| S8 | Đúng vị trí, chính chủ, nhưng nối **Wi-Fi khác** (quán bên cạnh, hotspot) | `pending_approval` | |
| S9 | Đúng vị trí, chính chủ, **tắt Wi-Fi** (chỉ 4G) | `pending_approval` | |
| S10 | **Người khác chụp hộ:** B đăng nhập tài khoản của A rồi chụp **mặt B** tại chi nhánh | `rejected` | `face` |
| S11 | **Ảnh giả:** chụp ảnh in / ảnh trên màn hình của A (cho tài khoản A) | `rejected` | `liveness` ** |
| S12 | Bấm check-in **ngoài khung giờ** của ca (quá sớm / ca đã kết thúc) | `rejected` | `time` |
| S13 | **Một điện thoại chấm công hộ hai người** cùng ngày: A check-in xong, B đăng nhập cùng máy và check-in (B chính chủ, đúng vị trí) | `pending_approval` | |
| F1–F5 | **Điều kiện ảnh của người thật** (chính chủ, đúng vị trí, đúng Wi-Fi): F1 thiếu sáng, F2 ngược sáng, F3 đeo khẩu trang, F4 đeo kính (kính râm), F5 mặt nghiêng ~30° / ở xa ~1 m | `accepted` | |

\* S6: ứng dụng chặn ngay trên điện thoại, **không gửi** yêu cầu lên máy chủ nên không có dòng nào trong hệ thống. Trong phiếu vẫn ghi `expected=rejected`; báo cáo sẽ báo “Không thấy lượt trên hệ thống” — với S6 đó chính là kết quả **đúng**.

\*\* S11: hệ thống **chưa có chống giả mạo (liveness)** nên dự kiến lượt này **lọt**. Dùng `expected_stage=liveness` (lớp chưa tồn tại) để báo cáo gom vào dòng “Lọt” như một **hạn chế đã biết** và **không** tính vào nhóm “người lạ” khi đo FAR (xem `docs/FACE_THRESHOLD.md`, mục Giới hạn).

Lưu ý khi thử S5: ứng dụng chỉ đọc cờ giả lập do **Android** báo; một số app Fake GPS “root” có thể không bị phát hiện — nếu lọt, ghi nhận y như vậy.

## 3. Cách ghi phiếu khi đi thử

Mỗi lần bấm chấm công, người thử (hoặc người đi cùng) ghi **một dòng** vào Google Sheets / Excel rồi xuất CSV. File mẫu: [`docs/experiment-sheet-template.csv`](experiment-sheet-template.csv).

| Cột | Bắt buộc | Ý nghĩa |
|---|---|---|
| `scenario` | có | Mã kịch bản (S1…S13, F1…F5, W1) |
| `employee_code` | có | Mã nhân viên của **tài khoản đăng nhập** (không phải người đứng trước camera) |
| `time` | có | Giờ bấm, `yyyy-MM-dd HH:mm` (giờ Việt Nam; lệch ≤ 3 phút là ghép được) |
| `expected` | có | `accepted`, `pending_approval` hoặc `rejected` (theo bảng ở mục 2) |
| `expected_stage` | không | `validate`, `time`, `gps`, `face`, `wifi`, `save`, hoặc `liveness` (S11) |
| `device` | không | Tên máy (VD `Samsung A54`) — để so sánh giữa các máy |
| `client_ms` | không | Thời gian từ lúc bấm “Gửi” tới lúc thấy kết quả, đo bằng đồng hồ bấm giờ trên điện thoại thứ hai (ms). Cho số **toàn trình**, gồm cả mạng |
| `note` | không | Điều kiện đặc biệt: ánh sáng, vị trí đứng, ai chụp, “lượt đầu sau khi server ngủ” |

Mẹo: ghi cả lượt **bị từ chối** — đó mới là số liệu về an toàn. Đừng bỏ lượt thất bại vì “bấm nhầm”.

## 4. Thu số liệu và tạo báo cáo

Chạy trên máy của bạn (PowerShell). Chuỗi kết nối TiDB **gõ vào biến môi trường của cửa sổ này**, không lưu vào file, không dán vào chat:

```powershell
$env:FW_REPORT_DB = "<chuỗi kết nối TiDB của bạn>"
dotnet run -c Release --project FW.Backend/tools/ExperimentReport -- `
  --from 2026-12-01 --to 2026-12-03 --sheet phieu-ghi.csv --out ket-qua-thi-nghiem
Remove-Item Env:FW_REPORT_DB
```

Công cụ **chỉ đọc** CSDL. Ra hai file trong thư mục `--out`:

- `bao-cao.md` — bảng sẵn để chép vào Chương 4: kết quả theo lớp chặn, thời gian xử lý (trung bình / trung vị / P95), FRR–FAR theo từng ngưỡng khuôn mặt, đối chiếu phiếu ghi (đúng kỳ vọng / **chặn nhầm** / **lọt** / không thấy lượt), danh sách lượt sai lệch kèm lý do.
- `luot-cham-cong.csv` — dữ liệu thô từng lượt (để vẽ biểu đồ).

Không có `--sheet` vẫn chạy được, nhưng chỉ có mục 1–3 và không tính được FRR/FAR. Thư mục kết quả có mã nhân viên và mã thiết bị: **đừng đẩy lên Git** nếu chưa ẩn danh.

### Cách đọc kết quả

- **Chặn nhầm** = người hợp lệ bị từ chối → ảnh hưởng trải nghiệm. **Lọt** = gian lận / sai quy định vẫn được ghi nhận → ảnh hưởng độ tin cậy. Hai loại này nên được tách riêng khi viết Chương 4.
- Thời gian **máy chủ** (`duration_ms`) gồm nhận diện khuôn mặt và tải ảnh lên kho ảnh; **toàn trình** (`client_ms`) cộng thêm chụp ảnh, nén, mạng 4G. Báo cả hai.
- Lượt đầu tiên sau khi Render ngủ chậm bất thường — tách riêng hoặc loại khỏi trung bình và nói rõ.

## 5. Đo lại ngưỡng khuôn mặt

Có hai nguồn số liệu bổ sung cho nhau:

1. **Từ chính các lượt thử** (mục 4): bảng FRR/FAR theo ngưỡng 0,20–0,60 dùng điểm `face_score` thực tế của S1, S2, F1–F5 (người thật) và S10 (người lạ). Đây là số liệu **sát thực tế nhất** nhưng cỡ mẫu nhỏ.
2. **Từ bộ ảnh của nhóm** bằng `FW.Backend/tools/FaceThresholdEval` (hướng dẫn thu thập ảnh trong README của tool): mỗi người 8–12 ảnh selfie chụp bằng điện thoại, nhiều điều kiện, ≥ 20 người; xin phép người được chụp, **không đưa ảnh vào Git**, xóa khi đo xong. Mẫu lớn hơn nên dùng để **chốt** ngưỡng.

Nếu số đo cho thấy nên đổi ngưỡng (hiện `MatchThreshold = 0,35`), sửa cấu hình `FaceRecognition:MatchThreshold` trên Render rồi chạy lại S1, S10, F1–F5. Ghi vào `docs/FACE_THRESHOLD.md`: số đo mới, ngưỡng chọn, lý do.

## 6. Đối soát bảng công

Mục tiêu: chứng minh bảng công do hệ thống tính **khớp** với tính tay từ giờ vào/ra thực tế.

1. Chọn 3–5 người thử đã có đủ lượt check-in và check-out hợp lệ (được ghi nhận, đã duyệt các lượt `pending_approval`).
2. Tính tay từng ca vào bảng sau, dùng **giờ vào/ra mà người thử ghi nhận lúc thử** và khung giờ của ca (giờ làm chỉ tính trong khung ca, trừ giờ nghỉ theo quy tắc của hệ thống; ví dụ ca 08:00–17:00 nghỉ 60 phút, vào 08:07:30, ra 16:50 → 7,74 giờ):

   | NV | Ngày | Ca | Vào thực tế | Ra thực tế | Muộn (tay) | Sớm (tay) | Giờ làm (tay) |
   |---|---|---|---|---|---:|---:|---:|

3. Trên Web Admin: **Chấm công → Bảng công → Tính** cho tháng thử, mở chi tiết từng người, đọc số ca, phút muộn, phút về sớm, giờ làm, số công.
4. Lập bảng đối chiếu `tay` ↔ `hệ thống` cho từng ca và từng người (tổng công tháng). Mọi chênh lệch phải **giải thích được** (làm tròn, ca bị trừ nghỉ, lượt chờ duyệt chưa được duyệt…) hoặc ghi là lỗi cần sửa.
5. Thử thêm hai tình huống bảng công thường sai: một ca **quên check-out** (Quản lý điều chỉnh giờ ra) và một lượt `pending_approval` **bị từ chối** (không được tính công).

## 7. Việc hoãn lại: bộ câu hỏi cho Trợ lý AI

Phần “~50 câu hỏi mẫu, đo tỉ lệ trích xuất đúng ngày/ca/loại đơn, thời gian phản hồi, chi phí mỗi lượt” **cần Trợ lý AI** (Cấp 7) đã chạy. Làm sau khi Cấp 7 xong: soạn bộ câu kèm đáp án chuẩn, chạy từng câu, đo ba chỉ số trên và ghi vào Chương 4 cùng với số liệu ở trên.

## 8. Giới hạn cần nêu thẳng trong báo cáo

- Cỡ mẫu nhỏ (vài người, vài máy) — kết quả mang tính minh họa, không phải kiểm thử thống kê quy mô lớn.
- Chưa có liveness: ảnh in / màn hình của chính chủ có thể qua được lớp khuôn mặt.
- Fake GPS chỉ phát hiện được khi hệ điều hành báo cờ giả lập (Android); iOS không có cờ này.
- Lớp Wi-Fi không bắt buộc (không đọc được BSSID chỉ chuyển sang chờ duyệt), và iPhone chạy Expo Go không đọc được BSSID.
- Số đo thời gian phụ thuộc mạng 4G tại thời điểm thử và việc Render có đang “ngủ” hay không.
