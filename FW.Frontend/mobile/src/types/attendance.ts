/** Kiểu dữ liệu chấm công — khớp các DTO trong FW.Application/Features/Attendance. Giờ dạng ISO giờ Việt Nam (không có múi giờ). */

export type AttendanceStatus = 'valid' | 'pending_approval' | 'invalid';
export type AttendanceAction = 'check_in' | 'check_out' | 'none';
export type WifiCheckStatus = 'matched' | 'not_matched' | 'no_wifi_info' | 'not_required';

export interface AttendanceLogSummary {
  id: number;
  checkinTime: string;
  checkoutTime: string | null;
  status: AttendanceStatus;
  lateMinutes: number;
  earlyMinutes: number;
  actualWorkHours: number;
  note: string | null;
}

export interface AttendanceShift {
  scheduleId: number;
  workDate: string;
  shiftCode: string;
  shiftName: string;
  startAt: string;
  endAt: string;
  checkInOpensAt: string;
  checkOutDeadline: string;
  scheduleStatus: string;
  log: AttendanceLogSummary | null;
}

export interface AttendanceBranch {
  id: number;
  name: string;
  latitude: number;
  longitude: number;
  radius: number;
  requiresWifi: boolean;
}

export interface AttendanceToday {
  now: string;
  faceRegistered: boolean;
  nextAction: AttendanceAction;
  actionScheduleId: number | null;
  message: string;
  branch: AttendanceBranch | null;
  shifts: AttendanceShift[];
}

/** Dữ liệu thiết bị gửi kèm ảnh selfie. */
export interface AttendanceCapture {
  latitude: number;
  longitude: number;
  accuracy: number | null;
  bssid: string | null;
  deviceId: string | null;
  isMocked: boolean;
}

export interface AttendanceResult {
  logId: number;
  scheduleId: number | null;
  action: 'check_in' | 'check_out';
  time: string;
  shiftName: string;
  status: AttendanceStatus;
  lateMinutes: number;
  earlyMinutes: number;
  actualWorkHours: number;
  gps: { passed: boolean; distanceMeters: number; radiusMeters: number; accuracyMeters: number | null };
  wifi: { passed: boolean; status: WifiCheckStatus; bssid: string | null };
  face: { passed: boolean; score: number; threshold: number };
  warnings: string[];
  message: string;
}

/** Một dòng lịch sử chấm công (AttendanceLogItemDto). */
export interface AttendanceLogItem {
  id: number;
  scheduleId: number | null;
  workDate: string;
  shiftCode: string | null;
  shiftName: string | null;
  shiftStartTime: string | null;
  shiftEndTime: string | null;
  branchName: string;
  checkinTime: string;
  checkoutTime: string | null;
  lateMinutes: number;
  earlyMinutes: number;
  actualWorkHours: number;
  status: AttendanceStatus;
  note: string | null;
  checkinDistance: number;
  checkoutDistance: number | null;
  checkinWifiMatched: boolean;
  checkoutWifiMatched: boolean | null;
  checkinImageUrl: string;
  checkoutImageUrl: string | null;
}
