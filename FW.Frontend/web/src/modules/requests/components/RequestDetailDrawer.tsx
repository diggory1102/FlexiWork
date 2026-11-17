import { useEffect, useState, type ReactNode } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Drawer, DrawerHeader } from '@astryxdesign/core/Drawer';
import { Icon } from '@astryxdesign/core/Icon';
import { Layout, LayoutContent, LayoutFooter } from '@astryxdesign/core/Layout';
import { Lightbox } from '@astryxdesign/core/Lightbox';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { Thumbnail } from '@astryxdesign/core/Thumbnail';
import { CheckCircle2, Circle, XCircle } from 'lucide-react';
import { Avatar } from '@/components/ui/Layout';
import { Badge, Notice } from '@/components/ui/Feedback';
import { requestApi } from '@/services/requestApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { formatDate, formatDateTime } from '@/lib/format';
import type { LeaveRequest } from '@/types/requests';
import { categoryLabels, categoryTone, days, hours, scheduleLabel, statusLabels, statusTone, timeOf } from '../requestUi';

/** Một bước trong tiến trình xử lý đơn. */
function Step({ state, title, detail }: { state: 'done' | 'failed' | 'waiting'; title: string; detail?: ReactNode }) {
  const icon = state === 'done' ? CheckCircle2 : state === 'failed' ? XCircle : Circle;
  const color = state === 'done' ? 'success' : state === 'failed' ? 'error' : 'secondary';
  return (
    <HStack gap={2} vAlign="start">
      <Icon icon={icon} color={color} size="sm" />
      <VStack gap={0.5}>
        <Text weight="medium" size="sm" color={state === 'waiting' ? 'secondary' : 'primary'}>
          {title}
        </Text>
        {detail && (
          <Text type="supporting" size="sm" as="div">
            {detail}
          </Text>
        )}
      </VStack>
    </HStack>
  );
}

interface Props {
  /** Đơn đang xem (giữ lại khi đóng để ngăn kéo không trống lúc trượt ra). */
  request: LeaveRequest | null;
  open: boolean;
  onClose: () => void;
  /** Gọi sau khi duyệt / từ chối xong để danh sách tải lại. */
  onReviewed: () => void;
}

/**
 * Ngăn chi tiết một đơn (UC 5.3): nội dung theo nhóm, ảnh minh chứng, tiến trình xử lý. Đơn chờ duyệt (và người xem có quyền)
 * thì duyệt / từ chối ngay tại đây — từ chối phải ghi lý do; thông báo sau khi duyệt tóm tắt các thay đổi đã áp dụng
 * (trừ phép, ca chuyển sang nghỉ, đổi người làm ca...).
 */
export function RequestDetailDrawer({ request, open, onClose, onReviewed }: Props) {
  const [current, setCurrent] = useState<LeaveRequest | null>(request);
  const [note, setNote] = useState('');
  const [noteError, setNoteError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState<'approve' | 'reject' | null>(null);
  const [photoOpen, setPhotoOpen] = useState(false);

  const requestId = request?.id;
  useEffect(() => {
    setCurrent(request);
    setNote('');
    setNoteError(null);
    setError(null);
    // Chỉ đặt lại khi mở đơn khác
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requestId, open]);

  const review = async (approved: boolean) => {
    if (!current) return;
    const trimmed = note.trim();
    if (!approved && !trimmed) return setNoteError('Vui lòng ghi lý do từ chối.');
    setNoteError(null);
    setError(null);
    setSaving(approved ? 'approve' : 'reject');
    try {
      const { data, message } = await requestApi.review(current.id, approved, trimmed || null);
      setCurrent(data);
      toast.success(message);
      onReviewed();
    } catch (err) {
      // Lỗi thường gặp: quỹ phép không đủ, lịch vừa thay đổi → hiện ngay trong ngăn để người duyệt đọc kỹ
      setError(getErrorMessage(err));
    } finally {
      setSaving(null);
    }
  };

  const r = current;
  return (
    <>
      <Drawer isOpen={open} onOpenChange={(isOpen) => !isOpen && onClose()} label="Chi tiết đơn" width={600} purpose="form">
        <Layout
          header={
            <DrawerHeader
              title="Chi tiết đơn"
              subtitle={r ? `${r.leaveTypeName} · gửi ${formatDateTime(r.createdAt)}` : undefined}
              onOpenChange={(isOpen) => !isOpen && onClose()}
            />
          }
          content={
            <LayoutContent>
              {r && (
                <VStack gap={4}>
                  <HStack gap={3} vAlign="center" hAlign="between" wrap="wrap">
                    <HStack gap={3} vAlign="center">
                      <Avatar name={r.employee.fullName} url={r.employee.avatarUrl} size={48} />
                      <VStack gap={0.5}>
                        <Heading level={3}>{r.employee.fullName}</Heading>
                        <Text type="supporting">
                          <span className="font-mono">{r.employee.employeeCode}</span>
                          {r.branchName && <> · {r.branchName}</>}
                        </Text>
                      </VStack>
                    </HStack>
                    <HStack gap={1.5}>
                      <Badge tone={categoryTone[r.category]}>{categoryLabels[r.category]}</Badge>
                      <Badge tone={statusTone[r.status]}>{statusLabels[r.status]}</Badge>
                    </HStack>
                  </HStack>

                  <Notice status="error" title={error ? 'Chưa duyệt được đơn' : null} description={error} />

                  <MetadataList label={{ position: 'start', width: 140 }}>
                    {r.category === 'leave' && (
                      <>
                        <MetadataListItem label="Thời gian nghỉ">
                          {r.startDate === r.endDate ? formatDate(r.startDate) : `${formatDate(r.startDate)} → ${formatDate(r.endDate)}`}
                        </MetadataListItem>
                        <MetadataListItem label="Số ngày">{days(r.durationDays)} ngày</MetadataListItem>
                        <MetadataListItem label="Tính lương">{r.isPaid ? 'Có hưởng lương' : 'Không hưởng lương'}</MetadataListItem>
                        {r.deductsBalance && <MetadataListItem label="Quỹ phép">Trừ {days(r.durationDays)} ngày phép năm khi duyệt</MetadataListItem>}
                      </>
                    )}
                    {r.category === 'overtime' && (
                      <>
                        <MetadataListItem label="Ngày">{formatDate(r.startAt)}</MetadataListItem>
                        <MetadataListItem label="Giờ">
                          {timeOf(r.startAt)} – {timeOf(r.endAt)}
                          {r.endAt?.slice(0, 10) !== r.startAt?.slice(0, 10) && ' (hôm sau)'}
                        </MetadataListItem>
                        <MetadataListItem label="Số giờ OT">{hours(r.durationHours ?? 0)} giờ</MetadataListItem>
                      </>
                    )}
                    {r.category === 'shift_swap' && (
                      <>
                        <MetadataListItem label="Ca nhường đi">{scheduleLabel(r.sourceSchedule)}</MetadataListItem>
                        <MetadataListItem label={r.targetSchedule ? 'Đổi với' : 'Người làm thay'}>
                          {r.targetUser ? `${r.targetUser.fullName} (${r.targetUser.employeeCode})` : '—'}
                        </MetadataListItem>
                        {r.targetSchedule && <MetadataListItem label="Ca nhận lại">{scheduleLabel(r.targetSchedule)}</MetadataListItem>}
                      </>
                    )}
                    {r.category === 'explanation' && r.attendanceLog && (
                      <>
                        <MetadataListItem label="Ca được giải trình">
                          {r.attendanceLog.shiftName ?? '—'} · {formatDate(r.attendanceLog.workDate)}
                        </MetadataListItem>
                        <MetadataListItem label="Giờ vào / ra">
                          {timeOf(r.attendanceLog.checkinTime)} / {timeOf(r.attendanceLog.checkoutTime)}
                        </MetadataListItem>
                        {r.attendanceLog.lateMinutes > 0 && <MetadataListItem label="Đi muộn">{r.attendanceLog.lateMinutes} phút</MetadataListItem>}
                        {r.attendanceLog.earlyMinutes > 0 && <MetadataListItem label="Về sớm">{r.attendanceLog.earlyMinutes} phút</MetadataListItem>}
                      </>
                    )}
                  </MetadataList>

                  <Card variant="muted" padding={4}>
                    <VStack gap={1}>
                      <Text weight="medium" size="sm">
                        Lý do
                      </Text>
                      <Text as="p">{r.reason}</Text>
                    </VStack>
                  </Card>

                  {r.attachmentUrl && (
                    <VStack gap={1.5}>
                      <Text weight="medium" size="sm">
                        Ảnh minh chứng
                      </Text>
                      <Thumbnail
                        src={r.attachmentUrl}
                        alt="Ảnh minh chứng"
                        label="Xem ảnh lớn"
                        onClick={() => setPhotoOpen(true)}
                        style={{ width: 120, height: 120 }}
                      />
                    </VStack>
                  )}

                  <VStack gap={3}>
                    <Text weight="medium" size="sm">
                      Tiến trình
                    </Text>
                    <Step state="done" title="Nhân viên gửi đơn" detail={formatDateTime(r.createdAt)} />
                    {r.category === 'shift_swap' && (
                      <Step
                        state={r.status === 'peer_declined' ? 'failed' : r.peerRespondedAt ? 'done' : 'waiting'}
                        title={
                          r.status === 'peer_declined'
                            ? `${r.targetUser?.fullName} từ chối đổi ca`
                            : r.peerRespondedAt
                              ? `${r.targetUser?.fullName} đã đồng ý`
                              : `Chờ ${r.targetUser?.fullName} đồng ý`
                        }
                        detail={r.peerRespondedAt && `${formatDateTime(r.peerRespondedAt)}${r.peerNote ? ` · “${r.peerNote}”` : ''}`}
                      />
                    )}
                    {r.status !== 'peer_declined' && (
                      <Step
                        state={r.status === 'approved' ? 'done' : r.status === 'rejected' ? 'failed' : 'waiting'}
                        title={
                          r.status === 'approved'
                            ? `${r.approverName ?? 'Người duyệt'} đã duyệt`
                            : r.status === 'rejected'
                              ? `${r.approverName ?? 'Người duyệt'} đã từ chối`
                              : r.status === 'cancelled'
                                ? 'Nhân viên đã rút đơn'
                                : 'Chờ Quản lý / HR duyệt'
                        }
                        detail={r.approvedAt && `${formatDateTime(r.approvedAt)}${r.approverNote ? ` · “${r.approverNote}”` : ''}`}
                      />
                    )}
                  </VStack>
                </VStack>
              )}
            </LayoutContent>
          }
          footer={
            r?.canReview && (
              <LayoutFooter hasDivider>
                {/* Ghi chú + nút duyệt nằm ở chân cố định: luôn thấy lỗi "thiếu lý do từ chối" dù đang cuộn tới đâu */}
                <VStack gap={3}>
                  <TextArea
                    label="Ghi chú duyệt"
                    labelTooltip="Bắt buộc khi từ chối. Nhân viên thấy ghi chú này trên ứng dụng."
                    placeholder="Bắt buộc khi từ chối. VD: Tháng này đã đủ người nghỉ, mời bạn chọn ngày khác."
                    rows={2}
                    maxLength={255}
                    value={note}
                    onChange={(v) => {
                      setNote(v);
                      if (noteError && v.trim()) setNoteError(null);
                    }}
                    status={noteError ? { type: 'error', message: noteError } : undefined}
                  />
                  <HStack gap={2} hAlign="end">
                    <Button
                      label="Từ chối"
                      variant="destructive"
                      onClick={() => review(false)}
                      isLoading={saving === 'reject'}
                      isDisabled={saving === 'approve'}
                    />
                    <Button
                      label="Duyệt đơn"
                      variant="primary"
                      onClick={() => review(true)}
                      isLoading={saving === 'approve'}
                      isDisabled={saving === 'reject'}
                    />
                  </HStack>
                </VStack>
              </LayoutFooter>
            )
          }
        />
      </Drawer>
      {r?.attachmentUrl && (
        <Lightbox isOpen={photoOpen} onOpenChange={setPhotoOpen} media={{ src: r.attachmentUrl, alt: 'Ảnh minh chứng' }} hasZoom />
      )}
    </>
  );
}
