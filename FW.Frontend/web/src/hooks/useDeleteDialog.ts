import { useState } from 'react';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';

/**
 * Luồng xóa có xác nhận: chọn bản ghi → hộp xác nhận → gọi API → toast → tải lại.
 * Trang vẫn tự viết tiêu đề / nội dung cảnh báo của <ConfirmDialog> và truyền `dialog` vào:
 * <ConfirmDialog {...del.dialog} title="Xóa phòng ban" message={...} />
 */
export function useDeleteDialog<T>(remove: (item: T) => Promise<{ message: string }>, onDeleted: () => void) {
  const [target, setTarget] = useState<T | null>(null);
  const [busy, setBusy] = useState(false);

  const confirm = async () => {
    if (!target) return;
    setBusy(true);
    try {
      toast.success((await remove(target)).message);
      setTarget(null);
      onDeleted();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return {
    /** Bản ghi đang chờ xác nhận xóa (dùng để hiện tên trong nội dung cảnh báo). */
    target,
    /** Mở hộp xác nhận cho một bản ghi. */
    ask: setTarget,
    /** Props cho <ConfirmDialog>: open, loading, onConfirm, onClose. */
    dialog: { open: target !== null, loading: busy, onConfirm: () => void confirm(), onClose: () => setTarget(null) },
  };
}
