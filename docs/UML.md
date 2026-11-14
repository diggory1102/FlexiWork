> **Cập nhật 14/11/2026:** bổ sung **Phân hệ 0 – Xác thực & Tài khoản** và các UC còn thiếu so với chức năng đã/sẽ cài đặt (đánh dấu ★); UC 1.6 sửa thành 4 vai trò (thêm HR). Tổng cộng **50 UC / 10 phân hệ**. Biểu đồ UC, đặc tả, activity, sequence sẽ vẽ một lần sau khi hoàn thiện sản phẩm (Task K.2).

#### **Phân hệ 0: Xác thực &amp; Tài khoản (04 UCs)** ★

* **UC 0.1** **\- Đăng nhập hệ thống:** Đăng nhập bằng email và mật khẩu trên Web Admin hoặc Mobile App, nhận phiên làm việc (JWT); tạm khóa đăng nhập 15 phút sau 5 lần nhập sai; tài khoản đang dùng mật khẩu tạm bị chuyển bắt buộc sang UC 0.3. *(Actor: Admin, HR, Manager, Nhân viên)*
* **UC 0.2** **\- Đăng xuất:** Kết thúc phiên làm việc trên thiết bị hiện tại và xóa phiên đã lưu (SecureStore trên điện thoại). *(Actor: Admin, HR, Manager, Nhân viên)*
* **UC 0.3** **\- Đổi mật khẩu:** Nhập mật khẩu hiện tại (hoặc mật khẩu tạm ở lần đăng nhập đầu) và mật khẩu mới đạt quy tắc độ mạnh; hệ thống thu hồi phiên trên các thiết bị khác và gửi email thông báo cho chủ tài khoản. *(Actor: Admin, HR, Manager, Nhân viên)*
* **UC 0.4** **\- Quên mật khẩu (OTP qua email):** Yêu cầu mã OTP 6 số gửi về email (hiệu lực 5 phút, tối đa 5 lần nhập sai, chờ 60 giây giữa hai lần gửi), nhập OTP và đặt mật khẩu mới; hệ thống không tiết lộ email nào tồn tại. *(Actor: Admin, HR, Manager, Nhân viên)*

---

#### **Phân hệ 1: Quản lý Hệ thống &amp; Danh mục (06 UCs)**

* **UC 1.1** **\- Cấu hình thông tin Công ty / Chi nhánh:** Khai báo thông tin hành chính, tên, mã chi nhánh, địa chỉ, số điện thoại của từng cơ sở[12]. *(Actor: Admin / HR)*
* **UC 1.2** **\- Cấu hình tọa độ GPS Geofencing &amp; Wi-Fi BSSID:** Thiết lập tâm tọa độ vĩ độ/kinh độ, bán kính cho phép (mét) và địa chỉ MAC/BSSID Wi-Fi cho từng chi nhánh[14][26]. *(Actor: Admin / HR)*
* **UC 1.3** **\- Cấu hình Loại ca làm việc:** Khai báo khung giờ các loại ca (hành chính, ca xoay, ca gãy, ca bán thời gian), giờ vào/ra chuẩn và giờ nghỉ giữa ca[12]. *(Actor: Admin / HR)*
* **UC 1.4** **\- Cấu hình Loại đơn từ:** Thiết lập danh mục các loại đơn (nghỉ phép có/không lương, đổi ca, làm thêm giờ OT, giải trình đi muộn/về sớm) và quy trình duyệt[19]. *(Actor: Admin / HR)*
* **UC 1.5** **\- Cấu hình Số công chuẩn &amp; Quy tắc phạt:** Thiết lập định mức công chuẩn trong tháng, số phút cho phép đi muộn/về sớm và mức trừ tiền/trừ công tương ứng[19]. *(Actor: Admin / HR)*
* **UC 1.6** **\- Phân quyền người dùng (RBAC):** Hệ thống có 4 vai trò cố định (Admin, HR, Manager, Nhân viên); Admin/HR gán vai trò cho tài khoản, Manager bắt buộc gắn với một chi nhánh và chỉ thao tác trên dữ liệu chi nhánh đó[12][13]. *(Actor: Admin / HR)*

---

#### **Phân hệ 2: Quản lý Hồ sơ Nhân sự (05 UCs)**

* **UC 2.1** **\- Quản lý Danh mục Phòng ban / Chức vụ:** Thêm, sửa, xóa, tìm kiếm cơ cấu phòng ban và chức danh chuyên môn[12]. *(Actor: Admin / HR)*
* **UC 2.2** **\- Quản lý Danh sách Hồ sơ Nhân viên:** Quản lý thông tin cá nhân, hợp đồng, chi nhánh làm việc và trạng thái tài khoản (Đang hoạt động/Khóa)[12]. *(Actor: Admin / HR)*
* **UC 2.3** **\- Mời nhân viên &amp; Khởi tạo tài khoản:** Tạo tài khoản mới, hệ thống tự động gửi email mời kích hoạt và cấp mật khẩu ban đầu[19][20]. *(Actor: Admin / HR)*
* **UC 2.4** **\- Cập nhật Hồ sơ &amp; Dữ liệu Face ID:** Nhân viên tự cập nhật thông tin cá nhân và chụp/tải lên ảnh khuôn mặt mẫu phục vụ đối soát sinh trắc học[14]. *(Actor: Nhân viên)*
* **UC 2.5** **\- Cấp lại mật khẩu tạm thời:** Khi nhân viên quên hoặc bị lộ mật khẩu, HR/Admin cấp mật khẩu tạm mới gửi qua email; mọi phiên đăng nhập cũ của nhân viên bị thu hồi và nhân viên phải đổi mật khẩu ở lần đăng nhập tới. ★ *(Actor: Admin / HR)*

---

#### **Phân hệ 3: Quản lý Lịch &amp; Ca làm việc (04 UCs)**

* **UC 3.1** **\- Khai báo &amp; Thiết lập khung ca làm việc:** Tạo mới và điều chỉnh các khung ca kíp linh hoạt áp dụng theo từng chi nhánh/cửa hàng[12]. *(Actor: Admin / Manager)*
* **UC 3.2** **\- Phân ca &amp; Xếp lịch làm việc:** Thao tác xếp lịch phân ca cho nhân viên full-time/part-time theo chu kỳ tuần hoặc tháng[12]. *(Actor: Manager)*
* **UC 3.3** **\- Phê duyệt &amp; Xuất bản lịch làm việc:** Chốt danh sách phân ca và công bố lịch làm việc chính thức lên hệ thống[19][20]. *(Actor: Manager / HR)*
* **UC 3.4** **\- Tra cứu Lịch làm việc:** Xem chi tiết lịch ca cá nhân trên ứng dụng di động hoặc xem lịch làm việc của toàn chi nhánh[8]. *(Actor: Nhân viên, Manager)*

---

#### **Phân hệ 4: Quản lý Chấm công Online (07 UCs)**

* **UC 4.1** **\- Thực hiện Check-in / Check-out:** Thao tác bấm chấm công đầu/cuối ca trên điện thoại cá nhân bằng cách chụp ảnh Selfie[12]. *(Actor: Nhân viên)*
* **UC 4.2** **\- Xác minh Vị trí &amp; Sinh trắc học (Tự động):** Hệ thống tự động kiểm tra ảnh Selfie qua nhận diện khuôn mặt Face ID, đo khoảng cách GPS Geofencing và kiểm tra BSSID Wi-Fi nội bộ[1]. *(Actor: Hệ thống)*
* **UC 4.3** **\- Xem Bảng công cá nhân:** Tra cứu tổng số giờ công, ngày công thực tế, số lần đi muộn/về sớm trong tháng của bản thân[8]. *(Actor: Nhân viên)*
* **UC 4.4** **\- Quản lý Bảng công chi nhánh:** Theo dõi, kiểm tra và duyệt bảng chấm công tổng hợp của toàn bộ nhân viên thuộc cơ sở phụ trách[19]. *(Actor: Manager / HR)*
* **UC 4.5** **\- Tra cứu Nhật ký chấm công (Attendance Logs):** Truy vấn lịch sử chi tiết từng bản ghi điểm danh (thời gian thực, ảnh chụp selfie, tọa độ GPS, địa chỉ MAC)[32][33]. *(Actor: Manager / HR)*
* **UC 4.6** **\- Duyệt bản ghi chấm công chờ xác nhận:** Xem minh chứng (ảnh selfie, tọa độ, khoảng cách, BSSID) của các lượt chấm công ở trạng thái *chờ duyệt* (VD: sai Wi-Fi, nghi trùng thiết bị) và chấp nhận hoặc từ chối. ★ *(Actor: Manager / HR)*
* **UC 4.7** **\- Tự động xử lý vắng mặt &amp; quên check-out:** Hết ca mà nhân viên không check-in thì đánh dấu vắng mặt; có check-in nhưng không check-out thì gắn cờ quên check-out để quản lý xử lý. ★ *(Actor: Hệ thống)*

---

#### **Phân hệ 5: Quản lý Yêu cầu &amp; Đơn từ (06 UCs)**

* **UC 5.1** **\- Lập &amp; Gửi đơn từ trực tuyến:** Tạo và gửi đơn xin nghỉ phép, đổi ca, đăng ký OT hoặc giải trình đi muộn/về sớm kèm lý do/tệp minh chứng[16]. *(Actor: Nhân viên)*
* **UC 5.2** **\- Theo dõi trạng thái đơn từ cá nhân:** Theo dõi tiến trình xử lý (Chờ duyệt, Đã duyệt, Từ chối) của các đơn đã nộp[23][28]. *(Actor: Nhân viên)*
* **UC 5.3** **\- Phê duyệt / Từ chối đơn từ:** Cửa hàng trưởng hoặc HR tiếp nhận, xem chi tiết nội dung và bấm Phê duyệt hoặc Từ chối kèm ghi chú phản hồi[16]. *(Actor: Manager / HR)*
* **UC 5.4** **\- Tự động cập nhật công &amp; Quỹ phép:** Sau khi đơn được duyệt, hệ thống tự động trừ quỹ ngày phép khả dụng và gán trạng thái nghỉ hợp lệ lên lịch trình/bảng công[16]. *(Actor: Hệ thống)*
* **UC 5.5** **\- Quản lý Quỹ ngày phép:** Khởi tạo quỹ phép đầu năm cho từng nhân viên, HR điều chỉnh thủ công (cộng/trừ kèm lý do), nhân viên xem quỹ phép còn lại. ★ *(Actor: HR, Nhân viên)*
* **UC 5.6** **\- Xác nhận đơn đổi ca của đồng nghiệp:** Nhân viên được đề nghị đổi ca đồng ý hoặc từ chối trước khi đơn được chuyển tới Manager duyệt. ★ *(Actor: Nhân viên)*

---

#### **Phân hệ 6: Quản lý Công việc theo ca (Shift Task Checklist) (04 UCs)**

* **UC 6.1** **\- Giao Task Checklist theo ca làm:** Thiết lập danh mục các đầu việc chuẩn cần làm đầu ca/trong ca/cuối ca gán cho từng vị trí làm việc[16]. *(Actor: Manager)*
* **UC 6.2** **\- Theo dõi To-do List cá nhân:** Xem danh sách các đầu việc được giao cần hoàn thành trong ca trực hiện tại[16]. *(Actor: Nhân viên)*
* **UC 6.3** **\- Tích chọn hoàn thành Task kèm ảnh minh chứng:** Nhân viên tích chọn hoàn thành đầu việc và chụp ảnh hiện trường (ví dụ: quầy đã dọn sạch) gửi lên hệ thống[16][24]. *(Actor: Nhân viên)*
* **UC 6.4** **\- Báo cáo tiến độ Task theo ca:** Quản lý theo dõi trực quan tỷ lệ hoàn thành công việc của từng nhân viên và toàn cửa hàng theo thời gian thực[16]. *(Actor: Manager)*

---

#### **Phân hệ 7: Truyền thông &amp; Chat Realtime (05 UCs)**

* **UC 7.1** **\- Chat cá nhân 1-1 (Direct Chat):** Gửi tin nhắn văn bản, hình ảnh trực tiếp giữa hai nhân sự trong nội bộ doanh nghiệp[17]. *(Actor: Nhân viên, Manager, HR)*
* **UC 7.2** **\- Quản lý Nhóm chat (Group Chat):** Tạo nhóm trao đổi tùy chỉnh, thêm/xóa thành viên và đổi tên nhóm[19][24]. *(Actor: Nhân viên, Manager)*
* **UC 7.3** **\- Tự động đồng bộ Nhóm chat:** Hệ thống tự động tạo và cập nhật danh sách thành viên các nhóm chat theo Chi nhánh / Phòng ban[24]. *(Actor: Hệ thống)*
* **UC 7.4** **\- Đẩy Thông báo tức thì (Push Notification):** Phát thông báo Realtime qua SignalR khi có tin nhắn mới, lịch ca mới hoặc kết quả duyệt đơn[13]. *(Actor: Hệ thống)*
* **UC 7.5** **\- Xem &amp; quản lý Thông báo:** Xem danh sách thông báo (duyệt đơn, lịch mới, task được giao), số thông báo chưa đọc, đánh dấu đã đọc. ★ *(Actor: Admin, HR, Manager, Nhân viên)*

---

#### **Phân hệ 8: Quản lý Bảng lương (Rút gọn) (05 UCs)**

* **UC 8.1** **\- Cấu hình Mức lương &amp; Phụ cấp:** Khai báo mức lương cơ bản, đơn giá công theo giờ/ca, phụ cấp ca gãy/đêm và định mức phạt[19][20]. *(Actor: HR / Admin)*
* **UC 8.2** **\- Tự động tổng hợp dữ liệu Bảng công:** Hệ thống gom dữ liệu công nhật thực tế, giờ OT, số ca làm và số lần phạt từ Phân hệ 4 &amp; 5[17]. *(Actor: Hệ thống)*
* **UC 8.3** **\- Tính toán Bảng lương tạm tính:** Tự động tính: *(Tổng số công thực tế × Đơn giá) + Phụ cấp + OT - Tiền phạt*[19][20]. *(Actor: HR / Admin)*
* **UC 8.4** **\- Chốt &amp; Xuất phiếu lương:** Chốt dữ liệu kỳ lương, gửi phiếu lương điện tử cho nhân viên và xuất file Excel cho kế toán[17]. *(Actor: HR / Admin)*
* **UC 8.5** **\- Xem Phiếu lương cá nhân:** Nhân viên xem chi tiết phiếu lương các kỳ đã chốt (công, đơn giá, phụ cấp, OT, khoản phạt) trên ứng dụng di động. ★ *(Actor: Nhân viên)*

---

#### **Phân hệ 9: Báo cáo &amp; Thống kê (04 UCs)**

* **UC 9.1** **\- Thống kê Dashboard Tổng quan:** Biểu đồ trực quan hóa tỷ lệ nhân sự đi làm đúng giờ, tổng số ca làm việc và đơn từ chờ duyệt thời gian thực[17]. *(Actor: Manager / HR / Admin)*
* **UC 9.2** **\- Báo cáo Đi muộn / Về sớm:** Thống kê chi tiết danh sách và tỷ lệ vi phạm giờ giấc làm việc của nhân sự theo từng cơ sở[19]. *(Actor: Manager / HR)*
* **UC 9.3** **\- Báo cáo Sử dụng Quỹ ngày phép:** Theo dõi chi tiết số ngày phép đã sử dụng và quỹ ngày phép còn lại trong năm của từng nhân viên[23][31]. *(Actor: HR)*
* **UC 9.4** **\- Trích xuất Bảng công ra Excel:** Xuất toàn bộ dữ liệu bảng công tổng hợp chi tiết theo tháng ra định dạng file `.xlsx` chuẩn[17]. *(Actor: HR / Admin)*