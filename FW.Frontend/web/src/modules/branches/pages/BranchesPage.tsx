import { useEffect, useState } from 'react';
import { MapPin, Pencil, Plus, Search, Trash2, Wifi } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { Button, IconButton } from '@/components/ui/Button';
import { Input, Select } from '@/components/ui/Form';
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
    { key: 'code', header: 'Mã', render: (b) => <span className="font-mono text-xs font-semibold">{b.branchCode}</span> },
    {
      key: 'name',
      header: 'Chi nhánh',
      render: (b) => (
        <div className="min-w-0">
          <p className="font-medium">{b.name}</p>
          <p className="truncate text-xs text-muted">{b.address}</p>
        </div>
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
          className="inline-flex items-center gap-1 text-xs text-primary hover:underline"
        >
          <MapPin className="h-3.5 w-3.5" />
          {b.latitude.toFixed(5)}, {b.longitude.toFixed(5)} · {b.radius} m
        </a>
      ),
    },
    { key: 'contact', header: 'Liên hệ', render: (b) => <span className="text-xs text-muted">{b.phone ?? '—'}</span> },
    {
      key: 'status',
      header: 'Trạng thái',
      render: (b) => <Badge tone={b.status === 'active' ? 'green' : 'gray'}>{branchStatusLabels[b.status]}</Badge>,
    },
    {
      key: 'actions',
      header: '',
      className: 'w-32 text-right',
      render: (b) => (
        <div className="flex justify-end gap-0.5" onClick={(e) => e.stopPropagation()}>
          <IconButton label="Wi-Fi nội bộ" onClick={() => setWifiBranch(b)}>
            <Wifi className="h-4 w-4" />
          </IconButton>
          {isAdmin && (
            <>
              <IconButton label="Sửa" onClick={() => setEditing(b)}>
                <Pencil className="h-4 w-4" />
              </IconButton>
              <IconButton label="Xóa" tone="danger" onClick={() => setDeleting(b)}>
                <Trash2 className="h-4 w-4" />
              </IconButton>
            </>
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Chi nhánh & Wi-Fi"
        description="Cấu hình tọa độ Geofence và danh sách Wi-Fi BSSID hợp lệ để chấm công tại từng chi nhánh."
        actions={
          isAdmin && (
            <Button icon={<Plus className="h-4 w-4" />} onClick={() => setEditing(null)}>
              Thêm chi nhánh
            </Button>
          )
        }
      />
      <div className="card overflow-hidden">
        <Toolbar>
          <div className="relative w-72">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted" />
            <Input className="pl-9" placeholder="Tìm theo mã, tên, địa chỉ…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
          <Select className="w-48" value={query.status ?? ''} onChange={(e) => setQuery({ status: e.target.value as BranchStatus | '' })}>
            <option value="">Tất cả trạng thái</option>
            <option value="active">Đang hoạt động</option>
            <option value="inactive">Ngừng hoạt động</option>
          </Select>
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(b) => b.id} loading={loading} emptyText="Không có chi nhánh nào" onRowClick={setWifiBranch} />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </div>

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
