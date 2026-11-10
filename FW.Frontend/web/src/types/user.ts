import type { PagedQuery } from './api';
import type { Role } from './auth';

export type UserStatus = 'active' | 'suspended' | 'left';
export type EmploymentType = 'full_time' | 'part_time';

/** Khớp UserDto ở backend. */
export interface User {
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

export interface UserFilter extends PagedQuery {
  branchId?: number | '';
  departmentId?: number | '';
  positionId?: number | '';
  status?: UserStatus | '';
  roleName?: Role | '';
}

export interface UserProfileRequest {
  employeeCode: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  branchId?: number | null;
  departmentId?: number | null;
  positionId?: number | null;
  employmentType: EmploymentType;
  hireDate?: string | null;
}

export interface CreateUserRequest extends UserProfileRequest {
  roles: Role[];
}

export interface CreateUserResult {
  user: User;
  invitationSent: boolean;
}

export interface RoleOption {
  id: number;
  roleName: Role;
  description: string | null;
  assignable: boolean;
}
