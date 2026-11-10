import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Bell, ChevronDown, KeyRound, LogOut, Menu as MenuIcon, MessageSquare, UserRound } from 'lucide-react';
import { menu, type MenuItem } from '@/config/menu';
import { useAuthStore } from '@/stores/authStore';
import { Avatar } from '@/components/ui/Layout';
import { roleLabels } from '@/lib/format';
import { Logo } from '@/components/common/Logo';

/** Khung giao diện Web Admin: header tối + sidebar trái + vùng nội dung (Outlet hoặc children). */
export function AdminLayout({ children }: { children?: ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  const location = useLocation();
  const title = findTitle(location.pathname);

  return (
    <div className="flex h-full flex-col">
      <Header collapsed={collapsed} onToggle={() => setCollapsed((c) => !c)} title={title} />
      <div className="flex min-h-0 flex-1">
        <Sidebar collapsed={collapsed} />
        <main className="min-w-0 flex-1 overflow-y-auto p-4 md:p-6">
          {children ?? <Outlet />}
        </main>
      </div>
    </div>
  );
}

function findTitle(pathname: string): string {
  for (const item of menu) {
    if (item.to && (item.to === pathname || (item.to !== '/' && pathname.startsWith(item.to)))) return item.label;
    const child = item.children?.find((c) => pathname.startsWith(c.to));
    if (child) return child.label;
  }
  if (pathname.startsWith('/profile')) return 'Hồ sơ cá nhân';
  if (pathname.startsWith('/change-password')) return 'Đổi mật khẩu';
  return 'FlexiWork';
}

function Header({ collapsed, onToggle, title }: { collapsed: boolean; onToggle: () => void; title: string }) {
  return (
    <header className="flex h-14 shrink-0 items-center bg-header text-white">
      <div className={`flex h-full items-center px-4 transition-all ${collapsed ? 'w-16 justify-center' : 'w-60'}`}>
        <Link to="/">
          <Logo compact={collapsed} />
        </Link>
      </div>
      <button type="button" onClick={onToggle} className="rounded-md p-2 text-white/80 hover:bg-white/10" aria-label="Thu gọn menu">
        <MenuIcon className="h-5 w-5" />
      </button>
      <h1 className="ml-2 truncate text-base font-semibold">{title}</h1>

      <div className="ml-auto flex items-center gap-1 pr-4">
        <button type="button" className="rounded-md p-2 text-white/80 hover:bg-white/10" title="Tin nhắn (Cấp 5)" aria-label="Tin nhắn">
          <MessageSquare className="h-5 w-5" />
        </button>
        <button type="button" className="rounded-md p-2 text-white/80 hover:bg-white/10" title="Thông báo (Cấp 5)" aria-label="Thông báo">
          <Bell className="h-5 w-5" />
        </button>
        <UserMenu />
      </div>
    </header>
  );
}

function UserMenu() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const close = (e: MouseEvent) => ref.current && !ref.current.contains(e.target as Node) && setOpen(false);
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  if (!user) return null;
  const go = (path: string) => {
    setOpen(false);
    navigate(path);
  };

  return (
    <div className="relative ml-2" ref={ref}>
      <button type="button" onClick={() => setOpen((o) => !o)} className="flex items-center gap-2 rounded-md p-1 hover:bg-white/10">
        <Avatar name={user.fullName} url={user.avatarUrl} size={32} />
        <ChevronDown className="h-4 w-4 text-white/70" />
      </button>
      {open && (
        <div className="absolute right-0 top-11 z-40 w-64 overflow-hidden rounded-lg border border-border bg-surface text-foreground shadow-lg">
          <div className="border-b border-border px-4 py-3">
            <p className="truncate font-semibold">{user.fullName}</p>
            <p className="truncate text-xs text-muted">{user.email}</p>
            <p className="mt-1 text-xs text-primary">{user.roles.map((r) => roleLabels[r]).join(', ')}</p>
          </div>
          <button type="button" onClick={() => go('/profile')} className="flex w-full items-center gap-2 px-4 py-2.5 hover:bg-background">
            <UserRound className="h-4 w-4 text-muted" /> Hồ sơ cá nhân
          </button>
          <button type="button" onClick={() => go('/change-password')} className="flex w-full items-center gap-2 px-4 py-2.5 hover:bg-background">
            <KeyRound className="h-4 w-4 text-muted" /> Đổi mật khẩu
          </button>
          <button
            type="button"
            onClick={() => {
              logout();
              navigate('/login', { replace: true });
            }}
            className="flex w-full items-center gap-2 border-t border-border px-4 py-2.5 text-danger hover:bg-danger/5"
          >
            <LogOut className="h-4 w-4" /> Đăng xuất
          </button>
        </div>
      )}
    </div>
  );
}

function Sidebar({ collapsed }: { collapsed: boolean }) {
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  const items = menu.filter((item) => hasAnyRole(item.roles));

  return (
    <aside className={`shrink-0 overflow-y-auto border-r border-border bg-surface py-3 transition-all ${collapsed ? 'w-16' : 'w-60'}`}>
      <nav className="flex flex-col gap-0.5 px-2">
        {items.map((item) => (
          <SidebarItem key={item.label} item={item} collapsed={collapsed} />
        ))}
      </nav>
    </aside>
  );
}

const linkClass = (active: boolean, collapsed: boolean) =>
  `flex items-center gap-3 rounded-md px-3 py-2 transition ${collapsed ? 'justify-center' : ''} ${
    active ? 'bg-primary/10 font-medium text-primary' : 'text-foreground/80 hover:bg-background'
  }`;

function SidebarItem({ item, collapsed }: { item: MenuItem; collapsed: boolean }) {
  const location = useLocation();
  const childActive = item.children?.some((c) => location.pathname.startsWith(c.to)) ?? false;
  const [open, setOpen] = useState(childActive);
  const Icon = item.icon;

  useEffect(() => {
    if (childActive) setOpen(true);
  }, [childActive]);

  if (!item.children) {
    return (
      <NavLink to={item.to!} end={item.to === '/'} title={collapsed ? item.label : undefined} className={({ isActive }) => linkClass(isActive, collapsed)}>
        <Icon className="h-[18px] w-[18px] shrink-0" />
        {!collapsed && <span className="truncate">{item.label}</span>}
      </NavLink>
    );
  }

  return (
    <div>
      <button
        type="button"
        title={collapsed ? item.label : undefined}
        onClick={() => setOpen((o) => !o)}
        className={`w-full ${linkClass(childActive && collapsed, collapsed)}`}
      >
        <Icon className="h-[18px] w-[18px] shrink-0" />
        {!collapsed && (
          <>
            <span className="flex-1 truncate text-left">{item.label}</span>
            <ChevronDown className={`h-4 w-4 text-muted transition ${open ? 'rotate-180' : ''}`} />
          </>
        )}
      </button>
      {open && !collapsed && (
        <div className="ml-[22px] mt-0.5 flex flex-col gap-0.5 border-l border-border pl-3">
          {item.children.map((child) => (
            <NavLink key={child.to} to={child.to} className={({ isActive }) => `rounded-md px-3 py-1.5 ${isActive ? 'font-medium text-primary' : 'text-foreground/70 hover:bg-background'}`}>
              {child.label}
            </NavLink>
          ))}
        </div>
      )}
    </div>
  );
}
