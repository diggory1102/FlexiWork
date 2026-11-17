import { useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Paperclip, Plus } from 'lucide-react';
import { PageHeader } from '@/components/ui/Layout';
import { ListCard } from '@/components/ui/ListCard';
import { Badge } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { RowActions } from '@/components/ui/RowActions';
import { useApiData } from '@/hooks/useApiData';
import { useDeleteDialog } from '@/hooks/useDeleteDialog';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { leaveTypeApi } from '@/services/requestApi';
import type { LeaveType } from '@/types/requests';
import { LeaveTypeFormModal } from '../components/LeaveTypeFormModal';
import { categoryLabels, categoryTone } from '../requestUi';

/** Loại đơn (UC 1.4) — chỉ Admin / HR cấu hình. Loại tạm ngừng không hiện trên ứng dụng nhưng đơn cũ vẫn giữ. */
export function LeaveTypesPage() {
  const { data, loading, reload } = useApiData(() => leaveTypeApi.list(true), []);
  const types = data ?? [];
  const [editing, setEditing] = useState<LeaveType | null | undefined>(undefined); // undefined = đóng, null = tạo mới

  const del = useDeleteDialog((item: LeaveType) => leaveTypeApi.remove(item.id), () => void reload());

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
      render: (t) => <RowActions onEdit={() => setEditing(t)} onDelete={() => del.ask(t)} />,
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

      <LeaveTypeFormModal open={editing !== undefined} type={editing ?? null} onClose={() => setEditing(undefined)} onSaved={() => void reload()} />
      <ConfirmDialog
        {...del.dialog}
        title="Xóa loại đơn"
        message={
          <>
            Xóa loại đơn <b>{del.target?.name}</b>? Không xóa được nếu còn đơn đang chờ xử lý — hãy chuyển sang <b>Tạm ngừng</b> để giữ lịch sử.
          </>
        }
        confirmText="Xóa"
      />
    </>
  );
}
