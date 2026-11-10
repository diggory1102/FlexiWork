import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '@/stores/authStore';
import type { Role } from '@/types/auth';

interface ProtectedRouteProps {
  /** Nếu truyền vào, chỉ những vai trò này mới được truy cập (Role-Aware Rendering). */
  allowedRoles?: Role[];
}

export function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const accessToken = useAuthStore((s) => s.accessToken);
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);

  if (!accessToken) {
    return <Navigate to="/login" replace />;
  }
  if (allowedRoles && !hasAnyRole(allowedRoles)) {
    return <Navigate to="/" replace />;
  }
  return <Outlet />;
}
