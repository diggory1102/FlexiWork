/**
 * Bảng màu Web Admin FlexiWork — nguồn duy nhất của mọi mã màu. Mỗi giá trị là cặp [sáng, tối].
 * Chỉ chứa dữ liệu; việc gắn vào design token của Astryx nằm ở `flexiworkTheme.ts`.
 */
export type Pair = [light: string, dark: string];

/** Tầng bề mặt, từ nền trang đến trạng thái hàng bảng. */
export const surface = {
  /** Sidebar và thanh trên. */
  chrome: ['#F1E9D6', '#14100E'] as Pair,
  /** Nền trang (vùng nội dung). */
  body: ['#F8F4EA', '#1A1513'] as Pair,
  /** Card, bảng, ô nhập. */
  card: ['#FFFDF8', '#221C19'] as Pair,
  /** Dropdown, popover, modal. */
  popover: ['#FFFDF8', '#2C2421'] as Pair,
  /** Tiêu đề bảng, vùng nhấn nhẹ. */
  header: ['#F3ECDC', '#2E2521'] as Pair,
  /** Hàng được chọn / mục menu đang mở. */
  selected: ['#F3DED5', '#4A231E'] as Pair,
  /** Lớp phủ khi rê chuột (trong suốt, đè lên bề mặt bên dưới). */
  hover: ['rgba(129, 28, 22, 0.05)', 'rgba(240, 140, 128, 0.09)'] as Pair,
  pressed: ['rgba(129, 28, 22, 0.11)', 'rgba(240, 140, 128, 0.16)'] as Pair,
  /** Nền nút phụ, nút biểu tượng. */
  neutral: ['rgba(92, 58, 30, 0.09)', 'rgba(241, 233, 221, 0.10)'] as Pair,
};

export const text = {
  primary: ['#241A16', '#F1E9DD'] as Pair,
  secondary: ['#5E4F45', '#BDB0A4'] as Pair,
  disabled: ['#A3968A', '#756A61'] as Pair,
};

export const line = {
  /** Viền card, đường kẻ bảng (trang trí). */
  subtle: ['#E3D9C3', '#41362F'] as Pair,
  /** Viền ô nhập và control: đạt ≥ 3:1. */
  control: ['#8C7C68', '#877669'] as Pair,
};

/** Dải thương hiệu ở trang đăng nhập: giữ màu đỏ của logo ở cả hai chế độ. */
export const brandPanel = {
  band: '#811C16',
  bandText: '#FDF6E9',
};

/** Màu nhấn duy nhất. */
export const brand = {
  accent: ['#811C16', '#F09A8C'] as Pair,
  onAccent: ['#FDF6E9', '#2A0A07'] as Pair,
};

/**
 * Họ màu cho nhãn (Badge / Token): nền nhạt + chữ đậm. Tên khóa trùng tên biến thể màu của Astryx.
 * Quy ước nghiệp vụ — ba nhóm không dùng chung màu:
 *   a) Trạng thái công : teal = đúng giờ · orange = đi muộn, về sớm · pink = vắng, thiếu check-in/out · blue = nghỉ phép
 *   b) Kết quả xác thực: green = hợp lệ · yellow = chờ duyệt · red = không hợp lệ
 *   c) Phương thức GPS / Wi-Fi / Face ID: gray (trung tính, kèm biểu tượng + nhãn)
 * `cyan` dự phòng cho phân loại. `purple` được đổi thành nâu xám để giao diện không còn màu tím.
 */
export const hue = {
  teal: { bg: ['#D3EFEF', '#0F3539'] as Pair, text: ['#0F5A63', '#7FD8DD'] as Pair },
  orange: { bg: ['#FFE0CC', '#432413'] as Pair, text: ['#92340B', '#FDB285'] as Pair },
  pink: { bg: ['#F7D6E6', '#451830'] as Pair, text: ['#8A1650', '#F7A6CA'] as Pair },
  blue: { bg: ['#DBE6FD', '#1B2F55'] as Pair, text: ['#1E429F', '#ABC8FF'] as Pair },
  green: { bg: ['#D8F0DC', '#163722'] as Pair, text: ['#175C2C', '#8CE0A4'] as Pair },
  yellow: { bg: ['#FCEBA3', '#3D3208'] as Pair, text: ['#664700', '#F2D05A'] as Pair },
  red: { bg: ['#FBDCDA', '#471A18'] as Pair, text: ['#9B1C14', '#FFA9A1'] as Pair },
  gray: { bg: ['#EDE6D6', '#362E29'] as Pair, text: ['#4A3F35', '#D3C8BB'] as Pair },
  cyan: { bg: ['#D6ECF8', '#11303F'] as Pair, text: ['#075379', '#8FCDEE'] as Pair },
  purple: { bg: ['#E6E1D8', '#332F2B'] as Pair, text: ['#433C33', '#C9C0B4'] as Pair },
};

/** Màu đậm của thông báo hệ thống (nút xóa, banner, chấm trạng thái). */
export const status = {
  success: { fill: ['#15803D', '#4CC774'] as Pair, on: ['#FFFFFF', '#06210F'] as Pair, text: hue.green.text },
  warning: { fill: ['#F0B400', '#F2C230'] as Pair, on: ['#151A21', '#151A21'] as Pair, text: hue.yellow.text },
  error: { fill: ['#B91C1C', '#F47067'] as Pair, on: ['#FFFFFF', '#260806'] as Pair, text: hue.red.text },
};

/**
 * Dãy màu phân loại cho biểu đồ (thứ tự cố định; đã kiểm tra phân biệt được khi mù màu).
 * Với biểu đồ chấm công: teal = có mặt, orange = đi muộn / về sớm, pink = vắng, blue = số lượng chung.
 */
export const chart = {
  blue: ['#1D4ED8', '#4F8EF7'] as Pair,
  orange: ['#B45309', '#D9731F'] as Pair,
  teal: ['#12A594', '#2BB3A3'] as Pair,
  pink: ['#A31354', '#B83A6B'] as Pair,
  brown: ['#A07A12', '#A8871F'] as Pair,
  cyan: ['#4A8FC2', '#4BA3D8'] as Pair,
  neutral: ['#8C7C68', '#9A8B7D'] as Pair,
};
