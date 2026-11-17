import type { PagedQuery } from './api';

/** Kiểu dữ liệu nhật ký chấm công — khớp các DTO trong FW.Application/Features/Attendance. Giờ dạng ISO giờ Việt Nam. */

export type AttendanceStatus = 'valid' | 'pending_approval' | 'invalid';

/** Một dòng nhật ký chấm công (AttendanceLogItemDto). */
export interface AttendanceLogItem {
  id: number;
  scheduleId: number | null;
  userId: number;
  employeeCode: string;
  fullName: string;
  avatarUrl: string | null;
  branchId: number;
  branchName: string;
  workDate: string;
  shiftCode: string | null;
  shiftName: string | null;
  shiftStartTime: string | null;
  shiftEndTime: string | null;
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
  checkinFaceScore: number | null;
  checkoutFaceScore: number | null;
  checkinImageUrl: string;
  checkoutImageUrl: string | null;
  /** Chi nhánh có khai báo Wi-Fi → lớp 2 có áp dụng. */
  branchRequiresWifi: boolean;
}

/** Minh chứng của một lượt vào hoặc ra. */
export interface AttendanceEvidence {
  time: string;
  imageUrl: string | null;
  latitude: number | null;
  longitude: number | null;
  accuracyMeters: number | null;
  distanceMeters: number | null;
  bssid: string | null;
  wifiMatched: boolean;
  faceMatched: boolean;
  faceScore: number | null;
  deviceId: string | null;
}

export interface AttendanceLogDetail {
  log: AttendanceLogItem;
  checkIn: AttendanceEvidence;
  checkOut: AttendanceEvidence | null;
  branch: { id: number; name: string; latitude: number; longitude: number; radius: number };
  reviewedBy: number | null;
  reviewerName: string | null;
  reviewedAt: string | null;
  reviewNote: string | null;
  canReview: boolean;
}

export interface AttendanceLogFilter extends PagedQuery {
  branchId?: number | '';
  userId?: number;
  fromDate?: string;
  toDate?: string;
  status?: AttendanceStatus | '';
}
