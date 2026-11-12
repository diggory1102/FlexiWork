import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { MapPin, Pencil, Plus, Trash2, Wifi } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { Badge } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { branchApi } from '@/services/organizationApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { branchStatusLabels } from '@/lib/format';
import { Roles } from '@/types/auth';
import type { Branch, BranchFilter, BranchStatus } from '@/types/organization';
import { BranchFormModal } from '../components/BranchFormModal';
import { BranchWifiModal } from '../components/BranchWifiModal';

const statusOptions = [
  { value: '', label: 'Tất cả trạng thái' },
  { value: 'active', label: branchStatusLabels.active },
  { value: 'inactive', label: branchStatusLabels.inactive },
];

/** Quản lý chi nhánh: Admin thêm/sửa/xóa; HR xem toàn bộ; Manager chỉ thấy chi nhánh của mình. */
export function BranchesPage() {
  const isAdmin = useAuthStore((s) => s.hasAnyRole([Roles.Admin]));
  const invalidateLookups = useLookupStore((s) => s.invalidate);
  const { query, setQuery, page, rows, loading, error, reload } = usePagedList<Branch, BranchFilter>(branchApi.list, { pageSize: 20 });

  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [editing, setEditing] = useState<Branch | null | undefined>(undefined); // undefined = đóng, null = tạo mới
  const [wifiBranch, setWifiBranch] = useState<Branch | null>(null);
  const [deleting, setDeleting] = useState<Branch | null>(null);
  const [deletingBusy, setDeletingBusy] = useState(false);

  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  const afterChange = () => {
    invalidateLookups();
    void reload();
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeletingBusy(true);
    try {
      const { message } = await branchApi.remove(deleting.id);
      toast.success(message);
      setDeleting(null);
      afterChange();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setDeletingBusy(false);
    }
  };

  const columns: Column<Branch>[] = [
    { key: 'code', header: 'Mã', width: 120, render: (b) => <span className="font-mono text-sm font-medium">{b.branchCode}</span> },
    {
      key: 'name',
      header: 'Chi nhánh',
      render: (b) => (
        <VStack gap={0.5}>
          <Text weight="medium">{b.name}</Text>
          <Text type="supporting" maxLines={1}>
            {b.address}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'geo',
      header: 'Vùng chấm công',
      render: (b) => (
        <a
          href={`https://www.google.com/maps?q=${b.latitude},${b.longitude}`}
          target="_blank"
          rel="noreferrer"
          onClick={(e) => e.stopPropagation()}
          className="inline-flex items-center gap-1 whitespace-nowrap text-xs text-accent hover:underline"
        >
          <MapPin className="h-3.5 w-3.5" />
          {b.latitude.toFixed(5)}, {b.longitude.toFixed(5)} · {b.radius} m
        </a>
      ),
    },
    { key: 'contact', header: 'Liên hệ', render: (b) => <Text type="supporting">{b.phone ?? '—'}</Text> },
    {
      key: 'status',
      header: 'Trạng thái',
      render: (b) => <Badge tone={b.status === 'active' ? 'green' : 'gray'}>{branchStatusLabels[b.status]}</Badge>,
    },
    {
      key: 'actions',
      header: '',
      width: 150,
      align: 'end',
      render: (b) => (
        <div onClick={(e) => e.stopPropagation()}>
          <HStack gap={0.5} hAlign="end">
            <IconButton variant="ghost" size="sm" label="Wi-Fi nội bộ" tooltip="Wi-Fi nội bộ" icon={<Icon icon={Wifi} />} onClick={() => setWifiBranch(b)} />
            {isAdmin && (
              <>
                <IconButton variant="ghost" size="sm" label="Sửa" tooltip="Sửa" icon={<Icon icon={Pencil} />} onClick={() => setEditing(b)} />
                <IconButton variant="ghost" size="sm" label="Xóa" tooltip="Xóa" icon={<Icon icon={Trash2} color="error" />} onClick={() => setDeleting(b)} />
              </>
            )}
          </HStack>
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Chi nhánh & Wi-Fi"
        description="Cấu hình tọa độ Geofence và danh sách Wi-Fi BSSID hợp lệ để chấm công tại từng chi nhánh."
        actions={isAdmin && <Button label="Thêm chi nhánh" variant="primary" icon={<Icon icon={Plus} />} onClick={() => setEditing(null)} />}
      />
      <ListCard>
        <Toolbar>
          <SearchInput placeholder="Tìm theo mã, tên, địa chỉ…" value={search} onChange={setSearch} />
          <Selector
            label="Trạng thái"
            isLabelHidden
            width={200}
            options={statusOptions}
            value={query.status ?? ''}
            onChange={(v) => setQuery({ status: (v ?? '') as BranchStatus | '' })}
          />
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(b) => b.id} loading={loading} emptyText="Không có chi nhánh nào" onRowClick={setWifiBranch} />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <BranchFormModal open={editing !== undefined} branch={editing ?? null} onClose={() => setEditing(undefined)} onSaved={afterChange} />
      <BranchWifiModal branch={wifiBranch} canEdit={isAdmin} onClose={() => setWifiBranch(null)} />
      <ConfirmDialog
        open={!!deleting}
        title="Xóa chi nhánh"
        message={
          <>
            Xóa chi nhánh <b>{deleting?.name}</b> và toàn bộ Wi-Fi của chi nhánh? Không thể xóa nếu chi nhánh vẫn còn nhân sự.
          </>
        }
        confirmText="Xóa"
        loading={deletingBusy}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}
