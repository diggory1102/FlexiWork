import { useEffect, useState, type ReactNode } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Lightbox } from '@astryxdesign/core/Lightbox';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextArea } from '@astryxdesign/core/TextArea';
import { Thumbnail } from '@astryxdesign/core/Thumbnail';
import { CheckCircle2, MapPinned, MinusCircle, XCircle } from 'lucide-react';
import { DetailDrawer } from '@/components/ui/DetailDrawer';
import { Avatar } from '@/components/ui/Layout';
import { Badge, FormError, Notice, Spinner } from '@/components/ui/Feedback';
import { attendanceApi } from '@/services/attendanceApi';
import { AdjustCheckoutModal } from './AdjustCheckoutModal';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { formatDate, formatDateTime } from '@/lib/format';
import { formatMinutes } from '@/lib/dates';
import type { AttendanceEvidence, AttendanceLogDetail, AttendanceLogItem } from '@/types/attendance';
import {
  attendanceStatusLabels,
  attendanceStatusTone,
  formatDistance,
  formatHours,
  formatScore,
  timeOf,
  wifiState,
  wifiText,
  type LayerState,
} from '../attendanceUi';

const layerIcon: Record<LayerState, { icon: typeof CheckCircle2; color: 'success' | 'error' | 'secondary' }> = {
  pass: { icon: CheckCircle2, color: 'success' },
  fail: { icon: XCircle, color: 'error' },
  skip: { icon: MinusCircle, color: 'secondary' },
};

/** Một lớp xác minh: biểu tượng đạt / không đạt + tên lớp + chi tiết. */
function LayerRow({ state, title, action, children }: { state: LayerState; title: string; action?: ReactNode; children: ReactNode }) {
  const { icon, color } = layerIcon[state];
  return (
    <HStack gap={2} vAlign="start">
      <Icon icon={icon} color={color} size="sm" />
      <VStack gap={0.5} className="min-w-0 flex-1">
        <HStack gap={1} vAlign="center" hAlign="between">
          <Text weight="medium" size="sm">
            {title}
          </Text>
          {action}
        </HStack>
        <Text type="supporting" size="sm" as="div">
          {children}
        </Text>
      </VStack>
    </HStack>
  );
}

/** Minh chứng của lượt vào / ra: ảnh selfie + 3 lớp xác minh + thiết bị. */
function EvidenceCard({
  title,
  evidence,
  radius,
  requiresWifi,
  workDate,
  onOpenPhoto,
}: {
  title: string;
  evidence: AttendanceEvidence | null;
  radius: number;
  requiresWifi: boolean;
  workDate: string;
  onOpenPhoto: () => void;
}) {
  if (!evidence) {
    return (
      <Card variant="muted" padding={4}>
        <VStack gap={1}>
          <Heading level={4}>{title}</Heading>
          <Text type="supporting">Chưa check-out.</Text>
        </VStack>
      </Card>
    );
  }
  const hasPoint = evidence.latitude != null && evidence.longitude != null;
  const inside = evidence.distanceMeters != null && evidence.distanceMeters <= radius;
  return (
    <Card variant="muted" padding={4}>
      <VStack gap={3}>
        <HStack gap={3} vAlign="center">
          {evidence.imageUrl && (
            <Thumbnail
              src={evidence.imageUrl}
              alt={`Ảnh selfie ${title.toLowerCase()}`}
              label="Xem ảnh lớn"
              onClick={onOpenPhoto}
              style={{ width: 72, height: 72 }}
            />
          )}
          <VStack gap={0.5}>
            <Heading level={4}>{title}</Heading>
            <Text size="lg" weight="semibold">
              {timeOf(evidence.time, workDate)}
            </Text>
          </VStack>
        </HStack>
        <LayerRow
          state={inside ? 'pass' : 'fail'}
          title="Lớp 1 · GPS"
          action={
            hasPoint && (
              <IconButton
                variant="ghost"
                size="sm"
                label="Xem vị trí trên Google Maps"
                tooltip="Xem vị trí trên Google Maps"
                icon={<Icon icon={MapPinned} />}
                href={`https://www.google.com/maps?q=${evidence.latitude},${evidence.longitude}`}
                target="_blank"
              />
            )
          }
        >
          Cách tâm {formatDistance(evidence.distanceMeters)} / bán kính {formatDistance(radius)}
          {evidence.accuracyMeters != null && <> · sai số ±{formatDistance(evidence.accuracyMeters)}</>}
        </LayerRow>
        <LayerRow state={wifiState(evidence.wifiMatched, requiresWifi)} title="Lớp 2 · Wi-Fi">
          {wifiText(evidence.wifiMatched, evidence.bssid, requiresWifi)}
          {evidence.bssid && (
            <>
              {' · '}
              <span className="font-mono">{evidence.bssid}</span>
            </>
          )}
        </LayerRow>
        <LayerRow state={evidence.faceMatched ? 'pass' : 'fail'} title="Lớp 3 · Khuôn mặt">
          {evidence.faceMatched ? 'Khớp' : 'Không khớp'} {formatScore(evidence.faceScore)} với ảnh đã đăng ký
        </LayerRow>
        {evidence.deviceId && (
          <Text type="supporting" size="sm" maxLines={1}>
            Thiết bị: <span className="font-mono">{evidence.deviceId}</span>
          </Text>
        )}
      </VStack>
    </Card>
  );
}

/** Ghi chú cảnh báo lưu dạng nhiều câu nối nhau → tách từng câu. */
const splitNote = (note: string) => note.split(/(?<=[.!?…])\s+/).filter(Boolean);

interface Props {
  /** Dòng đang xem (giữ lại khi đóng để ngăn kéo không trống lúc trượt ra). */
  row: AttendanceLogItem | null;
  open: boolean;
  onClose: () => void;
  onReviewed: () => void;
}

/**
 * Ngăn chi tiết một lượt chấm công (UC 4.5, 4.6): minh chứng 3 lớp của lượt vào / ra kèm ảnh selfie, cảnh báo, thông tin
 * duyệt. Lượt chờ duyệt (và người xem có quyền) thì chấp nhận / từ chối ngay tại đây — từ chối phải ghi lý do.
 */
export function AttendanceDetailDrawer({ row, open, onClose, onReviewed }: Props) {
  const [detail, setDetail] = useState<AttendanceLogDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [note, setNote] = useState('');
  const [noteError, setNoteError] = useState<string | null>(null);
  const [saving, setSaving] = useState<'approve' | 'reject' | null>(null);
  const [photoIndex, setPhotoIndex] = useState<number | null>(null);
  const [adjusting, setAdjusting] = useState(false);

  const rowId = row?.id;
  useEffect(() => {
    if (!open || rowId == null) return;
    let cancelled = false;
    setDetail(null);
    setLoadError(null);
    setNote('');
    setNoteError(null);
    setLoading(true);
    attendanceApi
      .get(rowId)
      .then(({ data }) => !cancelled && setDetail(data))
      .catch((err) => !cancelled && setLoadError(getErrorMessage(err)))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [open, rowId]);

  const review = async (approved: boolean) => {
    if (!detail) return;
    const trimmed = note.trim();
    if (!approved && !trimmed) return setNoteError('Vui lòng ghi lý do từ chối.');
    setNoteError(null);
    setSaving(approved ? 'approve' : 'reject');
    try {
      const { data, message } = await attendanceApi.review(detail.log.id, approved, trimmed || null);
      setDetail(data);
      toast.success(message);
      onReviewed();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(null);
    }
  };

  const log = detail?.log ?? row;
  const photos = detail
    ? [
        {
          src: detail.checkIn.imageUrl,
          alt: 'Ảnh selfie lượt vào',
          caption: `Lượt vào · ${timeOf(detail.checkIn.time, detail.log.workDate)}`,
        },
        detail.checkOut && {
          src: detail.checkOut.imageUrl,
          alt: 'Ảnh selfie lượt ra',
          caption: `Lượt ra · ${timeOf(detail.checkOut.time, detail.log.workDate)}`,
        },
      ].filter((p): p is { src: string; alt: string; caption: string } => !!p && !!p.src)
    : [];
  const openPhoto = (which: 'in' | 'out') => setPhotoIndex(which === 'in' || photos.length === 1 ? 0 : 1);

  return (
    <>
      <DetailDrawer
        open={open}
        onClose={onClose}
        label="Chi tiết chấm công"
        title="Chi tiết chấm công"
        subtitle={log ? `${log.branchName} · ngày công ${formatDate(log.workDate)}` : undefined}
        width={640}
        purpose="form"
        footer={
            detail?.canReview && (
              <>
                {/* Ghi chú + nút duyệt nằm ở chân cố định: luôn thấy lỗi "thiếu lý do từ chối" dù đang cuộn tới đâu */}
                <VStack gap={3}>
                  <TextArea
                    label="Ghi chú duyệt"
                    labelTooltip="Bắt buộc khi từ chối. Lượt bị từ chối không được tính công."
                    placeholder="Bắt buộc khi từ chối. VD: Đã xác nhận với ca trưởng, nhân viên có mặt đúng giờ."
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
                      label="Chấp nhận"
                      variant="primary"
                      onClick={() => review(true)}
                      isLoading={saving === 'approve'}
                      isDisabled={saving === 'reject'}
                    />
                  </HStack>
                </VStack>
              </>
            )
        }
      >
              {log && (
                <VStack gap={4}>
                  <HStack gap={3} vAlign="center" hAlign="between" wrap="wrap">
                    <HStack gap={3} vAlign="center">
                      <Avatar name={log.fullName} url={log.avatarUrl} size={48} />
                      <VStack gap={0.5}>
                        <Heading level={3}>{log.fullName}</Heading>
                        <Text type="supporting">
                          <span className="font-mono">{log.employeeCode}</span>
                          {log.shiftName && (
                            <>
                              {' · '}
                              {log.shiftName} {log.shiftStartTime}–{log.shiftEndTime}
                            </>
                          )}
                        </Text>
                      </VStack>
                    </HStack>
                    <Badge tone={attendanceStatusTone[log.status]}>{attendanceStatusLabels[log.status]}</Badge>
                  </HStack>

                  {loading && (
                    <HStack hAlign="center" padding={6}>
                      <Spinner />
                    </HStack>
                  )}
                  <FormError message={loadError} />

                  {detail && (
                    <>
                      {detail.log.note && (
                        <Notice
                          status={detail.log.status === 'pending_approval' ? 'warning' : 'info'}
                          title="Cảnh báo khi chấm công"
                          description={
                            <VStack gap={1}>
                              {splitNote(detail.log.note).map((line) => (
                                <Text key={line} size="sm">
                                  • {line}
                                </Text>
                              ))}
                            </VStack>
                          }
                        />
                      )}

                      <MetadataList label={{ position: 'start', width: 120 }}>
                        <MetadataListItem label="Giờ làm">{formatHours(detail.log.actualWorkHours)}</MetadataListItem>
                        <MetadataListItem label="Đi muộn">
                          {detail.log.lateMinutes > 0 ? formatMinutes(detail.log.lateMinutes) : 'Không'}
                        </MetadataListItem>
                        <MetadataListItem label="Về sớm">
                          {detail.log.earlyMinutes > 0 ? formatMinutes(detail.log.earlyMinutes) : 'Không'}
                        </MetadataListItem>
                      </MetadataList>

                      {detail.canAdjustCheckout && (
                        <Notice
                          status="warning"
                          title="Chưa có giờ ra"
                          description={
                            <VStack gap={2}>
                              <Text size="sm">Nhân viên chưa check-out nên ca này chưa được tính công. Bạn có thể nhập giờ ra thay họ (ghi lý do, hệ thống tính lại giờ làm).</Text>
                              <HStack>
                                <Button label="Điều chỉnh giờ ra" size="sm" variant="secondary" onClick={() => setAdjusting(true)} />
                              </HStack>
                            </VStack>
                          }
                        />
                      )}

                      <Grid columns={{ minWidth: 260, max: 2 }} gap={3}>
                        <EvidenceCard
                          title="Lượt vào"
                          evidence={detail.checkIn}
                          radius={detail.branch.radius}
                          requiresWifi={detail.log.branchRequiresWifi}
                          workDate={detail.log.workDate}
                          onOpenPhoto={() => openPhoto('in')}
                        />
                        {detail.checkoutAdjustedBy != null && detail.checkOut ? (
                          <Card padding={4}>
                            <VStack gap={2}>
                              <Heading level={4}>Lượt ra</Heading>
                              <Text weight="semibold">{timeOf(detail.checkOut.time, detail.log.workDate)} · nhập thủ công</Text>
                              <Text type="supporting" size="sm">
                                {detail.checkoutAdjusterName ?? '—'} ghi giờ ra lúc {formatDateTime(detail.checkoutAdjustedAt)}, không có ảnh hay vị trí minh chứng.
                              </Text>
                              {detail.checkoutAdjustNote && <Text size="sm">Lý do: {detail.checkoutAdjustNote}</Text>}
                            </VStack>
                          </Card>
                        ) : (
                          <EvidenceCard
                            title="Lượt ra"
                            evidence={detail.checkOut}
                            radius={detail.branch.radius}
                            requiresWifi={detail.log.branchRequiresWifi}
                            workDate={detail.log.workDate}
                            onOpenPhoto={() => openPhoto('out')}
                          />
                        )}
                      </Grid>

                      {detail.reviewedAt && (
                        <Notice
                          status={detail.log.status === 'valid' ? 'success' : 'error'}
                          title={`${detail.log.status === 'valid' ? 'Đã chấp nhận' : 'Đã từ chối'} bởi ${detail.reviewerName ?? '—'} lúc ${formatDateTime(detail.reviewedAt)}`}
                          description={detail.reviewNote ?? undefined}
                        />
                      )}
                    </>
                  )}
                </VStack>
              )}
      </DetailDrawer>
      {detail && (
        <AdjustCheckoutModal
          open={adjusting}
          logId={detail.log.id}
          employeeName={detail.log.fullName}
          workDate={detail.log.workDate}
          shiftLabel={`${detail.log.shiftName ?? 'Ca'} ${detail.log.shiftStartTime ?? ''}–${detail.log.shiftEndTime ?? ''}`}
          checkInTime={detail.checkIn.time}
          shiftEnd={detail.log.shiftEndTime}
          onClose={() => setAdjusting(false)}
          onAdjusted={(updated) => {
            setDetail(updated);
            onReviewed();
          }}
        />
      )}
      {photos.length > 0 && (
        <Lightbox
          isOpen={photoIndex != null}
          onOpenChange={(isOpen) => !isOpen && setPhotoIndex(null)}
          media={photos}
          index={photoIndex ?? 0}
          onIndexChange={setPhotoIndex}
          hasZoom
        />
      )}
    </>
  );
}
