import type { ReactNode } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Dialog, DialogHeader } from '@astryxdesign/core/Dialog';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { AlertTriangle } from 'lucide-react';

interface ModalProps {
  open: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  footer?: ReactNode;
  size?: 'sm' | 'md' | 'lg';
  /** form: chỉ đóng bằng nút/Esc sau khi đã nhập liệu (tránh mất dữ liệu khi bấm nhầm ra ngoài). */
  purpose?: 'info' | 'form';
  children: ReactNode;
}

const widths = { sm: 440, md: 600, lg: 820 };

/** Hộp thoại (Astryx Dialog): tiêu đề + nút đóng, nội dung cuộn, chân hộp thoại chứa nút thao tác. */
export function Modal({ open, title, description, onClose, footer, size = 'md', purpose = 'info', children }: ModalProps) {
  return (
    <Dialog isOpen={open} onOpenChange={(isOpen) => !isOpen && onClose()} width={widths[size]} maxHeight="88dvh" purpose={purpose}>
      <VStack gap={4}>
        <DialogHeader title={title} subtitle={description} onOpenChange={(isOpen) => !isOpen && onClose()} />
        <div>{children}</div>
        {footer && (
          <HStack gap={2} hAlign="end">
            {footer}
          </HStack>
        )}
      </VStack>
    </Dialog>
  );
}

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmText?: string;
  tone?: 'danger' | 'primary';
  loading?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}

/** Hộp xác nhận thao tác (xóa, khóa, cấp lại mật khẩu…). */
export function ConfirmDialog({ open, title, message, confirmText = 'Xác nhận', tone = 'danger', loading, onConfirm, onClose }: ConfirmDialogProps) {
  return (
    <Modal
      open={open}
      title={title}
      onClose={onClose}
      size="sm"
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} isDisabled={loading} />
          <Button label={confirmText} variant={tone === 'danger' ? 'destructive' : 'primary'} onClick={onConfirm} isLoading={loading} />
        </>
      }
    >
      <HStack gap={3} vAlign="start">
        <Icon icon={AlertTriangle} color={tone === 'danger' ? 'error' : 'warning'} />
        <VStack>
          <Text as="div">{message}</Text>
        </VStack>
      </HStack>
    </Modal>
  );
}
