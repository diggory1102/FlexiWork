import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Switch } from '@astryxdesign/core/Switch';
import { Text } from '@astryxdesign/core/Text';
import { Moon, Plus, Settings2 } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { Badge } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { RowActions } from '@/components/ui/RowActions';
import { useDeleteDialog } from '@/hooks/useDeleteDialog';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { shiftApi } from '@/services/schedulingApi';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { formatMinutes } from '@/lib/dates';
import { RoleGroups } from '@/types/auth';
import type { Shift, ShiftFilter } from '@/types/scheduling';
import { ShiftFormModal } from '../components/ShiftFormModal';

const ALL = 'all';

/**
 * Khung ca làm việc (UC 1.3 / 3.1). Admin/HR quản lý mọi ca; Quản lý thấy ca dùng chung + ca chi nhánh mình
 * và chỉ sửa được ca riêng của chi nhánh mình (cờ canEdit do backend tính).
 */
export function ShiftsPage() {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const { branches, load: loadLookups } = useLookupStore();
  const { query, setQuery, page, rows, loading, error, reload } = usePagedList<Shift, ShiftFilter>(shiftApi.list, {
    pageSize: 50,
    includeShared: true,
  });

  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [editing, setEditing] = useState<Shift | null | undefined>(undefined); // undefined = đóng, null = tạo mới

  useEffect(() => void loadLookups().catch(() => undefined), [loadLookups]);
  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  const branchName = (id: number | null) => branches.find((b) => b.id === id)?.name ?? `Chi nhánh #${id}`;

  const del = useDeleteDialog((item: Shift) => shiftApi.remove(item.id), () => void reload());

  const columns: Column<Shift>[] = [
    {
      key: 'shift',
      header: 'Ca làm việc',
      render: (s) => (
        <VStack gap={0.5}>
          <Text weight="medium">{s.name}</Text>
          <Text type="code" size="sm" color="secondary">
            {s.shiftCode}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'time',
      header: 'Giờ làm',
      width: 190,
      render: (s) => (
        <VStack gap={1}>
          <Text weight="medium" hasTabularNumbers>
            {s.startTime} – {s.endTime}
          </Text>
          {(s.isCrossDay || s.isNightShift) && (
            <HStack gap={1} wrap="wrap">
              {s.isCrossDay && <Badge tone="purple">Qua đêm</Badge>}
              {s.isNightShift && <Badge tone="blue">Ca đêm</Badge>}
            </HStack>
          )}
        </VStack>
      ),
    },
    {
      key: 'duration',
      header: 'Thời gian làm',
      width: 170,
      render: (s) => (
        <VStack gap={0.5}>
          <Text>{formatMinutes(s.workingMinutes)}</Text>
          <Text type="supporting">{s.breakMinutes > 0 ? `Nghỉ ${s.breakMinutes} phút` : 'Không nghỉ giữa ca'}</Text>
        </VStack>
      ),
    },
    { key: 'units', header: 'Hệ số công', width: 110, align: 'center', render: (s) => <Text hasTabularNumbers>{s.workUnits}</Text> },
    {
      key: 'grace',
      header: 'Phút cho phép',
      width: 150,
      render: (s) => (
        <VStack gap={0.5}>
          <Text type="supporting">Muộn {s.lateGraceMinutes} phút</Text>
          <Text type="supporting">Sớm {s.earlyLeaveGraceMinutes} phút</Text>
        </VStack>
      ),
    },
    {
      key: 'scope',
      header: 'Phạm vi',
      width: 180,
      render: (s) => (s.branchId == null ? <Badge tone="green">Dùng chung</Badge> : <Badge tone="gray">{branchName(s.branchId)}</Badge>),
    },
    {
      key: 'actions',
      header: '',
      width: 100,
      align: 'end',
      render: (s) => s.canEdit && <RowActions onEdit={() => setEditing(s)} onDelete={() => del.ask(s)} />,
    },
  ];

  const branchOptions = [{ value: ALL, label: 'Tất cả chi nhánh' }, ...branches.map((b) => ({ value: String(b.id), label: b.name }))];

  return (
    <>
      <PageHeader
        title="Khung ca làm việc"
        description="Khai báo giờ làm, giờ nghỉ, hệ số công và số phút cho phép đi muộn / về sớm của từng ca."
        actions={
          <>
            {isCompanyWide && <Button label="Quy tắc chấm công" variant="secondary" icon={<Icon icon={Settings2} />} href="/settings" />}
            <Button label="Thêm ca" variant="primary" icon={<Icon icon={Plus} />} onClick={() => setEditing(null)} />
          </>
        }
      />
      <ListCard>
        <Toolbar>
          <SearchInput placeholder="Tìm theo mã, tên ca…" value={search} onChange={setSearch} />
          {isCompanyWide && (
            <Selector
              label="Chi nhánh"
              isLabelHidden
              width={220}
              options={branchOptions}
              value={query.branchId ? String(query.branchId) : ALL}
              onChange={(v) => setQuery({ branchId: v && v !== ALL ? Number(v) : '' })}
            />
          )}
          {(!isCompanyWide || !!query.branchId) && (
            <Switch label="Hiện ca dùng chung" value={query.includeShared ?? true} onChange={(v) => setQuery({ includeShared: v })} />
          )}
        </Toolbar>
        <DataTable
          columns={columns}
          rows={rows}
          rowKey={(s) => s.id}
          loading={loading}
          emptyText="Chưa có ca làm việc nào"
          onRowClick={(s) => s.canEdit && setEditing(s)}
        />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <HStack gap={2} vAlign="center" className="mt-3">
        <Icon icon={Moon} size="sm" color="secondary" />
        <Text type="supporting">
          Ca qua đêm (VD 22:00 – 06:00) được tính theo ngày bắt đầu ca. Ca đã được xếp lịch không đổi được giờ — hãy tạo ca mới.
        </Text>
      </HStack>

      <ShiftFormModal open={editing !== undefined} shift={editing ?? null} onClose={() => setEditing(undefined)} onSaved={() => void reload()} />
      <ConfirmDialog
        {...del.dialog}
        title="Xóa ca làm việc"
        message={
          <>
            Xóa ca <b>{del.target?.shiftCode}</b> – {del.target?.name}? Lịch đã qua vẫn giữ nguyên; không xóa được nếu ca còn lịch từ hôm nay trở đi.
          </>
        }
        confirmText="Xóa"
      />
    </>
  );
}
