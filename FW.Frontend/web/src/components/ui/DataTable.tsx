import type { ReactNode } from 'react';
import { Pagination as AstryxPagination } from '@astryxdesign/core/Pagination';
import { HStack } from '@astryxdesign/core/Stack';
import { Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, pixel, proportional, useTableStickyColumns, type TablePlugin } from '@astryxdesign/core/Table';
import { Text } from '@astryxdesign/core/Text';
import type { PaginatedResult } from '@/types/api';
import { EmptyState, Spinner } from './Feedback';

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  /** Độ rộng tối thiểu (px) của cột — đặt trên ô tiêu đề (class Tailwind bị style của Astryx Table ghi đè). */
  width?: number;
  /** Class Tailwind thêm cho ô. */
  className?: string;
  align?: 'start' | 'center' | 'end';
}

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[];
  rowKey: (row: T) => string | number;
  loading?: boolean;
  emptyText?: string;
  onRowClick?: (row: T) => void;
  /** Độ rộng tối thiểu của bảng (px) — dưới mức này bảng cuộn ngang. Mặc định 720; bảng nhỏ trong ngăn / thẻ hẹp đặt thấp hơn. */
  minWidth?: number;
  /** Cột ghim bên trái khi cuộn ngang (khóa cột, tính từ cột đầu). */
  stickyStart?: string[];
  /** Độ cao hàng: `balanced` (mặc định, gọn) hoặc `spacious` cho danh sách cần thoáng. */
  density?: 'balanced' | 'spacious';
}

const alignClass = { start: 'text-left', center: 'text-center', end: 'text-right' };

/** Bảng dữ liệu (Astryx Table, chế độ tự ghép): trạng thái đang tải, rỗng, bấm dòng để xem chi tiết. */
export function DataTable<T>({ columns, rows, rowKey, loading, emptyText = 'Chưa có dữ liệu', onRowClick, minWidth = 720, stickyStart, density = 'balanced' }: DataTableProps<T>) {
  const cellClass = (c: Column<T>) => `${c.align ? alignClass[c.align] : ''} ${c.className ?? ''}`;

  return (
    <div className="relative -mb-4 overflow-x-auto pb-4">
      {stickyStart ? (
        <PinnedTable columns={columns} rows={rows} rowKey={rowKey} onRowClick={onRowClick} stickyStart={stickyStart} density={density} />
      ) : (
      <div style={{ minWidth }}>
        <Table density={density} hasHover verticalAlign="middle">
          <TableHeader>
            <TableRow>
              {columns.map((c) => (
                <TableHeaderCell key={c.key} className={cellClass(c)} style={c.width ? { width: c.width, minWidth: c.width } : undefined}>
                  {c.header}
                </TableHeaderCell>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => (
              <TableRow
                key={rowKey(row)}
                onClick={onRowClick ? () => onRowClick(row) : undefined}
                className={onRowClick ? 'cursor-pointer' : undefined}
              >
                {columns.map((c) => (
                  <TableCell key={c.key} className={cellClass(c)}>
                    {c.render(row)}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      )}
      {!loading && rows.length === 0 && <EmptyState title={emptyText} />}
      {loading && (
        <div className={`flex items-center justify-center ${rows.length === 0 ? 'py-16' : 'absolute inset-0 bg-surface/60'}`}>
          <Spinner />
        </div>
      )}
    </div>
  );
}

type Row = Record<string, unknown>;

/** Bảng theo chế độ dữ liệu của Astryx để dùng được plugin ghim cột. */
function PinnedTable<T>({
  columns,
  rows,
  rowKey,
  onRowClick,
  stickyStart,
  density,
}: Pick<DataTableProps<T>, 'columns' | 'rows' | 'rowKey' | 'onRowClick' | 'density'> & { stickyStart: string[] }) {
  const sticky = useTableStickyColumns<Row>({ startKeys: stickyStart });
  const behaviour: TablePlugin<Row> = {
    transformBodyRow: (props, item) =>
      onRowClick ? { ...props, htmlProps: { ...props.htmlProps, onClick: () => onRowClick(item as T), style: { ...props.htmlProps.style, cursor: 'pointer' } } } : props,
    transformBodyCell: (props, column) => ({
      ...props,
      htmlProps: {
        ...props.htmlProps,
        className: [props.htmlProps.className, columns.find((c) => c.key === column.key)?.className].filter(Boolean).join(' '),
        style: { ...props.htmlProps.style, whiteSpace: 'nowrap' },
      },
    }),
  };
  return (
    <Table<Row>
      density={density}
      hasHover
      verticalAlign="middle"
      data={rows as unknown as Row[]}
      idKey={(r) => rowKey(r as T)}
      columns={columns.map((c) => ({
        key: c.key,
        header: c.header,
        align: c.align,
        width: !c.width ? undefined : stickyStart.includes(c.key) ? pixel(c.width) : proportional(c.width, { minWidth: c.width }),
        renderCell: (r: Row) => c.render(r as T),
      }))}
      plugins={{ sticky, behaviour }}
    />
  );
}

/** Phân trang dưới bảng: "Hiển thị x–y / N bản ghi" + điều hướng trang của Astryx. */
export function Pagination<T>({ page, onChange }: { page: PaginatedResult<T> | null; onChange: (pageIndex: number) => void }) {
  if (!page || page.totalCount === 0) return null;
  const from = (page.pageIndex - 1) * page.pageSize + 1;
  const to = Math.min(page.pageIndex * page.pageSize, page.totalCount);

  return (
    <HStack gap={3} wrap="wrap" vAlign="center" hAlign="between" padding={3} className="border-t border-border">
      <Text type="supporting">
        Hiển thị {from}–{to} / {page.totalCount} bản ghi
      </Text>
      <AstryxPagination page={page.pageIndex} totalItems={page.totalCount} pageSize={page.pageSize} onChange={onChange} size="sm" />
    </HStack>
  );
}
