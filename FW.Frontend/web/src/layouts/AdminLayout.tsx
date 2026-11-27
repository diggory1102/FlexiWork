import type { ReactNode } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { AppShell } from '@astryxdesign/core/AppShell';
import { DropdownMenu, DropdownMenuDivider, DropdownMenuItem } from '@astryxdesign/core/DropdownMenu';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Icon } from '@astryxdesign/core/Icon';
import { SegmentedControl, SegmentedControlItem } from '@astryxdesign/core/SegmentedControl';
import { SideNav, SideNavHeading, SideNavItem, SideNavSection } from '@astryxdesign/core/SideNav';
import { Layout, LayoutContent, LayoutHeader } from '@astryxdesign/core/Layout';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { ChevronDown, KeyRound, LogOut, Moon, Sun, UserRound } from 'lucide-react';
import { menu, type MenuItem } from '@/config/menu';
import { useAuthStore } from '@/stores/authStore';
import { useUiStore } from '@/stores/uiStore';
import { Avatar } from '@/components/ui/Layout';
import { BrandIcon, SLOGAN } from '@/layouts/Brand';
import { ChatButton, NotificationBell } from '@/components/common/HeaderBadges';
import { RealtimeBridge } from '@/app/RealtimeBridge';
import { roleLabels } from '@/lib/format';

/** Khung giao diện Web Admin (Astryx AppShell): menu trái thu gọn được, thanh trên và vùng nội dung. */
export function AdminLayout({ children }: { children?: ReactNode }) {
  return (
    <AppShell variant="section" sideNav={<Sidebar />} contentPadding={0}>
      <RealtimeBridge />
      <Layout
        height="fill"
        padding={0}
        header={
          <LayoutHeader hasDivider padding={0}>
            <HStack hAlign="between" vAlign="center" gap={3} paddingInline={6} className="h-16 bg-[var(--fw-chrome)]">
              <div className="min-w-0 flex-1">
                <Text color="secondary" weight="medium" className="hidden md:block">
                  {SLOGAN}
                </Text>
              </div>
              <HStack gap={2} vAlign="center">
                <ChatButton />
                <NotificationBell />
                <UserMenu />
              </HStack>
            </HStack>
          </LayoutHeader>
        }
        content={<LayoutContent padding={4}>{children ?? <Outlet />}</LayoutContent>}
      />
    </AppShell>
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
  const colorMode = useUiStore((s) => s.colorMode);
  const toggleColorMode = useUiStore((s) => s.toggleColorMode);
  const collapsed = useUiStore((s) => s.sideNavCollapsed);
  const setCollapsed = useUiStore((s) => s.setSideNavCollapsed);
  const { pathname } = useLocation();
  const items = menu.filter((item) => hasAnyRole(item.roles));
  // Mục cấu hình hệ thống ghim ở đáy menu, tách khỏi nghiệp vụ hằng ngày
  const pinned = items.filter((item) => item.label === 'Cài đặt');

  return (
    <SideNav
      aria-label="Menu chức năng"
      header={<SideNavHeading heading="FlexiWork" icon={<BrandIcon size={44} />} headingHref="/" />}
      collapsible={{ isCollapsed: collapsed, onCollapsedChange: setCollapsed, buttonLabel: 'Thu gọn menu' }}
      footer={
        <VStack gap={2}>
          {pinned.length > 0 && (
            <SideNavSection title="Hệ thống" isHeaderHidden>
              {pinned.map((item) => (
                <MenuEntry key={item.label} item={item} pathname={pathname} />
              ))}
            </SideNavSection>
          )}
          {collapsed ? (
            <IconButton variant="ghost" label="Đổi giao diện sáng / tối" icon={<Icon icon={colorMode === 'light' ? Moon : Sun} />} onClick={toggleColorMode} />
          ) : (
            <SegmentedControl label="Giao diện" size="sm" layout="fill" value={colorMode} onChange={(v) => v !== colorMode && toggleColorMode()}>
              <SegmentedControlItem value="light" label="Sáng" icon={<Icon icon={Sun} size="sm" />} />
              <SegmentedControlItem value="dark" label="Tối" icon={<Icon icon={Moon} size="sm" />} />
            </SegmentedControl>
          )}
        </VStack>
      }
    >
      <SideNavSection title="Chức năng" isHeaderHidden>
        {items
          .filter((item) => !pinned.includes(item))
          .map((item) => (
            <MenuEntry key={item.label} item={item} pathname={pathname} />
          ))}
      </SideNavSection>
    </SideNav>
  );
}

/** Khớp đúng đường dẫn hoặc trang con ("/payroll" không được khớp "/payroll-config"). */
const isActive = (to: string, pathname: string) => (to === '/' ? pathname === '/' : pathname === to || pathname.startsWith(`${to}/`));

function MenuEntry({ item, pathname }: { item: MenuItem; pathname: string }) {
  const hasAnyRole = useAuthStore((s) => s.hasAnyRole);
  if (!item.children) {
    return <SideNavItem label={item.label} icon={item.icon} href={item.to} isSelected={isActive(item.to!, pathname)} />;
  }
  const children = item.children.filter((c) => !c.roles || hasAnyRole(c.roles));
  const childActive = children.some((c) => isActive(c.to, pathname));
  return (
    <SideNavItem label={item.label} icon={item.icon} collapsible={{ defaultIsCollapsed: !childActive }}>
      {children.map((child) => (
        <SideNavItem key={child.to} label={child.label} href={child.to} isSelected={isActive(child.to, pathname)} />
      ))}
    </SideNavItem>
  );
}
