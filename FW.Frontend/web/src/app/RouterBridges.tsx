import { forwardRef, useEffect, type AnchorHTMLAttributes } from 'react';
import { Link as RouterLink, Outlet } from 'react-router-dom';
import { LinkProvider } from '@astryxdesign/core/Link';
import { useToast } from '@astryxdesign/core/Toast';
import { useToastStore } from '@/stores/toastStore';

/**
 * Link của Astryx (SideNavItem, Button có href…) mặc định là thẻ <a> → tải lại trang.
 * Adapter này chuyển sang Link của react-router để điều hướng phía client.
 */
const AppLink = forwardRef<HTMLAnchorElement, AnchorHTMLAttributes<HTMLAnchorElement> & { href?: string }>(function AppLink(
  { href = '/', ...rest },
  ref,
) {
  const external = /^(https?:|mailto:|tel:)/.test(href) || rest.target === '_blank';
  return external ? <a ref={ref} href={href} {...rest} /> : <RouterLink ref={ref} to={href} {...rest} />;
});

/** Chuyển thông báo từ toastStore (gọi được ở bất kỳ đâu) sang hệ thống Toast của Astryx. */
function ToastBridge() {
  const showToast = useToast();
  const pendingCount = useToastStore((s) => s.pending.length);

  useEffect(() => {
    for (const t of useToastStore.getState().take()) {
      showToast({
        body: t.message,
        // Astryx chỉ có 2 loại: info (tự ẩn) và error (giữ tới khi đóng)
        type: t.type === 'error' ? 'error' : 'info',
        isAutoHide: true,
        autoHideDuration: t.type === 'error' ? 7000 : 4000,
      });
    }
  }, [pendingCount, showToast]);

  return null;
}

/** Phần tử gốc của router: cung cấp Link và Toast cho toàn bộ trang. */
export function RouterRoot() {
  return (
    <LinkProvider component={AppLink}>
      <Outlet />
      <ToastBridge />
    </LinkProvider>
  );
}
