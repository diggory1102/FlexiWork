import { useEffect, type ReactNode } from 'react';
import { AlertTriangle, X } from 'lucide-react';
import { Button } from './Button';

interface ModalProps {
  open: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  footer?: ReactNode;
  size?: 'sm' | 'md' | 'lg';
  children: ReactNode;
}

const widths = { sm: 'max-w-md', md: 'max-w-xl', lg: 'max-w-3xl' };

export function Modal({ open, title, description, onClose, footer, size = 'md', children }: ModalProps) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-header/50 p-4 pt-[8vh]" onMouseDown={onClose}>
      <div className={`card w-full ${widths[size]} shadow-xl`} role="dialog" aria-modal="true" onMouseDown={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between gap-4 border-b border-border px-5 py-4">
          <div>
            <h2 className="text-base font-semibold text-foreground">{title}</h2>
            {description && <p className="mt-0.5 text-xs text-muted">{description}</p>}
          </div>
          <button type="button" onClick={onClose} className="rounded-md p-1 text-muted hover:bg-background" aria-label="Đóng">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="max-h-[70vh] overflow-y-auto px-5 py-4">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-border px-5 py-3">{footer}</div>}
      </div>
    </div>
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

export function ConfirmDialog({ open, title, message, confirmText = 'Xác nhận', tone = 'danger', loading, onConfirm, onClose }: ConfirmDialogProps) {
  return (
    <Modal
      open={open}
      title={title}
      onClose={onClose}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={loading}>
            Hủy
          </Button>
          <Button variant={tone === 'danger' ? 'danger' : 'primary'} onClick={onConfirm} loading={loading}>
            {confirmText}
          </Button>
        </>
      }
    >
      <div className="flex gap-3">
        <AlertTriangle className={`mt-0.5 h-5 w-5 shrink-0 ${tone === 'danger' ? 'text-danger' : 'text-warning'}`} />
        <div className="text-sm text-foreground">{message}</div>
      </div>
    </Modal>
  );
}
