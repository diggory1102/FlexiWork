export type ScheduleStatus = 'scheduled' | 'in_progress' | 'completed' | 'absent' | 'cancelled' | 'on_leave';

/** Khớp ScheduleDto ở backend. Giờ dạng "HH:mm", workDate dạng yyyy-MM-dd. */
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

/** Khớp FaceProfileStatusDto ở backend (không chứa vector đặc trưng). */
export interface FaceStatus {
  registered: boolean;
  sampleImageUrl: string | null;
  modelName: string | null;
  registeredAt: string | null;
  updatedAt: string | null;
}
