import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { Toolbar } from '@/components/ui/Layout';
import { ListCard } from '@/components/ui/ListCard';
import { Badge } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { shiftApi } from '@/services/schedulingApi';
import { taskApi } from '@/services/taskApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { Roles, RoleGroups } from '@/types/auth';
import type { Shift } from '@/types/scheduling';
import type { TaskTemplate } from '@/types/tasks';
import { phaseLabels, phaseTone } from '../taskUi';
import { TaskTemplateFormModal } from './TaskTemplateFormModal';

/**
 * Mẫu checklist theo ca (UC 6.1). Admin thêm / sửa / xóa mọi việc; Quản lý chỉ việc riêng của chi nhánh mình (việc chung của Admin
 * chỉ xem); HR chỉ xem. Việc được giao tự động khi nhân viên check-in vào ca.
 */
export function TaskTemplatesTab() {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const canWrite = useAuthStore((s) => s.hasAnyRole([Roles.Admin, Roles.Manager]));
  const { branches, load: loadLookups } = useLookupStore();

  const [shifts, setShifts] = useState<Shift[]>([]);
  const [shiftId, setShiftId] = useState<number | ''>('');
  const [branchId, setBranchId] = useState<number | ''>('');
  const [tasks, setTasks] = useState<TaskTemplate[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<TaskTemplate | null | undefined>(undefined); // undefined = đóng, null = tạo mới
  const [deleting, setDeleting] = useState<TaskTemplate | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    void loadLookups().catch(() => undefined);
    shiftApi
      .list({ pageSize: 100, includeShared: true })
      .then(({ data }) => setShifts(data.items))
      .catch(() => undefined);
  }, [loadLookups]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setTasks((await taskApi.templates({ shiftId, branchId })).data);
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [shiftId, branchId]);
  useEffect(() => void load(), [load]);

  const confirmDelete = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      toast.success((await taskApi.removeTemplate(deleting.id)).message);
      setDeleting(null);
      void load();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const columns: Column<TaskTemplate>[] = [
    {
      key: 'title',
      header: 'Việc cần làm',
      render: (t) => (
        <VStack gap={0.5}>
          <Text weight="medium">{t.title}</Text>
          {t.description && (
            <Text type="supporting" maxLines={2}>
              {t.description}
            </Text>
          )}
        </VStack>
      ),
    },
    { key: 'shift', header: 'Ca', width: 190, render: (t) => <Text>{t.shiftName}</Text> },
    {
      key: 'phase',
      header: 'Giai đoạn',
      width: 130,
      render: (t) => (
        <HStack gap={1.5} vAlign="center">
          <Badge tone={phaseTone[t.phase]}>{phaseLabels[t.phase]}</Badge>
          <Text type="supporting" size="sm" hasTabularNumbers>
            #{t.sortOrder}
          </Text>
        </HStack>
      ),
    },
    {
      key: 'scope',
      header: 'Áp dụng cho',
      width: 220,
      render: (t) => (
        <VStack gap={1} hAlign="start">
          {t.branchId == null ? <Badge tone="green">Mọi chi nhánh</Badge> : <Badge tone="gray">{t.branchName}</Badge>}
          <Text type="supporting" size="sm">
            {t.positionTitle ?? 'Mọi chức vụ'}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'rules',
      header: 'Yêu cầu',
      width: 170,
      render: (t) => (
        <HStack gap={1} wrap="wrap" vAlign="center">
          {t.isMandatory ? <Badge tone="amber">Bắt buộc</Badge> : <Badge tone="gray">Tùy chọn</Badge>}
          {t.requiresPhoto && <Badge tone="blue">Cần ảnh</Badge>}
        </HStack>
      ),
    },
    {
      key: 'actions',
      header: '',
      width: 100,
      align: 'end',
      render: (t) =>
        t.canEdit && (
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
      <ListCard>
        <Toolbar>
          <Selector
            label="Ca"
            isLabelHidden
            width={280}
            options={[
              { value: '', label: 'Tất cả ca' },
              ...shifts.map((s) => ({ value: String(s.id), label: `${s.name} (${s.startTime}–${s.endTime})` })),
            ]}
            value={String(shiftId)}
            onChange={(v) => setShiftId(v ? Number(v) : '')}
          />
          {isCompanyWide && (
            <Selector
              label="Chi nhánh"
              isLabelHidden
              width={220}
              options={[{ value: '', label: 'Mọi chi nhánh' }, ...branches.map((b) => ({ value: String(b.id), label: b.name }))]}
              value={String(branchId)}
              onChange={(v) => setBranchId(v ? Number(v) : '')}
            />
          )}
          {canWrite && <Button label="Thêm việc" variant="primary" icon={<Icon icon={Plus} />} onClick={() => setEditing(null)} />}
        </Toolbar>
        <DataTable
          columns={columns}
          rows={tasks}
          rowKey={(t) => t.id}
          loading={loading}
          emptyText="Chưa có việc nào trong checklist"
          onRowClick={(t) => t.canEdit && setEditing(t)}
        />
      </ListCard>

      <TaskTemplateFormModal
        open={editing !== undefined}
        task={editing ?? null}
        defaultShiftId={shiftId === '' ? undefined : shiftId}
        onClose={() => setEditing(undefined)}
        onSaved={() => void load()}
      />
      <ConfirmDialog
        open={!!deleting}
        title="Xóa việc khỏi checklist"
        message={
          <>
            Xóa việc <b>{deleting?.title}</b>? Các ca đã giao việc này vẫn giữ lịch sử; ca sau sẽ không còn được giao.
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
