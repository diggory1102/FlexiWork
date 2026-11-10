import { useCallback, useEffect, useRef, useState } from 'react';
import { getErrorMessage } from '@/services/apiClient';
import type { PagedQuery, PaginatedResult } from '@/types/api';

/**
 * ViewModel cho màn hình danh sách (MVVM): giữ bộ lọc, gọi API phân trang, trạng thái tải/lỗi.
 * Đổi bộ lọc (trừ pageIndex) sẽ tự quay về trang 1.
 */
export function usePagedList<T, Q extends PagedQuery>(
  fetcher: (query: Q) => Promise<{ data: PaginatedResult<T> }>,
  initialQuery: Q,
) {
  const [query, setQueryState] = useState<Q>(initialQuery);
  const [page, setPage] = useState<PaginatedResult<T> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const requestId = useRef(0);

  const load = useCallback(async () => {
    const id = ++requestId.current;
    setLoading(true);
    setError(null);
    try {
      const { data } = await fetcher(query);
      if (id === requestId.current) setPage(data); // bỏ qua kết quả của request cũ
    } catch (err) {
      if (id === requestId.current) setError(getErrorMessage(err));
    } finally {
      if (id === requestId.current) setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [JSON.stringify(query)]);

  useEffect(() => {
    void load();
  }, [load]);

  const setQuery = useCallback((changes: Partial<Q>) => {
    setQueryState((prev) => ({ ...prev, ...changes, pageIndex: 'pageIndex' in changes ? changes.pageIndex : 1 }));
  }, []);

  return { query, setQuery, page, rows: page?.items ?? [], loading, error, reload: load };
}
