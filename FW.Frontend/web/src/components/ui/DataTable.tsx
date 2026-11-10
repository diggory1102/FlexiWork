import type { ReactNode } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import type { PaginatedResult } from '@/types/api';
import { EmptyState, Spinner } from './Feedback';

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  className?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  loading?: boolean;
  emptyText?: string;
  onRowClick?: (row: T) => void;
}

export function DataTable<T>({ columns, rows, rowKey, loading, emptyText = 'Chưa có dữ liệu', onRowClick }: DataTableProps<T>) {
  return (
    <div className="relative overflow-x-auto">
      <table className="w-full min-w-[720px] text-left text-sm">
        <thead>
          <tr className="border-b border-border bg-background/60 text-xs uppercase tracking-wide text-muted">
            {columns.map((c) => (
              <th key={c.key} className={`px-4 py-2.5 font-medium ${c.className ?? ''}`}>
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr
              key={rowKey(row)}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              className={`border-b border-border/70 last:border-0 ${onRowClick ? 'cursor-pointer hover:bg-primary/5' : 'hover:bg-background/50'}`}
            >
              {columns.map((c) => (
                <td key={c.key} className={`px-4 py-3 align-middle ${c.className ?? ''}`}>
                  {c.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {!loading && rows.length === 0 && <EmptyState title={emptyText} />}
      {loading && (
        <div className={`flex items-center justify-center ${rows.length === 0 ? 'py-16' : 'absolute inset-0 bg-surface/60'}`}>
          <Spinner />
        </div>
      )}
    </div>
  );
}

export function Pagination<T>({ page, onChange }: { page: PaginatedResult<T> | null; onChange: (pageIndex: number) => void }) {
  if (!page || page.totalCount === 0) return null;
  const from = (page.pageIndex - 1) * page.pageSize + 1;
  const to = Math.min(page.pageIndex * page.pageSize, page.totalCount);

  return (
    <div className="flex items-center justify-between border-t border-border px-4 py-3 text-xs text-muted">
      <span>
        Hiển thị <b className="text-foreground">{from}–{to}</b> / {page.totalCount} bản ghi
      </span>
      <div className="flex items-center gap-1">
        <button
          type="button"
          className="rounded-md p-1.5 hover:bg-background disabled:opacity-40"
          disabled={!page.hasPreviousPage}
          onClick={() => onChange(page.pageIndex - 1)}
          aria-label="Trang trước"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <span className="px-2">
          Trang {page.pageIndex} / {Math.max(page.totalPages, 1)}
        </span>
        <button
          type="button"
          className="rounded-md p-1.5 hover:bg-background disabled:opacity-40"
          disabled={!page.hasNextPage}
          onClick={() => onChange(page.pageIndex + 1)}
          aria-label="Trang sau"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
