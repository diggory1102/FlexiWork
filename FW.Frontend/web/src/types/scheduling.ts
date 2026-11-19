import type { PagedQuery } from './api';

// ---------------------------------------------------------------- Face ID

/** Khớp FaceProfileStatusDto ở backend (không chứa vector đặc trưng). */
export interface FaceStatus {
  registered: boolean;
  sampleImageUrl: string | null;
  modelName: string | null;
  registeredAt: string | null;
  updatedAt: string | null;
}

// ---------------------------------------------------------------- Khung ca

/** Khớp ShiftDto ở backend. Giờ dạng "HH:mm". */
export interface Shift {
  id: number;
  shiftCode: string;
  name: string;
  /** null = ca dùng chung mọi chi nhánh. */
  branchId: number | null;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  /** Số phút làm việc thực tế (đã trừ giờ nghỉ). */
  workingMinutes: number;
  workUnits: number;
  isNightShift: boolean;
  isCrossDay: boolean;
  lateGraceMinutes: number;
  earlyLeaveGraceMinutes: number;
  /** Số người tối thiểu mỗi ngày (mỗi chi nhánh); chưa đủ thì cảnh báo. null = không yêu cầu. */
  minStaff: number | null;
  /** Số người tối đa mỗi ngày (mỗi chi nhánh); đủ rồi thì không xếp thêm được. null = không giới hạn. */
  maxStaff: number | null;
  /** Người đang xem được sửa/xóa ca này không. */
  canEdit: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ShiftDetail {
  shift: Shift;
  /** Đã được xếp lịch → không đổi được giờ giấc, hệ số công, ca đêm, chi nhánh. */
  isInUse: boolean;
}

export interface ShiftFilter extends PagedQuery {
  branchId?: number | '';
  includeShared?: boolean;
}

export interface ShiftRequest {
  shiftCode: string;
  name: string;
  branchId: number | null;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  workUnits: number;
  isNightShift: boolean;
  lateGraceMinutes: number;
  earlyLeaveGraceMinutes: number;
  minStaff: number | null;
  maxStaff: number | null;
}

// ---------------------------------------------------------------- Quy tắc chấm công

export type ViolationType = 'late' | 'early_leave';

export interface ViolationRule {
  id: number;
  violationType: ViolationType;
  minMinutes: number;
  deductWorkUnits: number;
  penaltyAmount: number;
  description: string | null;
}

export interface AttendancePolicy {
  standardWorkDays: number;
  rules: ViolationRule[];
}

export interface AttendancePolicyRequest {
  standardWorkDays: number;
  rules: Omit<ViolationRule, 'id'>[];
}

// ---------------------------------------------------------------- Lịch làm việc

export type ScheduleStatus = 'scheduled' | 'in_progress' | 'completed' | 'absent' | 'cancelled' | 'on_leave';

/** Khớp ScheduleDto ở backend. workDate dạng yyyy-MM-dd. */
export interface Schedule {
  id: number;
  userId: number;
  employeeCode: string;
  fullName: string;
  shiftId: number;
  shiftCode: string;
  shiftName: string;
  startTime: string;
  endTime: string;
  isCrossDay: boolean;
  branchId: number;
  workDate: string;
  status: ScheduleStatus;
  isPublished: boolean;
  publishedAt: string | null;
}

export interface ScheduleRange {
  branchId?: number;
  fromDate: string;
  toDate: string;
}

export interface ScheduleItem {
  userId: number;
  shiftId: number;
  workDate: string;
}

export interface ScheduleBatchResult {
  created: number;
  skipped: number;
  skippedReasons: string[];
}
