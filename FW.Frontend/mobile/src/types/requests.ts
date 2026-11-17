/** Đơn từ — khớp các DTO trong FW.Application/Features/Requests. Ngày / giờ dạng ISO giờ Việt Nam (không múi giờ). */

export type RequestCategory = 'leave' | 'shift_swap' | 'overtime' | 'explanation';
export type RequestStatus = 'pending_peer' | 'pending' | 'approved' | 'rejected' | 'peer_declined' | 'cancelled';

export interface LeaveType {
  id: number;
  name: string;
  category: RequestCategory;
  description: string | null;
  isPaid: boolean;
  deductsBalance: boolean;
  requiresAttachment: boolean;
  isActive: boolean;
}

export interface RequestPerson {
  id: number;
  employeeCode: string;
  fullName: string;
  avatarUrl: string | null;
}

export interface RequestSchedule {
  id: number;
  workDate: string;
  shiftName: string | null;
  startTime: string | null;
  endTime: string | null;
  status: string | null;
}

export interface RequestAttendance {
  id: number;
  workDate: string;
  shiftName: string | null;
  checkinTime: string | null;
  checkoutTime: string | null;
  lateMinutes: number;
  earlyMinutes: number;
  status: string | null;
}

export interface LeaveRequest {
  id: number;
  employee: RequestPerson;
  branchId: number | null;
  branchName: string | null;
  leaveTypeId: number;
  leaveTypeName: string;
  category: RequestCategory;
  isPaid: boolean;
  deductsBalance: boolean;
  startDate: string;
  endDate: string;
  durationDays: number;
  startAt: string | null;
  endAt: string | null;
  durationHours: number | null;
  targetUser: RequestPerson | null;
  sourceSchedule: RequestSchedule | null;
  targetSchedule: RequestSchedule | null;
  peerRespondedAt: string | null;
  peerNote: string | null;
  attendanceLog: RequestAttendance | null;
  reason: string;
  attachmentUrl: string | null;
  status: RequestStatus;
  approvedBy: number | null;
  approverName: string | null;
  approvedAt: string | null;
  approverNote: string | null;
  createdAt: string;
  canCancel: boolean;
  canRespond: boolean;
  canReview: boolean;
}

export interface LeaveBalance {
  year: number;
  totalDays: number;
  usedDays: number;
  remainingDays: number;
  pendingDays: number;
  availableDays: number;
  isDefault: boolean;
}

export interface SwapShiftOption {
  scheduleId: number;
  userId: number;
  employeeCode: string;
  fullName: string;
  workDate: string;
  shiftName: string;
  startTime: string;
  endTime: string;
}

export interface SwapOptions {
  source: RequestSchedule;
  shifts: SwapShiftOption[];
  colleagues: RequestPerson[];
}

/** Dữ liệu gửi khi tạo đơn (trường của nhóm khác bỏ trống). */
export interface CreateRequestInput {
  leaveTypeId: number;
  reason: string;
  startDate?: string;
  endDate?: string;
  startAt?: string;
  endAt?: string;
  sourceScheduleId?: number;
  targetScheduleId?: number;
  targetUserId?: number;
  attendanceLogId?: number;
}
