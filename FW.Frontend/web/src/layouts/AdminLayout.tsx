import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/stores/authStore';

const menuItems = [
  { to: '/', label: 'Tổng quan' },
  // Bổ sung khi triển khai các module: Chi nhánh, Nhân sự, Ca làm việc, Xếp lịch, Chấm công, Đơn từ, Công việc, Bảng công
];

/** Khung giao diện Web Admin: sidebar + header + vùng nội dung. */
export function AdminLayout() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside className="hidden w-60 shrink-0 border-r border-border bg-surface md:block">
        <div className="px-5 py-4 text-lg font-semibold text-primary">FlexiWork</div>
        <nav className="flex flex-col gap-1 px-3">
          {menuItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end
              className={({ isActive }) =>
                `rounded-md px-3 py-2 text-sm ${isActive ? 'bg-primary/10 font-medium text-primary' : 'text-muted hover:bg-background'}`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 items-center justify-between border-b border-border bg-surface px-4">
          <span className="text-sm text-muted">Xin chào, {user?.fullName ?? 'người dùng'}</span>
          <button onClick={handleLogout} className="text-sm text-danger hover:underline">
            Đăng xuất
          </button>
        </header>
        <main className="flex-1 p-4 md:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
