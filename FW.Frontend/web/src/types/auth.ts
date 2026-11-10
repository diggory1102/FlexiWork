/** Vai trò hệ thống — khớp với bảng roles (role_name) ở CSDL. */
export const Roles = {
  Admin: 'ROLE_ADMIN',
  Hr: 'ROLE_HR',
  Manager: 'ROLE_MANAGER',
  Employee: 'ROLE_EMPLOYEE',
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];

/** Nhóm vai trò dùng cho phân quyền giao diện (khớp RoleNames ở backend). */
export const RoleGroups = {
  AdminOrHr: [Roles.Admin, Roles.Hr] as Role[],
  Managers: [Roles.Admin, Roles.Hr, Roles.Manager] as Role[],
};

export interface AuthUser {
  id: number;
  employeeCode: string;
  fullName: string;
  email: string;
  avatarUrl?: string | null;
  branchId?: number | null;
  roles: Role[];
  /** Đang dùng mật khẩu tạm do hệ thống cấp — phải chuyển tới màn hình đổi mật khẩu. */
  mustChangePassword: boolean;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthUser;
}
