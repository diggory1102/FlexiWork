import type { PagedQuery } from './api';

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

export interface LeaveTypeRequest {
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

export interface LeaveRequestFilter extends PagedQuery {
  status?: RequestStatus | '';
  category?: RequestCategory | '';
  branchId?: number | '';
  userId?: number;
  fromDate?: string;
  toDate?: string;
}

/** Quỹ phép năm của một nhân viên. isDefault = chưa khởi tạo, đang hiện số ngày mặc định. */
export interface LeaveBalanceRow {
  userId: number;
  employeeCode: string;
  fullName: string;
  avatarUrl: string | null;
  branchId: number | null;
  branchName: string | null;
  year: number;
  totalDays: number;
  usedDays: number;
  remainingDays: number;
  pendingDays: number;
  availableDays: number;
  isDefault: boolean;
}

export interface LeaveBalanceFilter extends PagedQuery {
  year?: number;
  branchId?: number | '';
}

export interface LeaveBalanceAdjustment {
  id: number;
  year: number;
  deltaDays: number;
  reason: string;
  createdBy: number | null;
  createdByName: string | null;
  createdAt: string;
}

export interface AdjustBalanceRequest {
  userId: number;
  year: number;
  deltaDays: number;
  reason: string;
}
