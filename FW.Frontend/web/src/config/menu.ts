import type { LucideIcon } from 'lucide-react';
import {
  BarChart3,
  Building2,
  CalendarDays,
  ClipboardCheck,
  Fingerprint,
  Inbox,
  LayoutDashboard,
  MessagesSquare,
  Settings,
  Users,
} from 'lucide-react';
import { RoleGroups, type Role } from '@/types/auth';

export interface MenuItem {
  label: string;
  to?: string;
  icon: LucideIcon;
  roles: Role[];
  children?: { label: string; to: string; roles?: Role[] }[];
}

/**
 * Menu Web Admin theo 6 phân hệ của đề tài (tham khảo bố cục HRM iPOS, đã bỏ Tuyển dụng / Bảng giá;
 * Hợp đồng gộp vào hồ sơ nhân viên; Lương → Bảng công).
 */
export const menu: MenuItem[] = [
  { label: 'Tổng quan', to: '/', icon: LayoutDashboard, roles: RoleGroups.Managers },
  { label: 'Yêu cầu chờ duyệt', to: '/requests', icon: Inbox, roles: RoleGroups.Managers },
  {
    label: 'Nhân sự',
    icon: Users,
    roles: RoleGroups.Managers,
    children: [
      { label: 'Nhân viên', to: '/employees' },
      { label: 'Phòng ban', to: '/departments' },
      { label: 'Chức vụ & đơn giá', to: '/positions' },
    ],
  },
  { label: 'Chi nhánh & Wi-Fi', to: '/branches', icon: Building2, roles: RoleGroups.Managers },
  {
    label: 'Lịch làm việc',
    icon: CalendarDays,
    roles: RoleGroups.Managers,
    children: [
      { label: 'Khung ca làm việc', to: '/shifts' },
      { label: 'Xếp lịch', to: '/schedules' },
    ],
  },
  {
    label: 'Chấm công',
    icon: Fingerprint,
    roles: RoleGroups.Managers,
    children: [
      { label: 'Nhật ký chấm công', to: '/attendance' },
      { label: 'Bảng công', to: '/timesheets' },
    ],
  },
  { label: 'Công việc', to: '/tasks', icon: ClipboardCheck, roles: RoleGroups.Managers },
  { label: 'Truyền thông nội bộ', to: '/chat', icon: MessagesSquare, roles: RoleGroups.Managers },
  { label: 'Báo cáo', to: '/reports', icon: BarChart3, roles: RoleGroups.Managers },
  { label: 'Cài đặt', to: '/settings', icon: Settings, roles: RoleGroups.AdminOrHr },
];
