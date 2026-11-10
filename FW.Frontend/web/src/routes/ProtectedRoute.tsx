import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/authStore';
import type { Role } from '@/types/auth';

interface ProtectedRouteProps {
  /** Nếu truyền vào, chỉ những vai trò này mới được truy cập (Role-Aware Rendering). */
  allowedRoles?: Role[];
}

/**
 * Chặn truy cập khi chưa đăng nhập / phiên hết hạn; ép đổi mật khẩu tạm trước khi dùng hệ thống;
 * giới hạn trang theo vai trò.
 */
export function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const location = useLocation();
  const user = useAuthStore((s) => s.user);
  const accessToken = useAuthStore((s) => s.accessToken);
  const isSessionValid = useAuthStore((s) => s.isSessionValid);
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);

  if (!accessToken || !isSessionValid()) {
    return <Navigate to="/login" replace />;
  }
  if (user?.mustChangePassword && location.pathname !== '/change-password') {
    return <Navigate to="/change-password" replace />;
  }
  if (allowedRoles && !hasAnyRole(allowedRoles)) {
    return <Navigate to="/" replace />;
  }
  return <Outlet />;
}
