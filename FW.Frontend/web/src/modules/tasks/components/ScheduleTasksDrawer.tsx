import { useEffect, useState } from 'react';
import { Icon } from '@astryxdesign/core/Icon';
import { Lightbox } from '@astryxdesign/core/Lightbox';
import { ProgressBar } from '@astryxdesign/core/ProgressBar';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { Thumbnail } from '@astryxdesign/core/Thumbnail';
import { Button } from '@astryxdesign/core/Button';
import { CheckCircle2, Circle } from 'lucide-react';
import { DetailDrawer } from '@/components/ui/DetailDrawer';
import { Avatar } from '@/components/ui/Layout';
import { Badge, FormError, Spinner } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { useDeleteDialog } from '@/hooks/useDeleteDialog';
import { useAuthStore } from '@/stores/authStore';
import { Roles } from '@/types/auth';
import { taskApi } from '@/services/taskApi';
import { getErrorMessage } from '@/services/apiClient';
import { formatDate } from '@/lib/format';
import { scheduleStatusLabels } from '@/modules/schedules/scheduleUi';
import type { ScheduleStatus } from '@/types/scheduling';
import type { ScheduleProgress, ScheduleTasks, TaskItem, TaskPhase } from '@/types/tasks';
import { phaseLabels, progressVariant } from '../taskUi';

const phases: TaskPhase[] = ['start', 'during', 'end'];

interface Props {
  /** Ca đang xem (giữ lại khi đóng để ngăn kéo không trống lúc trượt ra). */
  row: ScheduleProgress | null;
  date: string;
  open: boolean;
  onClose: () => void;
  /** Gọi sau khi Quản lý bỏ tích một việc (để tải lại bảng tiến độ). */
  onChanged?: () => void;
}

/** Ngăn chi tiết checklist của một ca (UC 6.4): từng việc đã làm hay chưa, giờ xong, ghi chú và ảnh hiện trường. */
export function ScheduleTasksDrawer({ row, date, open, onClose, onChanged }: Props) {
  const [data, setData] = useState<ScheduleTasks | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [photo, setPhoto] = useState<{ src: string; alt: string } | null>(null);

  const canUndo = useAuthStore((s) => s.hasAnyRole([Roles.Admin, Roles.Manager]));
  const [reloadKey, setReloadKey] = useState(0);
  const undo = useDeleteDialog(
    (item: TaskItem) => taskApi.uncomplete(item.assignmentId!),
    () => {
      setReloadKey((k) => k + 1);
      onChanged?.();
    },
  );

  const scheduleId = row?.scheduleId;
  useEffect(() => {
    if (!open || scheduleId == null) return;
    let cancelled = false;
    setData(null);
    setError(null);
    taskApi
      .scheduleTasks(scheduleId)
      .then(({ data: d }) => !cancelled && setData(d))
      .catch((err) => !cancelled && setError(getErrorMessage(err)));
    return () => {
      cancelled = true;
    };
  }, [open, scheduleId, reloadKey]);

  const mandatoryPending = data ? data.tasks.filter((t) => t.isMandatory && !t.isCompleted).length : 0;

  return (
    <>
      <DetailDrawer
        open={open}
        onClose={onClose}
        label="Checklist của ca"
        title="Checklist của ca"
        subtitle={row ? `${row.shiftName} · ${row.startTime}–${row.endTime} · ${formatDate(date)}` : undefined}
        width={560}
        purpose="info"
      >
              {row && (
                <VStack gap={4}>
                  <HStack gap={3} vAlign="center" hAlign="between" wrap="wrap">
                    <HStack gap={3} vAlign="center">
                      <Avatar name={row.fullName} url={row.avatarUrl} size={48} />
                      <VStack gap={0.5}>
                        <Heading level={3}>{row.fullName}</Heading>
                        <Text type="supporting">
                          <span className="font-mono">{row.employeeCode}</span> · {row.branchName}
                        </Text>
                      </VStack>
                    </HStack>
                    <Badge tone={row.scheduleStatus === 'completed' ? 'green' : row.scheduleStatus === 'in_progress' ? 'blue' : 'gray'}>
                      {scheduleStatusLabels[row.scheduleStatus as ScheduleStatus] ?? row.scheduleStatus}
                    </Badge>
                  </HStack>

                  <FormError message={error} />
                  {!data && !error && (
                    <HStack hAlign="center" padding={6}>
                      <Spinner />
                    </HStack>
                  )}

                  {data && (
                    <>
                      {data.isAssigned && data.total > 0 ? (
                        <ProgressBar
                          label="Tiến độ checklist"
                          value={data.percent ?? 0}
                          variant={progressVariant(data.percent, mandatoryPending)}
                          hasValueLabel
                          formatValueLabel={() => `${data.done}/${data.total} việc (${data.percent}%)`}
                        />
                      ) : (
                        <Text type="supporting">
                          {data.isAssigned
                            ? 'Ca này chưa có việc nào trong checklist.'
                            : 'Nhân viên chưa check-in nên chưa được giao việc — dưới đây là việc dự kiến.'}
                        </Text>
                      )}
                      {mandatoryPending > 0 && data.isAssigned && <Badge tone="amber">{mandatoryPending} việc bắt buộc chưa hoàn thành</Badge>}

                      {phases.map((phase) => {
                        const items = data.tasks.filter((t) => t.phase === phase);
                        if (items.length === 0) return null;
                        return (
                          <VStack key={phase} gap={2}>
                            <Text weight="semibold" size="sm" color="secondary">
                              {phaseLabels[phase].toUpperCase()}
                            </Text>
                            {items.map((t) => (
                              <HStack
                                key={t.assignmentId ?? `p${t.taskId}`}
                                gap={3}
                                vAlign="start"
                                hAlign="between"
                                className="rounded-lg border border-border p-3"
                              >
                                <HStack gap={2} vAlign="start">
                                  <Icon icon={t.isCompleted ? CheckCircle2 : Circle} color={t.isCompleted ? 'success' : 'secondary'} size="sm" />
                                  <VStack gap={1}>
                                    <Text weight="medium">{t.title}</Text>
                                    {t.description && (
                                      <Text type="supporting" size="sm">
                                        {t.description}
                                      </Text>
                                    )}
                                    <HStack gap={1.5} wrap="wrap" vAlign="center">
                                      {t.isMandatory && <Badge tone="amber">Bắt buộc</Badge>}
                                      {t.requiresPhoto && <Badge tone="blue">Cần ảnh</Badge>}
                                      {t.assignMode === 'shared' && <Badge tone="purple">Làm chung</Badge>}
                                      {t.isCompleted && t.completedAt && (
                                        <Text size="sm" color="secondary">
                                          {t.assignMode === 'shared' && t.completedByName ? `${t.completedByName} đã làm · ` : ''}
                                          Xong lúc {t.completedAt.slice(11, 16)}
                                        </Text>
                                      )}
                                    </HStack>
                                    {t.note && (
                                      <Text size="sm" as="p">
                                        “{t.note}”
                                      </Text>
                                    )}
                                  </VStack>
                                </HStack>
                                {t.proofImageUrl && (
                                  <Thumbnail
                                    src={t.proofImageUrl}
                                    alt={`Ảnh hiện trường: ${t.title}`}
                                    label="Xem ảnh lớn"
                                    onClick={() => setPhoto({ src: t.proofImageUrl!, alt: `Ảnh hiện trường: ${t.title}` })}
                                    style={{ width: 64, height: 64 }}
                                  />
                                )}
                                {canUndo && t.isCompleted && t.assignmentId != null && row?.scheduleStatus === 'in_progress' && (
                                  <Button label="Bỏ tích" size="sm" variant="ghost" onClick={() => undo.ask(t)} />
                                )}
                              </HStack>
                            ))}
                          </VStack>
                        );
                      })}
                    </>
                  )}
                </VStack>
              )}
      </DetailDrawer>
      <ConfirmDialog
        {...undo.dialog}
        title="Bỏ tích việc này"
        message={
          <>
            Bỏ tích <b>{undo.target?.title}</b>?{' '}
            {undo.target?.assignMode === 'shared' ? 'Việc làm chung sẽ mở lại cho cả ca và ảnh minh chứng (nếu có) bị xóa.' : 'Việc sẽ trở về chưa hoàn thành.'}
          </>
        }
        confirmText="Bỏ tích"
      />
      {photo && <Lightbox isOpen onOpenChange={(isOpen) => !isOpen && setPhoto(null)} media={photo} hasZoom />}
    </>
  );
}
