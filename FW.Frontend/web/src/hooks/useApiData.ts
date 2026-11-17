import { useCallback, useEffect, useRef, useState, type DependencyList } from 'react';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';

/**
 * Nạp một dữ liệu từ API (không phân trang): tự nạp lại khi `deps` đổi, bỏ qua kết quả của request cũ,
 * báo lỗi bằng toast (tắt được bằng toastError: false để trang tự hiện lỗi).
 * Với danh sách phân trang dùng `usePagedList`.
 */
export function useApiData<T>(
  fetcher: () => Promise<{ data: T }>,
  deps: DependencyList,
  options: { enabled?: boolean; toastError?: boolean } = {},
) {
  const { enabled = true, toastError = true } = options;
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);
  const requestId = useRef(0);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const load = useCallback(async () => {
    const id = ++requestId.current;
    setLoading(true);
    setError(null);
    try {
      const result = await fetcher();
      if (id === requestId.current) setData(result.data);
    } catch (err) {
      if (id !== requestId.current) return;
      const message = getErrorMessage(err);
      setError(message);
      if (toastError) toast.error(message);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  }, deps);

  useEffect(() => {
    if (enabled) void load();
    else {
      requestId.current++; // hủy kết quả đang chờ
      setData(null);
      setLoading(false);
    }
  }, [enabled, load]);

  return { data, loading, error, reload: load };
}
