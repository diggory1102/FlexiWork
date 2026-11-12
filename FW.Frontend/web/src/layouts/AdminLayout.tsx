import type { ReactNode } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { AppShell } from '@astryxdesign/core/AppShell';
import { DropdownMenu, DropdownMenuDivider, DropdownMenuItem } from '@astryxdesign/core/DropdownMenu';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Icon } from '@astryxdesign/core/Icon';
import { SideNav, SideNavItem, SideNavSection } from '@astryxdesign/core/SideNav';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { TopNav, TopNavHeading } from '@astryxdesign/core/TopNav';
import { Bell, ChevronDown, KeyRound, LogOut, MessageSquare, Moon, Sun, UserRound } from 'lucide-react';
import { menu, type MenuItem } from '@/config/menu';
import { useAuthStore } from '@/stores/authStore';
import { useUiStore } from '@/stores/uiStore';
import { Avatar } from '@/components/ui/Layout';
import { LogoMark } from '@/components/common/Logo';
import { roleLabels } from '@/lib/format';

/** Khung giao diện Web Admin (Astryx AppShell): thanh trên + menu trái thu gọn được + vùng nội dung. */
export function AdminLayout({ children }: { children?: ReactNode }) {
  return (
    <AppShell topNav={<Header />} sideNav={<Sidebar />} contentPadding={6}>
      {children ?? <Outlet />}
    </AppShell>
  );
}

function Header() {
  const colorMode = useUiStore((s) => s.colorMode);
  const toggleColorMode = useUiStore((s) => s.toggleColorMode);

  return (
    <TopNav
      label="Thanh điều hướng chính"
      heading={<TopNavHeading logo={<LogoMark size={28} />} heading="FlexiWork" subheading="Quản lý ca & chấm công" headingHref="/" />}
      endContent={
        <HStack gap={1} vAlign="center">
          <IconButton
            variant="ghost"
            label={colorMode === 'light' ? 'Chuyển sang giao diện tối' : 'Chuyển sang giao diện sáng'}
            tooltip={colorMode === 'light' ? 'Giao diện tối' : 'Giao diện sáng'}
            icon={<Icon icon={colorMode === 'light' ? Moon : Sun} />}
            onClick={toggleColorMode}
          />
          <IconButton variant="ghost" label="Tin nhắn" tooltip="Tin nhắn (Cấp 5)" icon={<Icon icon={MessageSquare} />} />
          <IconButton variant="ghost" label="Thông báo" tooltip="Thông báo (Cấp 5)" icon={<Icon icon={Bell} />} />
          <UserMenu />
        </HStack>
      }
    />
  );
}

function UserMenu() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();
  if (!user) return null;

  return (
    <DropdownMenu
      placement="below"
      alignment="end"
      menuWidth={260}
      renderTrigger={(props) => (
        <button
          {...props}
          type="button"
          aria-label="Tài khoản"
          className="ml-1 flex items-center gap-2 rounded-lg px-1.5 py-1 hover:bg-muted"
        >
          <Avatar name={user.fullName} url={user.avatarUrl} size={32} />
          <span className="hidden max-w-40 truncate text-sm font-medium text-primary md:block">{user.fullName}</span>
          <ChevronDown className="h-4 w-4 text-secondary" />
        </button>
      )}
      header={
        <VStack gap={0.5} padding={3}>
          <Text weight="semibold" maxLines={1}>
            {user.fullName}
          </Text>
          <Text type="supporting" maxLines={1}>
            {user.email}
          </Text>
          <Text type="supporting" color="accent">
            {user.roles.map((r) => roleLabels[r]).join(', ')}
          </Text>
        </VStack>
      }
    >
      <DropdownMenuItem label="Hồ sơ cá nhân" icon={UserRound} onClick={() => navigate('/profile')} />
      <DropdownMenuItem label="Đổi mật khẩu" icon={KeyRound} onClick={() => navigate('/change-password')} />
      <DropdownMenuDivider />
      <DropdownMenuItem
        label="Đăng xuất"
        icon={LogOut}
        variant="destructive"
        onClick={() => {
          logout();
          navigate('/login', { replace: true });
        }}
      />
    </DropdownMenu>
  );
}

function Sidebar() {
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  const collapsed = useUiStore((s) => s.sideNavCollapsed);
  const setCollapsed = useUiStore((s) => s.setSideNavCollapsed);
  const { pathname } = useLocation();
  const items = menu.filter((item) => hasAnyRole(item.roles));

  return (
    <SideNav
      aria-label="Menu chức năng"
      collapsible={{ isCollapsed: collapsed, onCollapsedChange: setCollapsed, buttonLabel: 'Thu gọn menu' }}
    >
      <SideNavSection title="Chức năng" isHeaderHidden>
        {items.map((item) => (
          <MenuEntry key={item.label} item={item} pathname={pathname} />
        ))}
      </SideNavSection>
    </SideNav>
  );
}

const isActive = (to: string, pathname: string) => (to === '/' ? pathname === '/' : pathname.startsWith(to));

function MenuEntry({ item, pathname }: { item: MenuItem; pathname: string }) {
  if (!item.children) {
    return <SideNavItem label={item.label} icon={item.icon} href={item.to} isSelected={isActive(item.to!, pathname)} />;
  }
  const childActive = item.children.some((c) => isActive(c.to, pathname));
  return (
    <SideNavItem label={item.label} icon={item.icon} collapsible={{ defaultIsCollapsed: !childActive }}>
      {item.children.map((child) => (
        <SideNavItem key={child.to} label={child.label} href={child.to} isSelected={isActive(child.to, pathname)} />
      ))}
    </SideNavItem>
  );
}
