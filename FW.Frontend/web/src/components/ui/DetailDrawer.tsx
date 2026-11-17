import type { ReactNode } from 'react';
import { Drawer, DrawerHeader } from '@astryxdesign/core/Drawer';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';

/**
 * Vỏ ngăn chi tiết bên phải: tiêu đề cố định, nội dung cuộn, chân (nút thao tác) cố định.
 * Chỉ lo khung — toàn bộ nội dung do từng màn hình tự dựng.
 */
export function DetailDrawer({
  open,
  onClose,
  label,
  title,
  subtitle,
  width = 640,
  purpose = 'form',
  footer,
  children,
}: {
  open: boolean;
  onClose: () => void;
  /** Nhãn truy cập (đọc bằng trình đọc màn hình). */
  label: string;
  title: string;
  subtitle?: string;
  width?: number;
  /** form: chỉ đóng bằng nút / Esc (tránh mất dữ liệu đang nhập khi bấm nhầm ra ngoài). */
  purpose?: 'info' | 'form';
  footer?: ReactNode;
  children: ReactNode;
}) {
  return (
    <Drawer isOpen={open} onOpenChange={(isOpen) => !isOpen && onClose()} label={label} width={width} purpose={purpose}>
      <Layout
        header={<DrawerHeader title={title} subtitle={subtitle} onOpenChange={(isOpen) => !isOpen && onClose()} />}
        content={<LayoutContent>{children}</LayoutContent>}
        footer={footer ? <LayoutFooter hasDivider>{footer}</LayoutFooter> : undefined}
      />
    </Drawer>
  );
}
