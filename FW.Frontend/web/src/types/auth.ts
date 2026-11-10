/** Vai trò hệ thống — khớp với bảng roles (role_name) ở CSDL. */
export const Roles = {
  Admin: 'ROLE_ADMIN',
  Hr: 'ROLE_HR',
  Manager: 'ROLE_MANAGER',
  Employee: 'ROLE_EMPLOYEE',
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];

export interface AuthUser {
  id: number;
  employeeCode: string;
  fullName: string;
  email: string;
  avatarUrl?: string | null;
  branchId?: number | null;
  roles: Role[];
}
