# FlexiWork - Nhật ký Dự án & Kiến trúc Chức năng (Project Progress & Architecture)

> **Tài liệu theo dõi tiến độ, kiến trúc chức năng và ma trận phân quyền hệ thống FlexiWork.**  
> *Thời gian cập nhật: 15/11/2026*

---

## 1. Tổng quan & Phạm vi Đề tài (Scope & Boundaries)

**FlexiWork** là hệ thống quản lý ca kíp và chấm công trực tuyến đa nền tảng:
* **Web Admin Portal:** Dành cho Quản trị viên (`Admin`), Bộ phận Nhân sự (`HR`), Quản lý chi nhánh / Cửa hàng trưởng (`Manager`).
* **Mobile App / Web Responsive (BYOD):** Dành cho Nhân viên (`Employee`) và Quản lý khi thao tác trên thiết bị di động cá nhân.

> [!IMPORTANT]
> **Phạm vi giới hạn dự án (Scope Boundary):**
> * Đề tài tập trung tối ưu bài toán chấm công thông minh, quản lý ca kíp linh hoạt, giao việc theo ca, tổng hợp bảng công tự động và **tính lương tạm tính rút gọn** từ bảng công.
> * **Không bao gồm:** Tính toán chi tiết bảo hiểm xã hội (BHXH) hay thuế TNCN phức tạp (lương rút gọn chỉ gồm: công × đơn giá + phụ cấp + OT − phạt).
> * **Không bao gồm:** Công cụ quản lý dự án đa cấp (Kanban, biểu đồ Gantt chuyên sâu).
> * **Không bao gồm:** Cuộc gọi thoại hoặc video call nội bộ.

---

## 2. Danh mục 8 Phân hệ Chức năng

> Đối chiếu `docs/UML.md` (11 phân hệ, 55 UC): UML tách "Xác thực", "Hệ thống & Danh mục", "Hồ sơ Nhân sự" thành 3 phân hệ — ở đây gộp vào 2.1; Phân hệ 8 (Bảng lương rút gọn) tương ứng mục 2.7; Phân hệ 10 (Trợ lý AI & Phân tích) tương ứng mục 2.8.

### 2.1. Phân hệ Quản lý Hồ sơ & Tổ chức (Web Admin)
* **Quản lý danh sách chi nhánh / cửa hàng:** Khai báo thông tin hành chính, thiết lập tâm tọa độ vĩ độ/kinh độ, bán kính chấm công hợp lệ (GPS Geofencing) và danh sách địa chỉ phần cứng Wi-Fi BSSID nội bộ.
* **Quản lý cơ cấu phòng ban & chức danh:** Thiết lập cơ cấu tổ chức, chức vụ chuyên môn và đơn giá công theo giờ.
* **Quản lý hồ sơ nhân sự:** Quản lý thông tin cá nhân, chức danh, hợp đồng lao động, chi nhánh trực thuộc và trạng thái tài khoản (Active/Locked).
* **Khởi tạo tài khoản & Phân quyền người dùng (RBAC):** Gửi email mời kích hoạt tài khoản và phân quyền truy cập theo vai trò (`Admin`, `Manager`, `Employee`, `HR`).
* **Đăng ký dữ liệu Face ID:** Nhân viên chụp và cập nhật ảnh chân dung mẫu phục vụ đối soát nhận diện khuôn mặt.

### 2.2. Phân hệ Quản lý Ca kíp & Chấm công Trực tuyến (Web Admin & Mobile App)
* **Cấu hình loại ca làm việc & Quy tắc vi phạm:** Khai báo khung giờ các loại ca linh hoạt: ca hành chính, ca xoay, ca gãy, ca bán thời gian; định mức đi muộn/về sớm cho phép và quy tắc trừ công/phạt.
* **Phân ca & Xếp lịch làm việc:** Hỗ trợ Cửa hàng trưởng/Quản lý xếp lịch làm việc theo chu kỳ tuần hoặc tháng cho từng chi nhánh và công bố lịch làm việc chính thức.
* **Chấm công đa lớp chống gian lận (Mobile App - BYOD):** Nhân viên thực hiện Check-in / Check-out với cơ chế xác minh 3 lớp đồng thời:
  1. *Lớp 1 (GPS Geofencing):* Kiểm tra tọa độ thiết bị nằm trong bán kính cho phép của chi nhánh.
  2. *Lớp 2 (Wi-Fi BSSID):* Kiểm tra mã định danh phần cứng router Wi-Fi nội bộ để ngăn chặn phần mềm giả lập Fake GPS.
  3. *Lớp 3 (Selfie Face ID):* Chụp ảnh selfie nhận diện khuôn mặt qua AI (`FW.AiService`), đối soát với ảnh mẫu để ngăn ngừa chấm công hộ (Buddy Punching).
* **Tra cứu nhật ký & bảng công:** Nhân viên theo dõi thời gian vào/ra, số phút đi muộn/về sớm của cá nhân; Quản lý/HR tra cứu toàn bộ dữ liệu minh chứng chấm công (ảnh selfie, tọa độ, sai số khoảng cách, địa chỉ MAC Wi-Fi).

### 2.3. Phân hệ Quản lý Đơn từ & Phê duyệt (Web Admin & Mobile App)
* **Khởi tạo đơn điện tử (Mobile App):** Cho phép nhân viên tạo đơn trực tuyến: xin nghỉ phép (hưởng lương/không lương), đơn xin đổi ca làm việc, đơn giải trình đi muộn/về sớm, đăng ký làm thêm giờ (OT) kèm lý do và ảnh minh chứng.
* **Phê duyệt trực tuyến (Web Admin & Mobile App):** Quản lý/HR tiếp nhận thông báo tức thì, kiểm tra nội dung và thực hiện phê duyệt hoặc từ chối kèm phản hồi.
* **Tự động cập nhật công & Quỹ phép:** Khi đơn được duyệt, hệ thống tự động trừ quỹ ngày phép khả dụng, gán trạng thái nghỉ hợp lệ lên lịch làm việc và bảng công.

### 2.4. Phân hệ Quản lý Công việc theo ca (Shift Task Management)
* **Thiết lập danh mục công việc chuẩn:** Quản lý tạo danh mục checklist các đầu việc cần làm đầu ca, trong ca hoặc cuối ca gán theo từng vị trí làm việc.
* **Theo dõi To-do List cá nhân (Mobile App):** Nhân viên xem danh sách các đầu việc được giao trong ca trực hiện tại.
* **Tích chọn hoàn thành kèm ảnh minh chứng:** Nhân viên đánh dấu hoàn thành từng đầu việc và chụp ảnh hiện trường (ví dụ: quầy thu ngân, kho hàng sạch sẽ) gửi lên hệ thống.
* **Báo cáo tiến độ theo thời gian thực:** Quản lý theo dõi trực quan tỷ lệ hoàn thành công việc theo từng ca trực.

### 2.5. Phân hệ Trao đổi Nội bộ Thời gian thực (Realtime Chat & Notification)
* **Trò chuyện trực tiếp (Direct Chat 1-1):** Nhắn tin văn bản và hình ảnh riêng giữa hai nhân sự trong nội bộ.
* **Nhóm chat (Group Chat):** Tạo các nhóm trao đổi tùy chỉnh giữa nhiều nhân viên hoặc nhóm chat tự động theo từng chi nhánh/phòng ban.
* **Thông báo đẩy tức thì (Push Notification):** Đẩy thông báo theo thời gian thực qua SignalR khi có tin nhắn mới, lịch ca mới, hoặc kết quả phê duyệt đơn từ.

### 2.6. Phân hệ Báo cáo & Tổng hợp Bảng công (Web Admin)
* **Dashboard giám sát quân số:** Trực quan hóa tình hình quân số thời gian thực: số nhân sự đang làm việc, tỷ lệ đi làm đúng giờ, số ca vắng mặt hoặc đi muộn theo từng chi nhánh.
* **Tự động tổng hợp bảng công:** Tự động gom dữ liệu chấm công thô, so khớp với khung ca và đơn từ hợp lệ để tính toán chính xác số giờ làm việc, thời gian đi muộn, về sớm, giờ OT và ngày công quy đổi.
* **Trích xuất dữ liệu (Export):** Hỗ trợ xuất dữ liệu bảng công chi tiết ra định dạng file **Excel (.xlsx)** hoặc **PDF** phục vụ cho bộ phận kế toán/nhân sự tính lương.

### 2.7. Phân hệ Bảng lương Rút gọn (Web Admin & Mobile App) — UML Phân hệ 8
* **Cấu hình mức lương & phụ cấp (UC 8.1):** Lương tháng (nhân viên toàn thời gian) hoặc đơn giá giờ theo chức vụ (bán thời gian, có thể ghi đè theo từng nhân viên); phụ cấp ca gãy/ca đêm, phụ cấp cố định; hệ số OT; định mức phạt.
* **Tự động gom dữ liệu (UC 8.2):** Lấy ngày công, giờ công, giờ OT, số ca gãy/đêm, phút vi phạm từ bảng công tháng đã khóa (Phân hệ 2.6).
* **Tính lương tạm tính (UC 8.3):** `(Công thực tế × Đơn giá) + Phụ cấp + OT − Tiền phạt`, lưu chi tiết từng khoản để đối soát.
* **Chốt & phát hành phiếu lương (UC 8.4):** Chốt kỳ lương, gửi phiếu lương điện tử qua email, nhân viên xem trên Mobile, xuất Excel cho kế toán.

### 2.8. Phân hệ Trợ lý AI & Phân tích (Mobile App & Web Admin) — UML Phân hệ 10
* **Trợ lý nhân sự (UC 10.1–10.2):** Chatbot dùng LLM (Claude API) + Function Calling: nhân viên hỏi công, phép, lịch ca bằng ngôn ngữ tự nhiên; nói "xin nghỉ ca sáng thứ 5 vì bận thi" → trợ lý trích xuất ngày/ca/lý do và tạo **bản nháp** đơn, nhân viên bấm xác nhận mới gửi. Mọi công cụ chạy với quyền của người hỏi.
* **Cảnh báo nguy cơ nghỉ việc (UC 10.3–10.4):** Tác vụ nền hằng tuần chấm điểm 0–100 từ biến động hành vi chấm công/đơn từ so với chính nhân viên đó, kèm lý do; Quản lý/HR xem danh sách "nhân sự cần quan tâm". Phương pháp chấm điểm có giải thích (không huấn luyện mô hình vì không có dữ liệu thật về người đã nghỉ).

---

## 3. Sơ đồ Phân rã Chức năng (BFD - Business Function Decomposition)

```mermaid
flowchart TD
    ROOT["Hệ thống Quản lý Chấm công & Phân ca FlexiWork"]

    ROOT --> M0["0. Xác thực & Tài khoản (Web & Mobile)"]
    ROOT --> M1["1. Quản lý Hồ sơ & Tổ chức (Web Admin)"]
    ROOT --> M2["2. Quản lý Ca kíp & Chấm công (Web & Mobile)"]
    ROOT --> M3["3. Quản lý Đơn từ & Phê duyệt (Web & Mobile)"]
    ROOT --> M4["4. Quản lý Công việc theo ca (Web & Mobile)"]
    ROOT --> M5["5. Trao đổi Thời gian thực (Web & Mobile)"]
    ROOT --> M6["6. Báo cáo & Bảng công (Web Admin)"]
    ROOT --> M7["7. Bảng lương Rút gọn (Web & Mobile)"]
    ROOT --> M8["8. Trợ lý AI & Phân tích (Mobile & Web)"]

    %% Module 0
    M0 --> M0_1["0.1 Đăng nhập / Đăng xuất (khóa tạm khi sai nhiều lần)"]
    M0 --> M0_2["0.2 Đổi mật khẩu (thu hồi phiên thiết bị khác)"]
    M0 --> M0_3["0.3 Quên mật khẩu bằng OTP qua email"]

    %% Module 1
    M1 --> M1_1["1.1 Quản lý Chi nhánh & Geofencing / Wi-Fi"]
    M1 --> M1_2["1.2 Quản lý Phòng ban, Chức vụ & Đơn giá công"]
    M1 --> M1_3["1.3 Quản lý Hồ sơ Nhân sự & Hợp đồng"]
    M1 --> M1_4["1.4 Khởi tạo Tài khoản & Phân quyền RBAC"]

    %% Module 2
    M2 --> M2_1["2.1 Cấu hình Loại ca & Quy tắc vi phạm"]
    M2 --> M2_2["2.2 Phân ca & Xếp lịch làm việc theo chu kỳ"]
    M2 --> M2_3["2.3 Chấm công 3 lớp (GPS + Wi-Fi BSSID + Face ID)"]
    M2 --> M2_4["2.4 Tra cứu Nhật ký & Bảng công chi tiết"]

    %% Module 3
    M3 --> M3_1["3.1 Khởi tạo Đơn điện tử kèm minh chứng"]
    M3 --> M3_2["3.2 Quy trình Phê duyệt / Từ chối đa cấp"]
    M3 --> M3_3["3.3 Tự động cấn trừ Quỹ phép & Cập nhật Bảng công"]

    %% Module 4
    M4 --> M4_1["4.1 Thiết lập Checklist công việc chuẩn theo ca"]
    M4 --> M4_2["4.2 To-do List cá nhân theo ca trực"]
    M4 --> M4_3["4.3 Check hoàn thành kèm ảnh minh chứng"]
    M4 --> M4_4["4.4 Dashboard giám sát tiến độ Task realtime"]

    %% Module 5
    M5 --> M5_1["5.1 Nhắn tin trực tiếp 1-1"]
    M5 --> M5_2["5.2 Chat nhóm theo Chi nhánh / Tùy chỉnh"]
    M5 --> M5_3["5.3 Thông báo đẩy tức thì (SignalR)"]

    %% Module 6
    M6 --> M6_1["6.1 Dashboard giám sát quân số thời gian thực"]
    M6 --> M6_2["6.2 Báo cáo vi phạm (Đi muộn / Về sớm / Vắng mặt)"]
    M6 --> M6_3["6.3 Tự động tổng hợp dữ liệu Bảng công"]
    M6 --> M6_4["6.4 Trích xuất Bảng công ra Excel (.xlsx) / PDF"]

    %% Module 7
    M7 --> M7_1["7.1 Cấu hình Mức lương, Phụ cấp & Hệ số OT"]
    M7 --> M7_2["7.2 Tính Bảng lương tạm tính từ Bảng công"]
    M7 --> M7_3["7.3 Chốt kỳ lương & Gửi phiếu lương điện tử"]
    M7 --> M7_4["7.4 Xuất Bảng lương ra Excel cho Kế toán"]

    %% Module 8
    M8 --> M8_1["8.1 Trợ lý nhân sự: hỏi đáp công, phép, lịch ca"]
    M8 --> M8_2["8.2 Tạo đơn từ qua hội thoại (bản nháp + xác nhận)"]
    M8 --> M8_3["8.3 Chấm điểm & cảnh báo nguy cơ nghỉ việc"]
```

---

## 4. Ma trận Phân quyền Người dùng (RBAC Matrix)

### 4.1. Quy ước vai trò & ký hiệu
* **Vai trò:**
  * `Admin`: Quản trị viên cao nhất hệ thống.
  * `HR`: Quản lý nhân sự toàn doanh nghiệp.
  * `Manager`: Quản lý chi nhánh / Cửa hàng trưởng (phạm vi theo chi nhánh phụ trách).
  * `Employee`: Nhân viên (chỉ thao tác trên dữ liệu cá nhân).
* **Quyền hạn:**
  * `C` (Create): Tạo mới
  * `R` (Read): Xem / Tra cứu
  * `U` (Update): Chỉnh sửa
  * `D` (Delete): Xóa
  * `A` (Approve/Reject): Phê duyệt / Từ chối
  * `E` (Execute): Thực thi tác vụ (chấm công, nộp đơn, xuất báo cáo)
  * `-`: Không có quyền truy cập

### 4.2. Bảng Ma trận Phân quyền

| Phân hệ & Chức năng chi tiết | Nền tảng | Admin | HR | Manager | Employee |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **0. Xác thực & Tài khoản** | | | | | |
| - Đăng nhập, đăng xuất, đổi mật khẩu, quên mật khẩu (OTP) | Mobile/Web | `E` | `E` | `E` | `E` *(Cá nhân)* |
| - Cấp lại mật khẩu tạm cho nhân viên | Web | `E` | `E` | `-` | `-` |
| **1. Quản lý Hồ sơ & Tổ chức** | | | | | |
| - Quản lý Chi nhánh, GPS Geofence, Wi-Fi BSSID | Web | `CRUD` | `R` | `R` *(theo CN)* | `-` |
| - Quản lý Phòng ban, Chức danh & Đơn giá công | Web | `CRUD` | `CRUD` | `R` | `-` |
| - Quản lý Danh sách Hồ sơ Nhân sự | Web | `CRUD` | `CRUD` | `R` *(theo CN)* | `-` |
| - Mời nhân viên & Cấp phát tài khoản | Web | `CRUD` | `CRUD` | `-` | `-` |
| - Cấu hình vai trò & Phân quyền (RBAC) | Web | `CRUD` | `R` | `-` | `-` |
| - Cập nhật thông tin cá nhân & Ảnh mẫu Face ID | Mobile/Web | `RU` | `RU` | `RU` | `RU` *(Cá nhân)* |
| **2. Quản lý Ca kíp & Chấm công** | | | | | |
| - Cấu hình Khung giờ ca làm việc | Web | `CRUD` | `CRUD` | `CRUD` *(ca riêng của CN; ca dùng chung chỉ `R`)* | `-` |
| - Cấu hình Số công chuẩn & Quy tắc phạt | Web | `CRUD` | `CRUD` | `R` | `-` |
| - Phân ca & Xếp lịch làm việc (Tuần/Tháng) | Web | `CRUD` | `CRUD` | `CRUD` *(theo CN)* | `-` |
| - Xuất bản (Publish) Lịch làm việc | Web | `CRUD` | `CRUD` | `CRUD` *(theo CN)* | `-` |
| - Tra cứu Lịch làm việc cá nhân & chi nhánh | Mobile/Web | `R` | `R` | `R` *(theo CN)* | `R` *(Cá nhân)* |
| - Chấm công 3 lớp (GPS + Wi-Fi + Face ID) | Mobile | `-` | `-` | `E` *(nếu có ca)* | `E` |
| - Tra cứu Nhật ký chấm công chi tiết (Ảnh, GPS, MAC) | Web | `R` | `R` | `R` *(theo CN)* | `R` *(Cá nhân)* |
| - Quản lý & Khóa bảng công chi nhánh | Web | `CRUD` | `CRUD` | `U` *(Xác nhận)* | `-` |
| - Duyệt bản ghi chấm công chờ xác nhận | Web | `A` | `A` | `A` *(theo CN)* | `-` |
| **3. Quản lý Đơn từ & Phê duyệt** | | | | | |
| - Khởi tạo Đơn (Nghỉ phép, Đổi ca, OT, Giải trình) | Mobile/Web | `C` | `C` | `C` | `C` *(Cá nhân)* |
| - Theo dõi tiến trình xử lý đơn cá nhân | Mobile/Web | `R` | `R` | `R` | `R` *(Cá nhân)* |
| - Phê duyệt / Từ chối đơn từ | Web/Mobile | `A` | `A` | `A` *(theo CN)* | `-` |
| - Đồng ý / Từ chối đề nghị đổi ca của đồng nghiệp | Mobile | `-` | `-` | `A` | `A` *(Cá nhân)* |
| - Cấu hình Danh mục đơn & Quy trình duyệt | Web | `CRUD` | `CRUD` | `-` | `-` |
| - Quản lý & Điều chỉnh Quỹ phép nhân viên | Web | `CRUD` | `CRUD` | `R` *(theo CN)* | `R` *(Cá nhân)* |
| **4. Quản lý Công việc theo ca (Shift Tasks)** | | | | | |
| - Thiết lập Danh mục Checklist công việc mẫu | Web | `CRUD` | `R` | `CRUD` *(theo CN)* | `-` |
| - Giao Checklist việc cho nhân viên theo ca | Web/Mobile | `CRUD` | `R` | `CRUD` *(theo CN)* | `-` |
| - Xem To-do List cá nhân trong ca trực | Mobile | `R` | `R` | `R` | `R` *(Cá nhân)* |
| - Tích hoàn thành Task & Upload ảnh minh chứng | Mobile | `-` | `-` | `E` | `E` |
| - Giám sát tiến độ hoàn thành Task thời gian thực | Web/Mobile | `R` | `R` | `R` *(theo CN)* | `-` |
| **5. Trao đổi Thời gian thực (Chat & Notification)** | | | | | |
| - Nhắn tin trực tiếp 1-1 (Direct Chat) | Mobile/Web | `CRUD` | `CRUD` | `CRUD` | `CRUD` |
| - Tạo và quản lý Nhóm chat tùy chỉnh | Mobile/Web | `CRUD` | `CRUD` | `CRUD` | `CRUD` |
| - Nhóm chat tự động theo Chi nhánh / Phòng ban | Mobile/Web | `R` | `R` | `R` | `R` |
| - Tiếp nhận thông báo đẩy tức thì (SignalR) | Mobile/Web | `R` | `R` | `R` | `R` |
| - Xem danh sách thông báo & đánh dấu đã đọc | Mobile/Web | `RU` | `RU` | `RU` | `RU` *(Cá nhân)* |
| **6. Báo cáo & Tổng hợp Bảng công** | | | | | |
| - Dashboard giám sát quân số Realtime | Web | `R` | `R` | `R` *(theo CN)* | `-` |
| - Báo cáo vi phạm (Đi muộn, về sớm, vắng) | Web | `R` | `R` | `R` *(theo CN)* | `-` |
| - Tổng hợp số giờ công, công quy đổi, giờ OT | Web | `R` | `CRUD` | `R` *(theo CN)* | `R` *(Cá nhân)* |
| - Trích xuất file Bảng công (Excel / PDF) | Web | `E` | `E` | `E` *(theo CN)* | `-` |
| **7. Bảng lương Rút gọn** | | | | | |
| - Cấu hình Mức lương, Phụ cấp, Hệ số OT, Mức phạt | Web | `CRUD` | `CRUD` | `-` | `-` |
| - Tính & Xem Bảng lương tạm tính | Web | `CRUD` | `CRUD` | `R` *(theo CN)* | `-` |
| - Chốt kỳ lương & Gửi phiếu lương | Web | `E` | `E` | `-` | `-` |
| - Xem phiếu lương cá nhân | Mobile/Web | `R` | `R` | `R` | `R` *(Cá nhân)* |
| - Xuất Bảng lương ra Excel | Web | `E` | `E` | `-` | `-` |
| **8. Trợ lý AI & Phân tích** | | | | | |
| - Hỏi đáp với trợ lý (chỉ dữ liệu trong phạm vi quyền của mình) | Mobile/Web | `E` | `E` | `E` *(theo CN)* | `E` *(Cá nhân)* |
| - Tạo bản nháp đơn qua hội thoại (phải bấm xác nhận) | Mobile | `C` | `C` | `C` | `C` *(Cá nhân)* |
| - Xem cảnh báo nhân sự cần quan tâm | Web | `R` | `R` | `R` *(theo CN)* | `-` |
| - Cấu hình trọng số / ngưỡng cảnh báo | Web | `U` | `U` | `-` | `-` |

---

## 5. Kiến trúc Kỹ thuật & Luồng Xử lý Trọng yếu

```
[Mobile App (BYOD)]                   [Web Admin Portal]
        │                                      │
        │ HTTPS (JWT)                          │ HTTPS (JWT)
        ▼                                      ▼
┌─────────────────────────────────────────────────────────────┐
│                    FW.API (.NET Core)                       │
│  - Controllers & Endpoints          - SignalR Hub           │
│  - Application Services             - Background Jobs       │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
        REST API (HTTP)                        │ Dapper (Micro-ORM)
               │                               │
               ▼                               ▼
┌───────────────────────────────┐     ┌───────────────────────┐
│         FW.AiService          │     │       MySQL 8.0       │
│  - Python FastAPI             │     │  - Relational Schema  │
│  - Face Detection & Embeddings│     │  - Attendance Logs    │
│  - Anti-spoofing / Cosine Sim │     │  - Tasks, Shifts, etc.│
└───────────────────────────────┘     └───────────────────────┘
```

1. **Quy trình Chấm công 3 lớp (Mobile $\rightarrow$ Backend $\rightarrow$ AI Service):**
   * **Bước 1 (Thu thập):** Ứng dụng Mobile đọc tọa độ GPS, mã Wi-Fi BSSID (nếu có kết nối Wi-Fi) và kích hoạt camera chụp Selfie trực tiếp.
   * **Bước 2 (Kiểm tra Lớp 1 & Lớp 2):** Backend kiểm tra công thức Haversine với tọa độ gốc của chi nhánh (sai số $\le$ bán kính cấu hình). Tiếp tục đối soát BSSID với danh sách điểm truy cập hợp lệ để chặn Fake GPS.
   * **Bước 3 (Kiểm tra Lớp 3):** Backend gửi ảnh selfie sang `FW.AiService` để phát hiện khuôn mặt và so sánh vector đặc trưng (Face Embeddings) với ảnh mẫu đã đăng ký của nhân viên (Cosine Similarity $\ge$ ngưỡng tin cậy).
   * **Bước 4 (Ghi nhận & Realtime):** Lưu bản ghi điểm danh kèm minh chứng (ảnh, tọa độ, địa chỉ MAC). Phát tín hiệu qua SignalR tới Web Admin của Quản lý chi nhánh để cập nhật bảng giám sát quân số.

2. **Cơ chế Đồng bộ & Realtime (SignalR):**
   * Quản lý nhóm kênh (Hub Groups) theo `Branch_{Id}` và `User_{Id}`.
   * Đảm bảo thông tin cập nhật tức thì khi có: ca mới được xếp, đơn xin nghỉ được duyệt, hoặc checklist công việc hoàn thành.

---

## 6. Kế hoạch & Bảng Theo dõi Tiến độ (Actionable Checklist)

> **Quy ước:**
> * Mỗi cấp bậc là một **lát cắt dọc** hoàn chỉnh: Backend API + Web Admin + Mobile App. Kết thúc mỗi cấp bậc có bản chạy được để demo (theo phương pháp Agile tại Mục 1.5 khóa luận).
> * Mỗi task backend có logic tính toán phải kèm **unit test** trong `FW.UnitTests`.
> * `[Tùy chọn]`: chỉ thực hiện khi còn thời gian; có thể đưa vào "Hướng phát triển" (Chương 5).

> **Tổng quan Tiến độ Hiện tại:**
> * **Giai đoạn đang thực hiện:** CẤP BẬC 3 - Lõi chấm công 3 lớp
> * **Tiến độ tổng thể:** `21 / 53` Nhiệm vụ hoàn thành (`40%`)
> * **Hành động tiếp theo:** **Task 3.4** — màn hình chấm công trên Mobile (chạy migration `005` trước khi push Task 3.1–3.3). Việc nhỏ đang chờ: bản đồ chọn tâm geofence trên form chi nhánh (Leaflet + OpenStreetMap).

### Các quyết định cần chốt

| Quyết định | Ảnh hưởng tới | Đề xuất mặc định |
| :--- | :--- | :--- |
| Manager phụ trách 1 hay nhiều chi nhánh | Task 1.3, 1.5 | 1 chi nhánh (`users.branch_id`) |
| ~~Múi giờ lưu trữ~~ | Task 1.2, 3.2, 5.3 | ✅ **Đã chốt:** giờ Việt Nam (UTC+7) — `SET time_zone` mỗi kết nối + `IDateTimeProvider` |
| ~~Xử lý khi 1 lớp chấm công không đạt~~ | Task 3.2 | ✅ **Đã chốt (15/11/2026):** GPS + khuôn mặt bắt buộc (không đạt → từ chối, không lưu bản ghi); sai Wi-Fi **hoặc không đọc được Wi-Fi** (tắt Wi-Fi, thiếu quyền vị trí, iOS trong Expo Go) → vẫn ghi nhận nhưng `pending_approval`, **không có ngoại lệ** cho iPhone; chi nhánh chưa khai báo Wi-Fi → bỏ qua lớp 2. Vị trí giả lập (cờ `mocked`) hoặc sai số GPS > 100 m → từ chối |
| ~~Ca gãy~~ | Task 2.2 | ✅ **Đã chốt:** tách thành 2 ca trong cùng ngày (ca nối tiếp không tính chồng giờ). Ca xoay là cách xếp lịch (Task 2.3), không phải thuộc tính của ca |
| ~~Quyền khai báo khung ca~~ | Task 2.2 | ✅ **Đã chốt (14/11/2026):** Admin/HR quản lý mọi ca (dùng chung + ca riêng của chi nhánh); Quản lý chi nhánh thêm/sửa/xóa ca riêng của chi nhánh mình (UC 3.1), chỉ xem ca dùng chung. Ca đã được xếp lịch không đổi được giờ, giờ nghỉ, hệ số công, ca đêm, chi nhánh — tạo ca mới thay thế; còn lịch từ hôm nay trở đi thì không xóa được. Ca dài tối đa 16 giờ |
| ~~Quy tắc vi phạm~~ | Task 2.2 | ✅ **Đã chốt (14/11/2026):** áp dụng toàn công ty, **theo bậc** (`violation_rules`: từ X phút → trừ Y công / phạt Z đồng, chỉ áp dụng bậc cao nhất thỏa mãn, bậc sau không nhẹ hơn bậc trước). Vượt số phút cho phép của ca thì tính **toàn bộ** số phút muộn/sớm. Số công chuẩn tháng ở `system_settings.standard_work_days` (mặc định 26). Mặc định: muộn/sớm từ 1' phạt 20.000đ, từ 15' phạt 50.000đ, từ 60' trừ 0,5 công + 50.000đ |
| Đơn đổi ca cần đồng nghiệp đồng ý trước | Task 4.2 | Có |
| Chống giả mạo khuôn mặt (liveness) | Task 6.2 | `[Tùy chọn]` |
| ~~Phát hiện "trùng thiết bị" (1 điện thoại chấm công cho nhiều người)~~ | Task 3.2 | ✅ **Đã chốt (15/11/2026):** lưu mã thiết bị mỗi lượt; người thứ hai check-in trên cùng điện thoại trong cùng ngày công → `pending_approval`, ghi chú mã nhân viên đã dùng máy đó (Quản lý duyệt ở UC 4.6, cảnh báo trên Dashboard ở Cấp 5) |
| ~~Giao diện Web Admin~~ | Task 1.6 trở đi | ✅ **Đã chốt:** tham khảo bố cục HRM iPOS, thương hiệu & hình ảnh riêng FlexiWork, bỏ Tuyển dụng/Bảng giá, Hợp đồng gộp vào hồ sơ NV, Lương → Bảng công (lương tạm tính) |
| ~~Thư viện giao diện Web Admin~~ | Toàn bộ Web | ✅ **Đã chốt (12/11/2026):** nâng **React 18 → 19**, **Tailwind 3 → 4**, dùng hệ thống thiết kế **Astryx** của Meta (`@astryxdesign/core` 0.7.14, đang beta — ghim cố định phiên bản). Theme `flexiwork` kế thừa `neutral`: màu nhấn xanh `#2563EB`, font **Be Vietnam Pro** (Figtree gốc thiếu dấu tiếng Việt), gói ngôn ngữ `vi-VN`, chế độ sáng/tối. Mobile giữ React Native (Astryx chỉ cho web) |
| ~~Phân hệ Bảng lương rút gọn (UML Phân hệ 8)~~ | Task 5.4, 5.5 | ✅ **Đã chốt (12/11/2026):** đưa vào phạm vi, không gồm thuế TNCN/BHXH |
| Cách tính lương | Task 5.4, 5.5 | Toàn thời gian: `lương tháng ÷ công chuẩn × ngày công`; bán thời gian: `giờ công × đơn giá giờ` (chức vụ, ghi đè được theo NV); OT ngày thường × 1,5; phạt theo quy tắc vi phạm (Task 2.2) |
| Vai trò HR (UML UC 1.6 mới ghi 3 vai trò) | Task K.2 | Giữ 4 vai trò; sửa UC 1.6 trong UML/Chương 3 thêm HR |
| Nhóm chat tùy chỉnh (UML UC 7.2) | Task 5.2 | Vẫn `[Tùy chọn]`; nhóm tự động theo chi nhánh là bắt buộc |
| ~~Công cụ bổ sung ngoài Chương 2~~ | Cấp 2–6 | ✅ **Đã chốt:** Excel → **ClosedXML** (MIT); PDF → **QuestPDF**; biểu đồ → **Recharts**; bản đồ → **Leaflet + OpenStreetMap** (tìm địa chỉ: Nominatim) thay Google Maps; ngày giờ Web → **dayjs**; push → **expo-notifications + Expo Push**; Fake GPS → cờ `mocked` của `expo-location`; mã thiết bị → **expo-application**; ảnh minh chứng → `expo-image-picker` + `expo-image-manipulator`; tác vụ nền → `BackgroundService` (.NET) |
| ~~Gửi email~~ | Task 1.3, 5.5, 6.3 | ✅ **Đã chốt:** local dùng Gmail + App Password (SMTP/MailKit); cloud dùng **Brevo HTTP API** (`BrevoEmailService`, ưu tiên khi có `Brevo:ApiKey`) vì Render gói free chặn cổng SMTP |
| ~~Redis khi triển khai~~ | Task 6.3 | ✅ **Đã chốt:** **Render Key Value** (khai báo trong `render.yaml`); backend đọc được cả URL `redis://` / `rediss://` |
| ~~Thu hồi phiên khi đổi mật khẩu~~ | Task 1.x (bổ sung) | ✅ **Đã chốt (14/11/2026):** đổi mật khẩu vẫn chỉ cần mật khẩu hiện tại (OTP chỉ cho "Quên mật khẩu"), bổ sung: (1) email thông báo "Mật khẩu vừa được thay đổi" sau đổi/đặt lại mật khẩu; (2) cột `users.password_changed_at` + claim JWT `pwd_stamp` — token cấp trước lần đổi/đặt lại/cấp lại mật khẩu bị từ chối 401 (kiểm tra ở `OnTokenValidated`, cache Redis 30 phút), thiết bị đang đổi nhận token mới. Migration `001_users_password_changed_at.sql`. Chương 2 (bảo mật) nêu cơ chế này |
| ~~Triển khai sớm~~ | Task 6.3 | ✅ **Đã chốt (12/11/2026):** đưa Web (Vercel) + API (Render, Docker) + MySQL (TiDB Cloud) lên cloud ngay để phát hiện lỗi sớm; AI Service lên cloud ở Task 2.4 (kiểm tra RAM), EAS Build ở Task 3.4. Không seed tài khoản mẫu trên cloud — Admin đầu tiên tạo từ biến môi trường `Bootstrap__*`. Hướng dẫn: `docs/DEPLOY.md` |
| ~~Nơi chạy nhận diện khuôn mặt~~ | Task 2.4, 6.3 | ✅ **Đã chốt (15/11/2026, thay quyết định Hugging Face cùng ngày):** Hugging Face Spaces yêu cầu gói PRO (9 USD/tháng) để chạy Docker → nhận diện chạy **ngay trong backend .NET** bằng ONNX Runtime với bộ mô hình InsightFace **buffalo_sc** (SCRFD det_500m + ArcFace w600k_mbf), xử lý ảnh viết bằng C# (SkiaSharp đọc ảnh — bỏ ImageSharp vì bản 3.x có lỗ hổng chưa vá, bản 4.x cần bản quyền trả phí). Đối chiếu InsightFace gốc: cosine 0,9998–0,9999, lệch điểm mốc < 0,1 px. Đo với giới hạn Render free (0,1 CPU, 512 MB): ~0,6–0,85 s/lần, backend ~151 MB RAM; image Docker 418 MB (chỉ thư viện linux-x64). Interface `IFaceRecognitionService` có 2 cách cài đặt chọn bằng `FaceRecognition:Engine`: `Onnx` (mặc định) và `AiService` (gọi microservice `FW.AiService` — giữ cho khả năng tách dịch vụ). Render chỉ còn `flexiwork-api`, giữ thức bằng UptimeRobot |
| ~~Phân bổ chức năng AI~~ | Task 2.4, Cấp 7 | ✅ **Đã chốt (15/11/2026):** Face ID → chạy trong backend (ONNX, xem dòng trên). **Chatbot** → backend .NET gọi **Claude API** (SDK chính thức cho C#, mô hình mặc định Claude Opus 5.5, giới hạn chi tiêu trên Console): công cụ tái sử dụng service sẵn có, chạy với quyền người hỏi, AI chỉ tạo **bản nháp** đơn — người dùng xác nhận. Đặt ở backend (Singapore, cùng TiDB) thay vì dịch vụ riêng để tránh nhiều vòng gọi xuyên lục địa và không phải chuyển token người dùng qua thêm một tầng. **Cảnh báo nghỉ việc** → tác vụ nền backend, chấm điểm có giải thích (không học máy: không có dữ liệu nhãn người đã nghỉ) |
| ~~CSDL trên cloud~~ | Task 6.3 | ✅ **Đã chốt (13/11/2026):** Aiven ngừng MySQL free (không tạo mới từ 06/11/2026, tắt dịch vụ cũ 21/12/2026) → dùng **TiDB Cloud Starter** (AWS Singapore, tương thích giao thức MySQL 8, free 5 GiB + 50 triệu RU/tháng). Giữ nguyên MySQL 8 trên máy dev, `MySqlConnector` và toàn bộ SQL; đã chạy đạt 33/33 test tích hợp trên TiDB v8.5. Không chuyển PostgreSQL (phải viết lại schema + ~57 câu SQL, mất tìm kiếm không dấu). Lưu ý: TiDB bắt buộc SSL, cổng 4000, ID tự tăng có thể nhảy cóc. Chương 2 khóa luận: thay "Aiven" bằng "TiDB Cloud" |

---

### 📌 CẤP BẬC 0: Nền tảng Kiến trúc & Hạ tầng (Foundation) `[5/5]` ✅ HOÀN THÀNH
- [x] **Task 0.1:** Khởi tạo Solution `.NET 8` Clean Architecture (`FW.Backend.sln`, `FW.Domain`, `FW.Application`, `FW.Infrastructure`, `FW.API`), khóa phiên bản SDK qua `global.json` (`8.0.427`), thiết lập Project References và kiểm tra biên dịch thành công.
- [x] **Task 0.2:** Xây dựng Base Domain Entities (`BaseEntity`, `IAuditableEntity`, `ISoftDelete`, `BaseAuditableEntity`, `IGenericRepository`) trong `FW.Domain`.
- [x] **Task 0.3:** Xây dựng Common Models & Response Wrappers (`ApiResponse<T>`, `PaginatedResult<T>`) cùng Custom Exceptions (`AppException`, `NotFoundException`, `BadRequestException`, `UnauthorizedException`, `ForbiddenException`) trong `FW.Application`.
- [x] **Task 0.4:** Cấu hình Data Access Layer trong `FW.Infrastructure` (Dapper + MySqlConnector, `DbConnectionFactory`), đăng ký DI extension methods, chuỗi kết nối trong `appsettings.json`, cấu hình CORS, JWT Bearer Swagger, `GlobalExceptionMiddleware` và test API `HealthCheckController` trong `FW.API`.
- [x] **Task 0.5:** Dựng khung toàn dự án theo công nghệ trong khóa luận:
  - *Backend:* xác thực JWT Bearer (kèm token qua query cho SignalR), `ApiControllerBase`, `CurrentUserService`, Settings (Jwt, Smtp, Redis, Cloudinary, AiService), dịch vụ hạ tầng `SmtpEmailService` (MailKit), `RedisCacheService`, `CloudinaryStorageService`, `FaceRecognitionClient`; thư mục `Features/<Module>`; project `FW.UnitTests` (xUnit); `Dockerfile`; tách bí mật sang `appsettings.Development.json` (không commit).
  - *Web:* React 18 + TS + Vite + Tailwind + Zustand + Axios + SignalR — router, `ProtectedRoute` theo vai trò, `AdminLayout`, `apiClient`, `signalrClient`, `vercel.json`.
  - *Mobile:* Expo SDK 58 + Expo Router + TS — `expo-camera`, `expo-location`, NetInfo (BSSID), `expo-secure-store`, quyền camera/vị trí/Wi-Fi trong `app.json`.
  - *Hạ tầng:* `docker-compose.yml` (MySQL + Redis + AI Service + API), `.env.example`, `README.md`.

---

### 📌 CẤP BẬC 1: Tổ chức, Người dùng & Bảo mật `[7/7]` ✅ HOÀN THÀNH
> 🎯 **Demo:** Đăng nhập Web Admin theo vai trò; quản lý chi nhánh, phòng ban, chức vụ, nhân sự; nhân viên đăng nhập trên Mobile.

- [x] **Task 1.1:** Định nghĩa Enums & Entities (`User`, `Role`, `UserRole`, `Department`, `Position`, `Branch`, `BranchWifi`) trong `FW.Domain`.
  - Enums dạng hằng chuỗi (`RoleNames`, `UserStatus`, `BranchStatus`) khớp giá trị VARCHAR trong CSDL (Dapper ghi enum C# thành số nguyên).
  - Bổ sung vai trò `ROLE_HR` (seed id 4); Manager phụ trách 1 chi nhánh theo `users.branch_id`.
  - Bổ sung cột `updated_at` cho `branch_wifis`, `departments`, `positions` để mọi bảng danh mục dùng chung `BaseAuditableEntity`.
  - Unit test `EntitySchemaMappingTests` đối chiếu 2 chiều thuộc tính Entity ↔ cột trong `flexiwork_schema.sql`.
- [x] **Task 1.2:** Data Access nền tảng trong `FW.Infrastructure`:
  - `BaseRepository<T>` (Dapper): tự sinh SQL CRUD từ `EntityMap` (PascalCase ↔ snake_case), xóa mềm tự động với Entity có `ISoftDelete`, phân trang (`PagedQuery` → `PaginatedResult<T>`), tìm kiếm LIKE có thoát ký tự đặc biệt.
  - `DbSession` (scoped) cài đặt `IUnitOfWork`: một kết nối + transaction dùng chung cho mọi repository trong request; `ExecuteInTransactionAsync` tự commit/rollback, hỗ trợ lồng nhau.
  - Repository: `BranchRepository`, `BranchWifiRepository`, `DepartmentRepository`, `PositionRepository`, `RoleRepository`, `UserRepository` (lọc theo chi nhánh/phòng ban/chức vụ/trạng thái/vai trò, `SetRolesAsync` trong transaction). Kiểm tra trùng mã/email xét cả bản ghi đã xóa mềm vì ràng buộc UNIQUE.
  - Interface repository chuyển từ `FW.Domain/Interfaces` sang `FW.Application/Common/Interfaces/Persistence` (để dùng `PaginatedResult`).
  - Múi giờ: `MySqlDbConnectionFactory` chạy `SET time_zone = '+07:00'` mỗi lần mở kết nối; `IDateTimeProvider` trả giờ Việt Nam.
  - Project `FW.IntegrationTests` (9 test trên MySQL thật, CSDL riêng `flexiwork_test`, bật bằng biến môi trường `FW_TEST_DB`).
- [x] **Task 1.3:** Xác thực & Phân quyền:
  - API `/api/auth`: `login`, `me`, `change-password`, `forgot-password`, `reset-password` (`AuthService`).
  - Đăng nhập BCrypt; JWT (HMAC-SHA256) claims `sub`, `email`, `name`, `employee_code`, `branch_id`, `role` (tắt `MapInboundClaims` để tên claim khớp 2 phía). Khóa đăng nhập 15 phút sau 5 lần sai; cùng thông báo lỗi cho sai email/sai mật khẩu.
  - Quên mật khẩu: OTP 6 số qua email (MailKit), lưu cache dạng băm SHA-256, hiệu lực 5 phút, dùng 1 lần, tối đa 5 lần nhập sai, chờ 60 giây giữa các lần gửi; không tiết lộ email có tồn tại hay không.
  - Quy tắc mật khẩu (`PasswordPolicy`): ≥ 8 ký tự, có chữ cái và chữ số, không khoảng trắng.
  - Phân quyền: nhóm vai trò `RoleNames.AdminOrHr`, `RoleNames.Managers` cho `[Authorize(Roles=...)]`; phạm vi chi nhánh `AccessScopeExtensions` (`GetBranchScope`, `EnsureCanAccessBranch`): Admin/HR toàn công ty, Manager chỉ chi nhánh của mình.
  - Lỗi 400 (validate), 401, 403 đều trả `ApiResponse`; JSON giữ nguyên tiếng Việt.
  - Seed: hash BCrypt thật cho mật khẩu `Admin@123`, thêm tài khoản HR (`hr@flexiwork.vn`).
  - Môi trường dev: cache bộ nhớ khi chưa có Redis, email ghi ra console khi chưa cấu hình SMTP.
  - Test: 31 unit test mới (AuthService, PasswordPolicy, phạm vi chi nhánh, BCrypt, JWT) + 5 integration test (đăng nhập 4 tài khoản mẫu, đặt lại mật khẩu trên MySQL).
- [x] **Task 1.4:** API Chi nhánh (tọa độ Lat/Long, bán kính Geofence, danh sách Wi-Fi BSSID), Phòng ban, Chức vụ & đơn giá công.
  - `/api/branches` (+ `/{id}/wifis`), `/api/departments`, `/api/positions`: danh sách phân trang + tìm kiếm, chi tiết, thêm, sửa, xóa mềm.
  - Phân quyền đúng ma trận RBAC: chi nhánh — Admin ghi, HR xem toàn bộ, Manager chỉ xem chi nhánh của mình (kể cả khi cố tình lọc chi nhánh khác); phòng ban/chức vụ — Admin & HR ghi, Manager xem; Employee không truy cập.
  - Ràng buộc nghiệp vụ: mã chi nhánh/phòng ban chuẩn hóa chữ hoa và không trùng; tên chức danh không trùng; tọa độ hợp lệ, bán kính 10–5000 m; BSSID chuẩn hóa về `XX:XX:XX:XX:XX:XX` (chấp nhận `-` hoặc liền nhau), không trùng trong chi nhánh; không xóa chi nhánh/phòng ban/chức vụ đang có nhân sự; xóa chi nhánh xóa luôn Wi-Fi (transaction).
  - Lỗi đầu vào (JSON sai, thiếu body, sai kiểu) trả thông báo tiếng Việt thay vì thông báo kỹ thuật tiếng Anh.
  - Test: 12 unit test (BSSID) + 7 integration test (nghiệp vụ & phạm vi theo vai trò); kiểm tra API thật với 4 vai trò (200/201/400/403/404).
- [x] **Task 1.5:** API Hồ sơ Nhân sự: thêm mới kèm **email mời kích hoạt tài khoản**, phân vai trò, gán chi nhánh/phòng ban/chức vụ, khóa/mở tài khoản, tìm kiếm & phân trang; cập nhật thông tin cá nhân và ảnh đại diện (Cloudinary).
  - `/api/users`: danh sách (lọc chi nhánh/phòng ban/chức vụ/trạng thái/vai trò), chi tiết (kèm tên chi nhánh, phòng ban, chức vụ, vai trò), tạo, sửa, `PATCH /status` (active/suspended/left), `PUT /roles` (chỉ Admin), `POST /reissue-password`, xóa mềm. `/api/profile` (+ `/avatar`): tự xem/sửa SĐT/ảnh đại diện. `/api/roles`: danh mục vai trò kèm cờ được phép gán.
  - Tạo tài khoản: sinh **mật khẩu tạm** ngẫu nhiên (12 ký tự, bỏ ký tự dễ nhầm) gửi qua email, cờ `must_change_password` buộc đổi ở lần đăng nhập đầu (trả về trong `login`/`me`); lỗi SMTP không làm mất tài khoản (báo để HR cấp lại mật khẩu).
  - Quy tắc: Admin gán mọi vai trò; HR chỉ gán Employee/Manager và không quản lý tài khoản Admin; Manager chỉ xem nhân sự chi nhánh mình; Manager bắt buộc thuộc một chi nhánh; không tự khóa/tự xóa/tự đổi vai trò; mã NV & email không trùng; chi nhánh/phòng ban/chức vụ phải tồn tại.
  - Schema `users`: thêm `employment_type` (full_time/part_time), `hire_date`, `must_change_password`.
  - Ảnh đại diện JPG/PNG/WEBP ≤ 5 MB; khi phát triển chưa có Cloudinary thì lưu cục bộ và phục vụ tại `/uploads`.
  - Test: 8 unit test (sinh mật khẩu, quy tắc quản lý tài khoản) + 8 integration test; kiểm tra API thật (tạo NV → mật khẩu tạm → đăng nhập → ảnh đại diện → khóa → cấp lại mật khẩu).
- [x] **Task 1.6:** Web Admin: màn hình đăng nhập, quên mật khẩu, menu theo vai trò; quản lý Chi nhánh (kèm Wi-Fi), Phòng ban, Chức vụ, Nhân sự.
  - Bố cục tham khảo HRM iPOS (header tối, sidebar nhóm thu gọn, Dashboard thẻ thống kê) với thương hiệu, logo, hình minh họa SVG riêng của FlexiWork; menu theo 6 phân hệ, mục chưa làm hiển thị trang "đang phát triển".
  - Xác thực: đăng nhập, quên mật khẩu 2 bước (email → OTP + mật khẩu mới), **bắt buộc đổi mật khẩu tạm** (chặn mọi trang khác), đổi mật khẩu chủ động, tự đăng xuất khi token hết hạn/401.
  - Trang: Tổng quan (khung số liệu, chờ dữ liệu Cấp 3/5), Nhân viên (lọc chi nhánh/phòng ban/vai trò/trạng thái; thêm kèm vai trò; sửa; khóa/mở; cấp lại mật khẩu; phân quyền; xóa; xem chi tiết), Chi nhánh (Geofence: lấy vị trí hiện tại, xem bản đồ; quản lý Wi-Fi BSSID), Phòng ban, Chức vụ & đơn giá, Hồ sơ cá nhân (SĐT, ảnh đại diện).
  - Hiển thị theo vai trò: Admin đầy đủ; HR không gán Admin/HR, không thao tác tài khoản Admin, không phân quyền; Manager chỉ xem chi nhánh mình; Employee thấy trang hướng dẫn dùng app di động.
  - Kỹ thuật: MVVM (hook `usePagedList` làm ViewModel), Zustand (`authStore`, `lookupStore`, `toastStore`), Axios client chuẩn hóa `ApiError`, bộ component UI dùng chung (Button, Form, Modal, DataTable, Badge, Toaster), icon `lucide-react`.
  - **Làm lại giao diện bằng Astryx (12/11/2026):** React 19 + Tailwind 4; khung `AppShell` + `TopNav` + `SideNav` thu gọn được; form/bảng/hộp thoại/toast/phân trang dùng component Astryx; lớp mỏng `components/ui` (PageHeader, DataTable, Modal/ConfirmDialog, Badge, ListCard) giữ API cũ cho các trang; `LinkProvider` nối react-router; `ToastBridge` chuyển `toast.*` sang Astryx; nút chuyển giao diện sáng/tối (lưu theo người dùng). Kiểm thử lại bằng Chrome: tạo NV, xem chi tiết, Wi-Fi, sửa chi nhánh, chế độ tối, buộc đổi mật khẩu tạm, phân quyền hiển thị Manager/HR, menu tài khoản — không lỗi console.
  - Kiểm thử bằng trình duyệt thật (Chrome headless, playwright-core): 18 màn hình, luồng tạo NV → mật khẩu tạm → buộc đổi mật khẩu, phân quyền hiển thị theo Admin/HR/Manager/Employee.
- [x] **Task 1.7:** Mobile App: đăng nhập, lưu phiên bảo mật (SecureStore), khung điều hướng tab, màn hình hồ sơ cá nhân.
  - Định tuyến theo phiên bằng `Stack.Protected` (Expo Router): chưa đăng nhập → Đăng nhập / Quên mật khẩu; đang dùng **mật khẩu tạm** → chỉ màn hình đổi mật khẩu (không vào được tab, có nút đăng xuất); đã đăng nhập → 5 tab: Trang chủ, Lịch ca, Chấm công, Đơn từ, Cá nhân (+ màn hình Công việc, Tin nhắn chờ Cấp 4–5).
  - Phiên lưu trong `expo-secure-store` (iOS Keychain / Android Keystore) qua Zustand `persist`; giữ màn hình khởi động tới khi đọc xong phiên; `useSessionGuard`: token hết hạn → đăng xuất, mở lại app → đồng bộ `/auth/me` (bị khóa → 401 → đăng xuất; được cấp lại mật khẩu → buộc đổi mật khẩu).
  - Quên mật khẩu 2 bước (OTP 6 số, đếm ngược 60 giây gửi lại khớp backend), đổi mật khẩu chủ động, kiểm tra mật khẩu khớp `PasswordPolicy`.
  - Trang chủ: lời chào, đồng hồ, nút chấm công nhanh, thẻ "Ca làm hôm nay" (chờ Cấp 2), lối tắt. Hồ sơ: thông tin công việc, đổi ảnh đại diện (chụp hoặc chọn ảnh, cắt vuông, JPEG trên iOS), sửa SĐT (ràng buộc khớp backend), đổi mật khẩu, đăng xuất có xác nhận.
  - Kỹ thuật: MVVM (`useProfile` làm ViewModel), bộ component dùng chung (`Screen`, `Button`, `TextField`, `Notice`, `Card`, `Badge`, `Avatar`), theme màu đồng bộ Web Admin, icon `@expo/vector-icons`; `react-native-web` để xem thử trên trình duyệt (bản web lưu phiên vào localStorage).
  - Kiểm thử: `tsc` sạch, đóng gói Android (`expo export`) thành công; chạy bản web khổ 390×844 với backend thật (Chrome headless): 14 màn hình — chuyển hướng khi chưa đăng nhập, sai mật khẩu, giữ phiên sau khi tải lại, tab, sửa SĐT, tải ảnh đại diện, đăng xuất, NV mới bị buộc đổi mật khẩu tạm (không vào được tab), quên mật khẩu bằng OTP → đăng nhập bằng mật khẩu mới.

---

### 📌 CẤP BẬC 2: Khung ca, Xếp lịch & Dữ liệu Face ID `[6/6]` ✅ HOÀN THÀNH
> 🎯 **Demo:** Quản lý xếp và công bố lịch tuần trên Web; nhân viên xem lịch và đăng ký khuôn mặt trên Mobile.

- [x] **Task 2.1:** Entities & Repository `Shift`, `Schedule`, `FaceProfile`. Bổ sung schema: `shifts.branch_id` (NULL = ca dùng chung), số phút cho phép đi muộn/về sớm, trạng thái nháp/công bố của lịch (`schedules`).
  - *Đã làm (14/11/2026):* migration `002_shifts_schedules_face_profiles.sql` — `shifts`: `branch_id`, `late_grace_minutes`, `early_leave_grace_minutes` (mặc định 5), `updated_at`; `schedules`: `is_published`, `published_at`, `created_by`, `updated_at`, **bỏ xóa mềm** (lịch nháp xóa hẳn, lịch đã công bố chuyển `cancelled` — `ScheduleStatus`); `face_profiles`: `sample_image_public_id` (xóa ảnh cũ trên Cloudinary), `model_name` (`buffalo_l`), `created_at`.
  - Domain `Shift`: `Duration`, `WorkingMinutes`, `StartAt/EndAt` theo ngày (ca qua đêm), `OverlapsWith` (ca nối tiếp không tính chồng giờ) — nền cho kiểm tra chồng ca (Task 2.3) và tính đi muộn (Task 3.2). `FaceProfile.EmbeddingSize = 512`, vector `float[]` ↔ cột JSON qua `FloatArrayJsonTypeHandler`.
  - Repository: `ShiftRepository` (lọc theo chi nhánh ± ca dùng chung, trùng mã, ca đang được dùng), `ScheduleRepository` (lưới lịch JOIN nhân viên + ca, INSERT nhiều dòng, công bố theo khoảng ngày, lịch hiệu lực của nhân viên), `FaceProfileRepository` (upsert theo `user_id`, danh sách đã đăng ký). Test: 87 unit + 37 tích hợp đạt; migration chạy trên schema cũ cho kết quả trùng khớp schema mới.
- [x] **Task 2.2:** API Khung ca & Quy tắc vi phạm: ca hành chính, ca xoay, ca gãy, ca đêm qua ngày, giờ nghỉ, hệ số công; cấu hình mức trừ công/phạt khi đi muộn, về sớm.
  - *Đã làm (14/11/2026):* API `/api/shifts` (danh sách lọc chi nhánh ± ca dùng chung, chi tiết kèm `isInUse`, `available/{branchId}` cho xếp lịch, thêm/sửa/xóa; giờ dạng `HH:mm`, tự tính ca qua đêm & số phút làm việc, cờ `canEdit` theo quyền) và `/api/attendance-policy` (GET: Admin/HR/Quản lý; PUT: Admin/HR — lưu số công chuẩn + thay toàn bộ bậc trong một transaction). Migration `003_violation_rules_system_settings.sql` (kèm dữ liệu mặc định). Domain `ViolationPolicy` (phút muộn/sớm làm tròn xuống, so với số phút cho phép, chọn bậc) dùng lại ở Task 3.2 / 5.3. Test: 102 unit + 41 tích hợp; thử API thật bằng tài khoản Quản lý / HR / Nhân viên.
- [x] **Task 2.3:** API Phân ca & Xếp lịch: xếp theo tuần/tháng, chống trùng và chồng giờ giữa các ca, chuyển nháp → công bố, xem lịch cá nhân / chi nhánh.
  - *Đã làm (14/11/2026):* API `/api/schedules` — `GET` lưới lịch chi nhánh (kể cả nháp/đã hủy, tối đa 62 ngày), `GET me` lịch đã công bố của tôi (mặc định 14 ngày), `POST` xếp nhiều ca (≤ 500/lần, **có xung đột thì không lưu ca nào** và trả danh sách lý do), `POST copy` sao chép tuần/khoảng ngày (bỏ qua ca không hợp lệ/xung đột, báo lý do), `POST publish`, `DELETE drafts`, `DELETE {id}` (nháp → xóa hẳn; đã công bố & chưa tới giờ → `cancelled`). Ràng buộc: nhân viên đang hoạt động & thuộc đúng chi nhánh, ca dùng chung hoặc ca của chi nhánh, ngày từ hôm nay tới tối đa 366 ngày; Quản lý chỉ thao tác chi nhánh mình (gửi chi nhánh khác → 403). Domain `SchedulePlanner`: trùng ca, chồng giờ (kể cả ca đêm hôm trước kéo sang), ca gãy nối tiếp hợp lệ. Không cần migration. Test: 109 unit + 46 tích hợp; thử API thật bằng tài khoản Quản lý / Nhân viên.
- [x] **Task 2.4:** Đăng ký Face ID: nhận ảnh mẫu → lưu Cloudinary → `FW.AiService` trích xuất vector 512 chiều → lưu `face_profiles`; sửa vector mẫu trong seed cho đủ 512 chiều.
  - *Đã làm (15/11/2026):* nhận diện chạy trong backend (`OnnxFaceRecognitionService`: SCRFD → căn chỉnh 5 điểm mốc → ArcFace 512 chiều, xoay ảnh theo EXIF, ảnh > 1280 px thu nhỏ; đăng ký yêu cầu chỉ một người trong ảnh, mặt đủ lớn). API: `GET/POST /api/profile/face` (nhân viên tự đăng ký — **chỉ lần đầu**, chống tự đổi sang mặt người khác để nhờ chấm công hộ), `GET/DELETE /api/users/{id}/face` (Quản lý chi nhánh / HR xem & đặt lại). Ảnh mẫu lưu thư mục `faces` của kho ảnh, đặt lại thì xóa ảnh cũ; vector không trả về client. Dockerfile tự tải mô hình (kiểm tra SHA-256), `scripts/download-face-models.ps1` cho máy dev. Migration 004: `face_profiles.model_name` mặc định `buffalo_sc`. Test: 132 unit + 48 tích hợp (gồm nạp mô hình thật); thử image Docker thật với giới hạn Render free.
- [x] **Task 2.5:** Web Admin: quản lý khung ca & quy tắc; lưới xếp lịch theo tuần cho từng chi nhánh; công bố lịch.
  - *Đã làm (15/11/2026):* trang **Khung ca** (`/shifts`: danh sách lọc chi nhánh ± ca dùng chung, form có xem trước ca qua đêm / giờ làm thực tế, khóa trường giờ giấc khi ca đã được xếp lịch, ẩn nút sửa theo cờ `canEdit`); trang **Xếp lịch** (`/schedules`: lưới tuần nhân viên × 7 ngày, màu theo trạng thái nháp / đã công bố / hoàn thành / vắng / hủy, bấm ô để xếp một hoặc nhiều ca, **Xếp nhanh** nhiều người × nhiều ngày, sao chép tuần trước, công bố, xóa nháp, xem chi tiết → xóa nháp / hủy ca; lỗi xung đột hiện từng dòng); trang **Cài đặt** (`/settings`, Admin/HR: số công chuẩn + bảng bậc phạt đi muộn / về sớm). Đã chụp ảnh kiểm tra bằng tài khoản Quản lý và HR trên CSDL test.
- [x] **Task 2.6:** Mobile App: xem lịch ca cá nhân; chụp ảnh đăng ký Face ID.
  - *Đã làm (15/11/2026):* tab **Lịch ca** (tuần trước / này / sau, mỗi ngày một thẻ, ca qua đêm ghi "hôm sau", ca đã hủy gạch ngang + nhãn, chỉ lịch đã công bố); **Trang chủ** hiện ca hôm nay và nhắc đăng ký khuôn mặt khi chưa có; màn hình **Khuôn mặt (Face ID)** (hướng dẫn → camera trước có khung căn mặt, tự chọn cỡ ảnh ~1280 px → xem lại → gửi; đã đăng ký thì hiện ảnh mẫu + hướng dẫn nhờ Quản lý/HR đặt lại); mục Face ID trong Hồ sơ kèm trạng thái. Chạy được trên **Expo Go** (không cần bản build riêng). Đã chụp ảnh kiểm tra bằng bản web (khung iPhone) với camera giả lập phát ảnh mẫu — đăng ký thành công qua API thật.

---

### 📌 CẤP BẬC 3: LÕI CHẤM CÔNG 3 LỚP (3-Factor Attendance Engine) `[3/5]`
> 🎯 **Demo:** Nhân viên check-in/check-out trên điện thoại thật; Quản lý xem nhật ký kèm minh chứng trên Web.

- [x] **Task 3.1:** Entity `AttendanceLog`; helper tính khoảng cách Haversine và xác minh Wi-Fi BSSID (kèm unit test).
  - *Đã làm (15/11/2026):* thiết kế lại bảng `attendance_logs` (migration `005`): thêm ngày công `work_date`, sai số GPS, kết quả từng lớp ở **cả lượt vào và lượt ra** (khoảng cách, BSSID + có khớp không, độ tương đồng khuôn mặt cosine 0–1), mã thiết bị, thông tin duyệt (`reviewed_by/at`, `review_note`); `UNIQUE(schedule_id)` — mỗi ca tối đa một bản ghi, chặn hai yêu cầu check-in đồng thời ở tầng CSDL. Trạng thái `AttendanceStatus`: `valid` / `pending_approval` / `invalid`.
  - Domain: `Geofence` (Haversine, R = 6.371.008,8 m; kiểm tra tọa độ hợp lệ; `GeofenceResult.IsInside` — đúng bằng bán kính vẫn tính là trong vùng); `WifiVerifier` → `Matched` / `NotMatched` / `NoWifiInfo` / `NotRequired` (chi nhánh chưa khai báo Wi-Fi), vẫn lưu BSSID lạ làm minh chứng; `BssidFormat` chuyển xuống Domain, chấp nhận BSSID iOS bỏ số 0 đầu nhóm (`0:1a:...`), nhận diện địa chỉ giả `02:00:00:00:00:00` (Android thiếu quyền vị trí) — form Wi-Fi chi nhánh cũng từ chối địa chỉ này.
  - Repository `AttendanceLogRepository`: theo ca, lượt đang mở của nhân viên, nhân viên khác đã chấm công trên cùng thiết bị trong ngày.
  - Test: +37 unit test (Haversine: 1 độ vĩ, co theo cos vĩ độ, kinh tuyến 180, Hà Nội ↔ TP.HCM ≈ 1.140 km, biên bán kính; Wi-Fi; BSSID) + 3 integration test (minh chứng vào/ra + duyệt, trùng ca bị chặn, trùng thiết bị). Đã chạy thử migration `005` trên bản sao schema cũ: cột và index khớp schema mới.
- [x] **Task 3.2:** Engine Check-in / Check-out:
  - Kiểm tra 3 lớp: GPS $\le$ bán kính, Wi-Fi BSSID hợp lệ, Face ID match $\ge$ ngưỡng tin cậy (qua `FaceRecognitionClient`).
  - Xử lý khi một lớp không đạt (từ chối hoặc `pending_approval`) theo quyết định đã chốt.
  - So khớp với ca (`schedules`) để tính `late_minutes`, `early_minutes`, `actual_work_hours`; lưu đầy đủ minh chứng vào `attendance_logs`.
  - *Đã làm (15/11/2026):* API `GET /api/attendance/today` (việc cần làm tiếp theo: check-in / check-out / không có, kèm chi nhánh để app tự đo khoảng cách, các ca trong ngày + ca đêm hôm qua), `POST /api/attendance/check-in` và `check-out` (form-data: ảnh selfie, `latitude`, `longitude`, `accuracy`, `bssid`, `deviceId`, `isMocked`). Kiểm tra từ rẻ tới đắt: thời gian → GPS → khuôn mặt (ONNX) → Wi-Fi, dừng ở lớp bắt buộc đầu tiên không đạt; lượt bị từ chối không lưu gì và không tải ảnh lên.
  - Quy tắc thời gian (`AttendanceRules`, cấu hình mục `Attendance` trong appsettings): check-in từ 60' trước giờ vào tới trước giờ kết thúc ca (ca phải đã công bố, chưa check-in; ca gãy chọn ca bắt đầu sớm nhất còn mở); check-out tới 240' sau giờ kết thúc, quá hạn thì nhờ Quản lý (UC 4.7). Giờ làm thực tế chỉ tính phần có mặt trong khung ca, quy đổi theo tỉ lệ giờ làm/giờ có mặt để trừ giờ nghỉ (có mặt trọn ca = đúng số giờ làm của ca); đến sớm/về muộn không cộng thêm (OT đi theo đơn). Ca qua đêm check-out sáng hôm sau vẫn tính vào ngày bắt đầu ca.
  - Lịch chuyển `in_progress` khi check-in, `completed` khi check-out (cùng transaction với bản ghi chấm công); hai yêu cầu check-in đồng thời → `UNIQUE(schedule_id)` chặn, xóa ảnh thừa. Vector khuôn mặt mẫu khác mô hình / sai số chiều → yêu cầu đặt lại Face ID. Máy chủ cố định văn hóa số Invariant để đọc tọa độ dạng `21.0285` trong form-data.
  - Test: +15 unit test (`AttendanceRules`: khung giờ, ca gãy, ca đêm, giờ làm thực tế) + 6 integration test (trọn ca vào/ra, từng lớp bắt buộc từ chối, chưa có / Face ID cũ, Wi-Fi & trùng thiết bị → chờ duyệt, quá hạn check-out, ca đêm). Chạy thử API thật với ONNX: đứng cách 222 m bị từ chối, check-in đủ 3 lớp (muộn 38'), check-out Wi-Fi lạ → chờ duyệt.
- [x] **Task 3.3:** API Tra cứu Nhật ký Chấm công: nhân viên xem lịch sử bản thân; Quản lý / HR tra cứu theo chi nhánh kèm ảnh, tọa độ, khoảng cách, BSSID.
  - *Đã làm (15/11/2026):* `GET /api/attendance/me` (lịch sử của tôi, mặc định từ đầu tháng), `GET /api/attendance/logs` (Admin/HR mọi chi nhánh, Quản lý chi nhánh mình; lọc chi nhánh, nhân viên, khoảng ngày công — mặc định 7 ngày, tối đa 366 —, trạng thái, tìm theo tên/mã nhân viên; phân trang, mới nhất trước), `GET /api/attendance/logs/{id}` (chính chủ hoặc người quản lý chi nhánh: minh chứng lượt vào/ra — ảnh, tọa độ, sai số, khoảng cách, BSSID, độ tương đồng khuôn mặt, mã thiết bị — kèm tâm & bán kính chi nhánh, thông tin duyệt, cờ `canReview`). Đọc bằng một truy vấn JOIN nhân viên – chi nhánh – lịch – ca – người duyệt.
  - Làm luôn API duyệt cho UC 4.6 (Task 3.5 chỉ còn giao diện Web): `PUT /api/attendance/logs/{id}/review` `{approved, note}` — chỉ bản ghi `pending_approval`, trong phạm vi chi nhánh, **không tự duyệt bản ghi của mình** (bản ghi của Quản lý do HR/Admin duyệt), từ chối bắt buộc ghi lý do; chấp nhận → `valid`, từ chối → `invalid` (không tính công). Lượt ra có vấn đề Wi-Fi sau khi lượt vào đã được duyệt → về lại chờ duyệt, xóa thông tin duyệt cũ.
  - Test: +3 integration test (phạm vi theo vai trò & bộ lọc, chi tiết minh chứng, quy tắc duyệt); gọi thử qua HTTP: 403 khi Quản lý xem chi nhánh khác / nhân viên gọi `/logs`, 400 trạng thái sai / từ chối thiếu lý do.
- [ ] **Task 3.4:** Mobile App: màn hình chấm công (lấy GPS + BSSID + chụp selfie), hiển thị kết quả từng lớp xác minh; lịch sử chấm công cá nhân.
- [ ] **Task 3.5:** Web Admin: nhật ký chấm công có bộ lọc; xem chi tiết minh chứng; duyệt các bản ghi `pending_approval`.

---

### 📌 CẤP BẬC 4: Đơn từ, Phê duyệt & Công việc theo ca `[0/5]`
> 🎯 **Demo:** Nhân viên gửi đơn trên Mobile → Quản lý duyệt trên Web → quỹ phép và lịch tự cập nhật; checklist ca trực có ảnh minh chứng.

- [ ] **Task 4.1:** API Đơn từ: CRUD loại đơn (`leave_types`); tạo đơn nghỉ phép, đổi ca, OT (có giờ bắt đầu/kết thúc), giải trình đi muộn/về sớm kèm ảnh minh chứng. Bổ sung schema cho đơn OT.
- [ ] **Task 4.2:** Quy trình Phê duyệt (transaction): Quản lý duyệt theo chi nhánh, HR duyệt toàn bộ; tự động trừ quỹ phép (`leave_balances`), cập nhật `schedules` / `attendance_logs`, hoán đổi lịch khi đổi ca; HR điều chỉnh quỹ phép.
- [ ] **Task 4.3:** API Checklist công việc: mẫu checklist theo ca (`shift_tasks`); tự động gán khi nhân viên vào ca; tích hoàn thành kèm ảnh hiện trường; API tiến độ theo ca.
- [ ] **Task 4.4:** Mobile App: tạo & theo dõi đơn từ; to-do list ca trực, tích hoàn thành và chụp ảnh minh chứng.
- [ ] **Task 4.5:** Web Admin: duyệt/từ chối đơn, quản lý loại đơn & quỹ phép; quản lý checklist mẫu; giám sát tiến độ task theo ca.

---

### 📌 CẤP BẬC 5: Realtime, Bảng công, Bảng lương & Báo cáo `[0/10]`
> 🎯 **Demo:** Thông báo & chat tức thì giữa Web và Mobile; dashboard quân số realtime; chốt bảng công tháng → tính lương tạm tính → gửi phiếu lương; xuất Excel.

- [ ] **Task 5.1:** `NotificationHub` + bảng `notifications`: lưu và đẩy thông báo (kết quả duyệt đơn, lịch mới, check-in, task được giao); API danh sách, đánh dấu đã đọc.
- [ ] **Task 5.2:** `ChatHub` + API hội thoại: chat 1-1 (văn bản, hình ảnh), nhóm tự động theo chi nhánh, lịch sử tin nhắn. Nhóm chat tùy chỉnh `[Tùy chọn]`.
- [ ] **Task 5.3:** Thuật toán Tổng hợp Bảng công tháng (`monthly_timesheets`) từ `attendance_logs`, `schedules`, `leave_requests` (ngày công, giờ OT, phút vi phạm, nghỉ có/không lương, số ca gãy/đêm); quy trình Quản lý xác nhận → HR khóa bảng công (kèm unit test).
- [ ] **Task 5.4:** Cấu hình lương (UC 8.1): bổ sung schema — lương tháng / đơn giá giờ riêng theo nhân viên (`users`), danh mục phụ cấp & phụ cấp theo nhân viên, hệ số OT, kỳ lương (`payroll_periods`) và phiếu lương chi tiết từng khoản (`payslips`, `payslip_items`); API CRUD cấu hình.
- [ ] **Task 5.5:** Tính & chốt Bảng lương rút gọn (UC 8.2–8.4): gom dữ liệu từ bảng công đã khóa → `(công × đơn giá) + phụ cấp + OT − phạt` (kèm unit test); tính lại khi còn nháp; chốt kỳ lương (transaction); gửi phiếu lương HTML qua email (MailKit); API phiếu lương cá nhân.
- [ ] **Task 5.6:** Dashboard quân số realtime theo chi nhánh; báo cáo vi phạm (đi muộn, về sớm, vắng mặt); báo cáo sử dụng quỹ phép.
- [ ] **Task 5.7:** Xuất bảng công & bảng lương ra Excel (`.xlsx`, ClosedXML). Xuất PDF (QuestPDF) `[Tùy chọn]`.
- [ ] **Task 5.8:** Web Admin: dashboard (Recharts), bảng công & khóa công, cấu hình lương & phụ cấp, bảng lương & chốt kỳ, báo cáo, chat, trung tâm thông báo.
- [ ] **Task 5.9:** Mobile App: thông báo, chat, bảng công cá nhân trong tháng, phiếu lương cá nhân.
- [ ] **Task 5.10:** `[Tùy chọn]` Push Notification khi ứng dụng đang đóng (Expo Push).

---

### 📌 CẤP BẬC 6: Hoàn thiện, Triển khai & Thực nghiệm `[0/5]`
> 🎯 **Demo:** Hệ thống chạy trên môi trường cloud; có số liệu thực nghiệm cho Chương 4.

- [ ] **Task 6.1:** Tác vụ nền (`BackgroundService`): tự đánh dấu vắng mặt khi hết ca, xử lý quên check-out, khởi tạo quỹ phép đầu năm.
- [ ] **Task 6.2:** Gia cố nhận diện khuôn mặt: hiệu chỉnh ngưỡng khớp (`FaceRecognition:MatchThreshold`, mặc định 0,40) bằng ảnh thật ở Task 6.5; `FW.AiService` (phương án tách dịch vụ): tắt chế độ dự phòng (fallback) ngoài dev, xác thực bằng API key. Chống giả mạo khuôn mặt (liveness) `[Tùy chọn]`.
- [ ] **Task 6.3:** Triển khai: MySQL trên TiDB Cloud, Backend trên Render (giữ thức bằng UptimeRobot), nhận diện khuôn mặt chạy trong backend, Web trên Vercel, bản build Android (EAS) để demo.
  - *Đã chuẩn bị (12/11/2026):* `render.yaml` (API Docker + Key Value), `FW.Frontend/mobile/eas.json`, `docs/DEPLOY.md` (TiDB Cloud → Brevo → Cloudinary → Render → Vercel → EAS); backend: `BrevoEmailService`, `RedisConnectionParser` (URL `redis://`), `BootstrapAdminService` (Admin đầu tiên từ biến môi trường), `flexiwork_seed_production.sql` (chỉ vai trò + loại đơn), ForwardedHeaders sau proxy, cờ `Swagger:Enabled`; Dockerfile không phụ thuộc `global.json`. Kiểm thử chế độ Production cục bộ: tạo Admin tự động, đăng nhập qua Redis, tài khoản mẫu không tồn tại, CORS chỉ nhận domain khai báo, gọi được API Brevo. *Đã triển khai (13/11/2026):* Web <https://flexiwork-tlu.vercel.app> (Vercel, root `FW.Frontend/web`) — API <https://flexiwork-api.onrender.com> (Render Blueprint + Key Value) — CSDL TiDB Cloud Starter (Singapore) — email Brevo, ảnh Cloudinary; Admin đầu tiên tạo qua Bootstrap. Còn lại: AI Service (Task 2.4), EAS Build (Task 3.4).
- [ ] **Task 6.4:** Dữ liệu demo quy mô 30–80 nhân sự (seed mở rộng: nhiều chi nhánh, lịch 1 tháng, lịch sử chấm công, đơn từ). Gồm vài nhân viên có hành vi "xấu dần" (đi muộn, xin nghỉ tăng) để minh họa cảnh báo nghỉ việc (Task 7.4).
- [ ] **Task 6.5:** Thực nghiệm trên thiết bị thật: chấm công đúng vị trí, ngoài bán kính, dùng Fake GPS, sai Wi-Fi, người khác chụp hộ; đo tỷ lệ nhận diện và thời gian xử lý; đối soát bảng công — làm số liệu cho Chương 4. Trợ lý AI: bộ ~50 câu hỏi/yêu cầu mẫu — đo tỷ lệ trích xuất đúng ngày/ca/loại đơn, thời gian phản hồi và chi phí mỗi lượt.

---

### 📌 CẤP BẬC 7: Trợ lý AI & Phân tích `[0/6]`
> 🎯 **Demo:** Nhân viên nhắn "Cho em xin nghỉ ca sáng thứ 5 tuần này vì bận thi" → trợ lý tạo bản nháp đơn đúng ngày/ca → nhân viên xác nhận → Quản lý nhận đơn chờ duyệt. Dashboard của Quản lý hiện "Nhân sự cần quan tâm" kèm lý do.
> ⚠️ **Phụ thuộc:** làm sau Cấp 4–5 (cần đơn từ, quỹ phép, bảng công để trợ lý tra cứu và dữ liệu chấm công tích lũy để chấm điểm). Cấu hình: `docs/DEPLOY.md` Bước 8.

- [ ] **Task 7.1:** Module `Assistant` (backend): gọi Claude API bằng SDK chính thức cho C# với Function Calling; công cụ tra cứu tái sử dụng service sẵn có (lịch ca, bảng công, quỹ phép, trạng thái đơn, loại đơn) và **chạy với quyền của người hỏi**; lưu hội thoại (`assistant_conversations`, `assistant_messages`); giới hạn số lượt/người/ngày để kiểm soát chi phí; xử lý lỗi/từ chối của mô hình.
- [ ] **Task 7.2:** Tạo đơn qua hội thoại (UC 10.2): công cụ tạo **bản nháp** đơn nghỉ / đổi ca / OT (ngày tương đối như "thứ 5 tuần này" quy đổi theo giờ Việt Nam) → thẻ xác nhận → gửi qua API đơn từ (Task 4.1). Bộ kiểm thử ~50 câu mẫu.
- [ ] **Task 7.3:** Mobile App + Web Admin: màn hình chat với trợ lý (hiển thị câu trả lời dần dần), thẻ xác nhận đơn, lịch sử hội thoại.
- [ ] **Task 7.4:** Chấm điểm nguy cơ nghỉ việc (UC 10.3): tác vụ nền hằng tuần so 2 tuần gần nhất với nền 8 tuần trước của chính nhân viên (phút đi muộn, số đơn nghỉ, vắng mặt, xin đổi ca, nguyện vọng ca nếu có) → điểm 0–100 + lý do, lưu `attrition_risk_snapshots`; trọng số / ngưỡng cấu hình được (kèm unit test).
- [ ] **Task 7.5:** Web Admin (UC 10.4): widget "Nhân sự cần quan tâm" trên Dashboard, trang chi tiết xu hướng theo tuần; `[Tùy chọn]` LLM viết tóm tắt dễ hiểu cho Quản lý.
- [ ] **Task 7.6:** `[Tùy chọn]` Đăng ký nguyện vọng ca (UC 3.5): nhân viên đăng ký ca / ngày có thể làm; Quản lý xem khi xếp lịch; cung cấp tín hiệu "giảm đăng ký ca" cho Task 7.4.

---

### 📝 SONG SONG: Viết Khóa luận `[0/4]`
- [ ] **Task K.1:** Sửa Chương 1–2:
  - Loại bỏ nội dung còn sót từ dự án SOSMap: `Post`, `RescueTask`, `Comment`, "mạng xã hội cứu trợ", "SosReport", feed bài đăng, Flat Threading bình luận, FilterJson, Optimistic UI; Google Maps "tính thời gian đến điểm cứu hộ" → Leaflet/OpenStreetMap chọn tâm geofence.
  - Điền chỗ trống "mô hình (…)"; thống nhất Clean Architecture; chỉ dùng **SignalR** (bỏ Socket.io).
  - Bổ sung công nghệ đang dùng: Mobile (Expo, expo-camera/location, NetInfo, SecureStore), `FW.AiService` (FastAPI + InsightFace), xUnit, ClosedXML/QuestPDF, Recharts, Expo Push.
  - Mục Frontend: **React 18 → React 19**; **Tailwind CSS v4** (cascade layers, cấu hình bằng CSS); thêm **hệ thống thiết kế Astryx** (Meta): component có trợ năng (accessibility), design token qua CSS variables (Dynamic Theming), chế độ sáng/tối, đa ngôn ngữ; giải thích vì sao chọn design system thay vì tự viết component.
  - Mục 1.4.2: thêm phân hệ Bảng lương rút gọn và **Trợ lý AI & Phân tích** cho khớp `docs/UML.md`.
  - Chương 2: lý thuyết LLM & Function Calling (Claude API), phương pháp chấm điểm rủi ro có giải thích; triển khai: Render (API), Vercel (Web), TiDB Cloud (CSDL), nhận diện khuôn mặt bằng **ONNX Runtime trong backend** (FW.AiService là phương án tách dịch vụ), Brevo, Cloudinary.
- [ ] **Task K.2:** Chương 3 — **làm một lần sau khi hoàn thiện sản phẩm** (đã chốt 14/11/2026) để biểu đồ khớp code thật:
  - Quy trình TO-BE; danh sách tác nhân; biểu đồ UC tổng quát + UC phân rã 11 phân hệ theo `docs/UML.md` (đã bổ sung Phân hệ 0, Phân hệ 10 và các UC ★ — 55 UC); BFD.
  - Đặc tả UC dạng bảng ngắn cho mọi UC; đặc tả đầy đủ + **biểu đồ hoạt động** + **biểu đồ tuần tự** cho ~12–15 UC cốt lõi (đăng nhập, quên mật khẩu OTP, đổi mật khẩu, mời nhân viên, cấu hình geofence, xếp & công bố lịch, đăng ký Face ID, **chấm công 3 lớp**, duyệt chấm công chờ xác nhận, lập & duyệt đơn, tổng hợp bảng công, tính & chốt lương).
  - Biểu đồ lớp (theo entity/service thật), ERD & mô tả bảng (theo `flexiwork_schema.sql`), kiến trúc Clean Architecture & triển khai cloud.
  - Công cụ: **PlantUML** (`docs/diagrams/*.puml`, render PNG bằng Java có sẵn trên máy) — sửa được bằng code, tuần tự đi đúng lớp `Controller → Service → Repository`. Đầu ra: ảnh PNG + nội dung chương (đặc tả, lời dẫn hình) để chép vào Word; không sửa trực tiếp file `.docx`.
- [ ] **Task K.3:** Chương 4: môi trường cài đặt, giao diện Web Admin & Mobile App, kết quả thực nghiệm (từ Task 6.5).
- [ ] **Task K.4:** Chương 5 & phần đầu: kết luận, hạn chế, hướng phát triển; cập nhật tóm tắt, mục lục, danh mục viết tắt/hình/bảng; sửa lời cam đoan ("nhóm tác giả" → cá nhân).
