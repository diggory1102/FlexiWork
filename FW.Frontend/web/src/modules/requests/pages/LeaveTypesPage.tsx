import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Paperclip, Pencil, Plus, Trash2 } from 'lucide-react';
import { PageHeader } from '@/components/ui/Layout';
import { ListCard } from '@/components/ui/ListCard';
import { Badge } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { leaveTypeApi } from '@/services/requestApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import type { LeaveType } from '@/types/requests';
import { LeaveTypeFormModal } from '../components/LeaveTypeFormModal';
import { categoryLabels, categoryTone } from '../requestUi';

/** Loại đơn (UC 1.4) — chỉ Admin / HR cấu hình. Loại tạm ngừng không hiện trên ứng dụng nhưng đơn cũ vẫn giữ. */
export function LeaveTypesPage() {
  const [types, setTypes] = useState<LeaveType[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<LeaveType | null | undefined>(undefined); // undefined = đóng, null = tạo mới
  const [deleting, setDeleting] = useState<LeaveType | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setTypes((await leaveTypeApi.list(true)).data);
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => void load(), [load]);

  const confirmDelete = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      toast.success((await leaveTypeApi.remove(deleting.id)).message);
      setDeleting(null);
      void load();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const columns: Column<LeaveType>[] = [
    {
      key: 'name',
      header: 'Loại đơn',
      render: (t) => (
        <VStack gap={0.5}>
          <Text weight="medium">{t.name}</Text>
          {t.description && (
            <Text type="supporting" maxLines={1}>
              {t.description}
            </Text>
          )}
        </VStack>
      ),
    },
    { key: 'category', header: 'Nhóm', width: 170, render: (t) => <Badge tone={categoryTone[t.category]}>{categoryLabels[t.category]}</Badge> },
    {
      key: 'rules',
      header: 'Quy tắc',
      width: 260,
      render: (t) =>
        t.category === 'leave' ? (
          <HStack gap={1} wrap="wrap">
            <Badge tone={t.isPaid ? 'green' : 'gray'}>{t.isPaid ? 'Có lương' : 'Không lương'}</Badge>
            {t.deductsBalance && <Badge tone="blue">Trừ phép năm</Badge>}
          </HStack>
        ) : (
          <Text type="supporting">—</Text>
        ),
    },
    {
      key: 'attachment',
      header: 'Ảnh minh chứng',
      width: 150,
      render: (t) =>
        t.requiresAttachment ? (
          <HStack gap={1} vAlign="center">
            <Icon icon={Paperclip} size="sm" color="secondary" />
            <Text size="sm">Bắt buộc</Text>
          </HStack>
        ) : (
          <Text type="supporting">Không bắt buộc</Text>
        ),
    },
    {
      key: 'status',
      header: 'Trạng thái',
      width: 150,
      render: (t) => <Badge tone={t.isActive ? 'green' : 'gray'}>{t.isActive ? 'Đang nhận đơn' : 'Tạm ngừng'}</Badge>,
    },
    {
      key: 'actions',
      header: '',
      width: 100,
      align: 'end',
      render: (t) => (
        <HStack gap={0.5} hAlign="end">
          <IconButton variant="ghost" size="sm" label="Sửa" tooltip="Sửa" icon={<Icon icon={Pencil} />} onClick={() => setEditing(t)} />
          <IconButton
            variant="ghost"
            size="sm"
            label="Xóa"
            tooltip="Xóa"
            icon={<Icon icon={Trash2} color="error" />}
            onClick={() => setDeleting(t)}
          />
        </HStack>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Loại đơn"
        description="Danh mục các loại đơn nhân viên được gửi: nghỉ, đổi ca, làm thêm giờ, giải trình. HR đặt tên tùy ý; cách xử lý đơn theo nhóm."
        actions={<Button label="Thêm loại đơn" variant="primary" icon={<Icon icon={Plus} />} onClick={() => setEditing(null)} />}
      />
      <ListCard>
        <DataTable columns={columns} rows={types} rowKey={(t) => t.id} loading={loading} emptyText="Chưa có loại đơn nào" onRowClick={setEditing} />
      </ListCard>

      <LeaveTypeFormModal open={editing !== undefined} type={editing ?? null} onClose={() => setEditing(undefined)} onSaved={() => void load()} />
      <ConfirmDialog
        open={!!deleting}
        title="Xóa loại đơn"
        message={
          <>
            Xóa loại đơn <b>{deleting?.name}</b>? Không xóa được nếu còn đơn đang chờ xử lý — hãy chuyển sang <b>Tạm ngừng</b> để giữ lịch sử.
          </>
        }
        confirmText="Xóa"
        loading={busy}
        onConfirm={() => void confirmDelete()}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}
