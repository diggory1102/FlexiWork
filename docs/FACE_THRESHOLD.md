# Hiệu chỉnh ngưỡng nhận diện khuôn mặt (Task 6.2)

Tài liệu ghi lại cách đo và căn cứ chọn hai ngưỡng `FaceRecognition:MatchThreshold` (chấm công) và `FaceRecognition:DuplicateThreshold`
(chặn đăng ký trùng khuôn mặt). Số liệu dùng được cho Chương 4 của khóa luận; **cần đo lại bằng ảnh chấm công thật ở Task 6.5** trước khi chốt cuối.

## 1. Phương pháp

- **Công cụ:** `FW.Backend/tools/FaceThresholdEval` — dùng đúng các lớp của backend (phát hiện SCRFD `det_500m` → căn chỉnh 5 điểm → ArcFace
  `w600k_mbf`, bộ InsightFace `buffalo_sc`), nên số đo phản ánh hệ thống thật. Hướng dẫn chạy: `FW.Backend/tools/FaceThresholdEval/README.md`.
- **Dữ liệu:** bộ ảnh công khai **LFW** (Labeled Faces in the Wild), ảnh gốc 250×250: **127 người** có từ 12 ảnh trở lên, lấy 12 ảnh mỗi người
  (**1.521 ảnh** dùng được). Cặp **cùng người** 8.349, cặp **khác người** 1.147.611. Ảnh không được đưa vào Git.
- **Chỉ số:** với mỗi cặp ảnh tính độ giống cosine của hai vector 512 chiều.
  - **FAR** (nhận nhầm) = tỉ lệ cặp *khác người* có độ giống ≥ ngưỡng → người lạ chấm công / đăng ký trùng được.
  - **FRR** (từ chối nhầm) = tỉ lệ cặp *cùng người* có độ giống < ngưỡng → người thật phải chụp lại.
- **Điều kiện ảnh kém:** đăng ký bằng ảnh gốc (tốt), rồi "chấm công" bằng ảnh đã bị làm xấu bằng Pillow: thiếu sáng (độ sáng ×0,28), ngược sáng
  (tối + tương phản thấp), nhòe (Gaussian bán kính 2,5), nén mạnh (JPEG chất lượng 12), chụp xa (thu nhỏ còn 70%). Mỗi lần dùng 8 ảnh/người.

## 2. Kết quả

Phân phối độ giống trên ảnh tốt: cặp cùng người có trung vị **0,613** (5% thấp nhất ≤ 0,412); cặp khác người có trung vị **0,008** (99% ≤ 0,173).

| Ngưỡng | FAR | FRR | Chặn nhầm / lần đăng ký (80 nhân viên) | Bắt được cặp cùng người |
|---|---|---|---|---|
| 0,25 | 0,0798% | 1,22% | 6,11% | 98,8% |
| 0,30 | 0,0178% | 1,66% | 1,39% | 98,3% |
| **0,35** | **0,0046%** | **2,59%** | **0,36%** | **97,4%** |
| 0,40 | 0,0024% | 4,26% | 0,19% | 95,7% |
| 0,45 | 0,0019% | 8,31% | 0,15% | 91,7% |
| 0,50 | 0,0018% | 16,18% | 0,14% | 83,8% |

FAR / FRR của chấm công theo điều kiện ảnh (đăng ký ảnh tốt, chấm công ảnh kém):

| Điều kiện ảnh | FAR @0,35 | FRR @0,35 | FAR @0,40 | FRR @0,40 | FAR @0,45 | FRR @0,45 | Ảnh bị loại khi dò mặt |
|---|---|---|---|---|---|---|---|
| Ảnh tốt | 0,0046% | 2,59% | 0,0024% | 4,26% | 0,0019% | 8,31% | 0 |
| Thiếu sáng | 0,0067% | 3,39% | 0,0041% | 6,67% | 0,0036% | 12,76% | 3 |
| Ngược sáng | 0,0070% | 3,36% | 0,0041% | 6,68% | 0,0036% | 12,48% | 3 |
| Nhòe | 0,0043% | 8,15% | 0,0029% | 16,51% | 0,0028% | 28,78% | 4 |
| Nén mạnh | 0,0044% | 6,49% | 0,0030% | 12,53% | 0,0025% | 24,01% | 3 |
| Chụp xa (mặt ~77 px) | 0,0052% | 2,16% | 0,0038% | 3,96% | 0,0035% | 8,68% | 155 |

Chụp xa hơn nữa (thu nhỏ còn 45%, mặt ~50 px): **100%** ảnh thử bị từ chối vì mặt nhỏ hơn `MinFaceSize` = 64 px (app yêu cầu chụp gần hơn).

## 3. Kết luận và quyết định (19/11/2026)

1. **`MatchThreshold` 0,40 → 0,35.**
   - FAR gần như **phẳng từ 0,35 đến 0,50** (0,002–0,007% ở mọi điều kiện) rồi **tăng gấp ~4 lần khi xuống 0,30** → 0,35 là điểm "khuỷu" của đường cong.
   - Ở 0,35 người thật bị từ chối **ít bằng một nửa** so với 0,40 khi ảnh kém (nhòe 8,2% so với 16,5%; nén mạnh 6,5% so với 12,5%), trong khi
     người lạ lọt qua vẫn chỉ khoảng 1/20.000 cặp.
   - Công cụ cũng đề xuất ngưỡng thấp nhất để FAR ≤ 0,01% là 0,32; 0,35 nằm trên mức đó.
2. **`DuplicateThreshold` 0,50 → 0,35 và luôn `≤ MatchThreshold`.** Nếu khuôn mặt mới đủ giống khuôn mặt đã đăng ký của tài khoản khác để *qua được*
   chấm công của tài khoản đó thì phải coi là trùng. Với 0,50 cũ, 16% cặp "một người hai tài khoản" lọt qua; với 0,35 chỉ 2,6%, đổi lại tỉ lệ chặn
   nhầm tăng từ 0,14% lên 0,36% mỗi lần đăng ký (≈ 0,3 người bị chặn nhầm trên 80 nhân viên). Mã dùng `EffectiveDuplicateThreshold = min(Duplicate, Match)`
   nên chỉnh `MatchThreshold` xuống thì ngưỡng chặn trùng tự hạ theo.
3. Ảnh nhòe và nén mạnh là điều kiện làm FRR tăng nhiều nhất → trên app nên nhắc giữ máy ổn định, và Quản lý có sẵn luồng duyệt lượt chấm công bị
   từ chối nhầm ở UC 4.6.

## 4. Giới hạn (cần nói rõ trong khóa luận)

- LFW là **ảnh báo chí của người nổi tiếng**, không phải ảnh selfie chấm công: tư thế, ánh sáng, biểu cảm đa dạng hơn (làm FRR *cao hơn* thực tế), nhưng
  không có các yếu tố riêng của cửa hàng (khẩu trang, kính, đồng phục, camera điện thoại rẻ). Phải đo lại ở Task 6.5.
- **Cặp "khác người" trong LFW là ngẫu nhiên.** Họ hàng, anh em sinh đôi, người có nét giống nhau cho độ giống 0,4–0,8 và *không* bị chặn bởi bất kỳ ngưỡng
  hợp lý nào; một vài cặp "khác người" độ giống rất cao trong LFW có thể là nhãn nhầm của chính bộ dữ liệu. Hai người như vậy cùng làm ở một cửa hàng
  sẽ bị chặn đăng ký trùng, hiện chưa có luồng để Nhân sự cho phép ngoại lệ.
- Chưa có kiểm tra **người thật (liveness)**: ảnh in hoặc ảnh trên màn hình của chính người đó vẫn cho độ giống cao.
- Làm xấu ảnh bằng Pillow chỉ xấp xỉ điều kiện thật (không có nhiễu cảm biến, rung tay, ánh sáng nhiều nguồn).
