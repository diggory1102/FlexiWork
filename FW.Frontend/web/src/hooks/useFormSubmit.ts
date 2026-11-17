import { useCallback, useState } from 'react';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';

/**
 * Phần chung của form trong hộp thoại: trạng thái đang lưu, lỗi hiển thị trong form, try/catch, toast thành công.
 * Trường nhập và kiểm tra hợp lệ vẫn do từng form tự viết:
 *
 *   if (!form.name.trim()) return setError('Vui lòng nhập tên.');
 *   void run(async () => (await api.save(form)).message, () => { onSaved(); onClose(); });
 */
export function useFormSubmit() {
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  /** `action` trả về thông báo thành công (hiện bằng toast); `onSuccess` chạy sau đó. Lỗi hiện trong form. */
  const run = useCallback(async (action: () => Promise<string | void>, onSuccess?: () => void) => {
    setSaving(true);
    setError(null);
    try {
      const message = await action();
      if (message) toast.success(message);
      onSuccess?.();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }, []);

  return { saving, error, setError, run };
}
