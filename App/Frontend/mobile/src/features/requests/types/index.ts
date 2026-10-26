export type RequestType = 'leave' | 'shift_swap' | 'overtime' | 'attendance_explanation';

export interface EmployeeRequest {
  id: number;
  type: RequestType;
  title: string;
  startDate: string;
  endDate: string;
  reason: string;
  status: 'pending' | 'approved' | 'rejected';
  createdAt: string;
}
