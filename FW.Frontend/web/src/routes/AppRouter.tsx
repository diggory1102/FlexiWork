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
                  comingSoon('/requests', 'Yêu cầu chờ duyệt', 'Cấp bậc 4 — Đơn từ & Phê duyệt'),
                  comingSoon('/shifts', 'Khung ca làm việc', 'Cấp bậc 2 — Khung ca & Xếp lịch'),
                  comingSoon('/schedules', 'Xếp lịch', 'Cấp bậc 2 — Khung ca & Xếp lịch'),
                  comingSoon('/attendance', 'Nhật ký chấm công', 'Cấp bậc 3 — Chấm công 3 lớp'),
                  comingSoon('/timesheets', 'Bảng công', 'Cấp bậc 5 — Tổng hợp bảng công'),
                  comingSoon('/tasks', 'Công việc theo ca', 'Cấp bậc 4 — Công việc theo ca'),
                  comingSoon('/chat', 'Truyền thông nội bộ', 'Cấp bậc 5 — Realtime'),
                  comingSoon('/reports', 'Báo cáo', 'Cấp bậc 5 — Báo cáo'),
                  comingSoon('/settings', 'Cài đặt', 'Cấp bậc 2 & 4 — Quy tắc vi phạm, loại đơn'),
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
