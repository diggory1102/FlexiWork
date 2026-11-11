import type { Role } from './auth';

export type UserStatus = 'active' | 'suspended' | 'left';
export type EmploymentType = 'full_time' | 'part_time';

/** Khớp UserDto ở backend (GET /api/profile). */
export interface UserProfile {
  id: number;
  employeeCode: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  avatarUrl: string | null;
  branchId: number | null;
  branchName: string | null;
  departmentId: number | null;
  departmentName: string | null;
  positionId: number | null;
  positionTitle: string | null;
  employmentType: EmploymentType;
  hireDate: string | null;
  status: UserStatus;
  mustChangePassword: boolean;
  roles: Role[];
  createdAt: string;
  updatedAt: string | null;
}
