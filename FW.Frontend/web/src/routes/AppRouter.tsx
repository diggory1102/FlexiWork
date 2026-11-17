import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { RouterRoot } from '@/app/RouterBridges';
import { AdminLayout } from '@/layouts/AdminLayout';
import { ProtectedRoute } from '@/routes/ProtectedRoute';
import { LoginPage } from '@/modules/auth/pages/LoginPage';
import { ForgotPasswordPage } from '@/modules/auth/pages/ForgotPasswordPage';
import { ChangePasswordPage } from '@/modules/auth/pages/ChangePasswordPage';
import { DashboardPage } from '@/modules/dashboard/pages/DashboardPage';
import { BranchesPage } from '@/modules/branches/pages/BranchesPage';
import { DepartmentsPage } from '@/modules/organization/pages/DepartmentsPage';
import { PositionsPage } from '@/modules/organization/pages/PositionsPage';
import { EmployeesPage } from '@/modules/employees/pages/EmployeesPage';
import { ProfilePage } from '@/modules/profile/pages/ProfilePage';
import { ShiftsPage } from '@/modules/shifts/pages/ShiftsPage';
import { SchedulesPage } from '@/modules/schedules/pages/SchedulesPage';
import { SettingsPage } from '@/modules/settings/pages/SettingsPage';
import { AttendanceLogsPage } from '@/modules/attendance/pages/AttendanceLogsPage';
import { RequestsPage } from '@/modules/requests/pages/RequestsPage';
import { LeaveBalancesPage } from '@/modules/requests/pages/LeaveBalancesPage';
import { LeaveTypesPage } from '@/modules/requests/pages/LeaveTypesPage';
import { TasksPage } from '@/modules/tasks/pages/TasksPage';
import { ReportsPage } from '@/modules/reports/pages/ReportsPage';
import { ComingSoonPage } from '@/components/common/ComingSoonPage';
import { NotFoundPage } from '@/components/common/NotFoundPage';
import { useAuthStore } from '@/stores/authStore';
import { RoleGroups } from '@/types/auth';

/** Trang đổi mật khẩu bắt buộc hiển thị toàn màn hình; đổi chủ động thì nằm trong khung Admin. */
function ChangePasswordRoute() {
  const forced = useAuthStore((s) => s.user?.mustChangePassword);
  return forced ? (
    <ChangePasswordPage />
  ) : (
    <AdminLayout>
      <ChangePasswordPage />
    </AdminLayout>
  );
}

const comingSoon = (path: string, title: string, stage: string) => ({ path, element: <ComingSoonPage title={title} stage={stage} /> });

const router = createBrowserRouter([
  {
    element: <RouterRoot />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/forgot-password', element: <ForgotPasswordPage /> },
      {
        element: <ProtectedRoute />,
        children: [
          { path: '/change-password', element: <ChangePasswordRoute /> },
          {
            element: <AdminLayout />,
            children: [
              { index: true, element: <DashboardPage /> },
              { path: '/profile', element: <ProfilePage /> },
              {
                element: <ProtectedRoute allowedRoles={RoleGroups.Managers} />,
                children: [
                  { path: '/employees', element: <EmployeesPage /> },
                  { path: '/departments', element: <DepartmentsPage /> },
                  { path: '/positions', element: <PositionsPage /> },
                  { path: '/branches', element: <BranchesPage /> },
                  { path: '/requests', element: <RequestsPage /> },
                  { path: '/leave-balances', element: <LeaveBalancesPage /> },
                  { path: '/shifts', element: <ShiftsPage /> },
                  { path: '/schedules', element: <SchedulesPage /> },
                  { path: '/attendance', element: <AttendanceLogsPage /> },
                  comingSoon('/timesheets', 'Bảng công', 'Cấp bậc 5 — Tổng hợp bảng công'),
                  { path: '/tasks', element: <TasksPage /> },
                  comingSoon('/chat', 'Truyền thông nội bộ', 'Cấp bậc 5 — Realtime'),
                  { path: '/reports', element: <ReportsPage /> },
                ],
              },
              {
                element: <ProtectedRoute allowedRoles={RoleGroups.AdminOrHr} />,
                children: [
                  { path: '/settings', element: <SettingsPage /> },
                  { path: '/leave-types', element: <LeaveTypesPage /> },
                ],
              },
            ],
          },
        ],
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);

export function AppRouter() {
  return <RouterProvider router={router} />;
}
