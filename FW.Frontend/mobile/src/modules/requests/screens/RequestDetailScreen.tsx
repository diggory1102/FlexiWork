import { useCallback, useState, type ReactNode } from 'react';
import { ActivityIndicator, Image, Linking, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect, useLocalSearchParams } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { requestApi } from '@/services/requestApi';
import { getErrorMessage } from '@/services/apiClient';
import { confirmAction, notify } from '@/lib/dialog';
import { formatDate, formatTime } from '@/lib/format';
import { colors, radius } from '@/theme';
import type { LeaveRequest } from '@/types/requests';
import { categoryInfo, days, scheduleLabel, statusBadges, summarize } from '../requestUi';

const dateTime = (iso: string | null) => (iso ? `${formatTime(iso)} ${formatDate(iso)}` : '—');

/** Chi tiết đơn: nội dung theo nhóm, ảnh minh chứng, tiến trình xử lý; rút đơn / trả lời đổi ca. */
export function RequestDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [request, setRequest] = useState<LeaveRequest | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setRequest((await requestApi.get(Number(id))).data);
      setError(null);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }, [id]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load]),
  );

  const run = async (action: () => Promise<{ data: LeaveRequest; message: string }>) => {
    setBusy(true);
    try {
      const { data, message } = await action();
      setRequest(data);
      notify(message);
    } catch (err) {
      notify(getErrorMessage(err), 'Không thực hiện được');
    } finally {
      setBusy(false);
    }
  };

  if (!request) {
    return (
      <View style={styles.center}>
        {error ? <Notice tone="danger" message={error} /> : <ActivityIndicator color={colors.primary} />}
      </View>
    );
  }

  const r = request;
  const info = categoryInfo[r.category];
  const badge = statusBadges[r.status];

  return (
    <ScrollView contentContainerStyle={styles.content}>
      <Card style={styles.header}>
        <View style={[styles.icon, { backgroundColor: `${info.tint}1A` }]}>
          <Ionicons name={info.icon} size={24} color={info.tint} />
        </View>
        <View style={styles.flex}>
          <Text style={styles.title}>{r.leaveTypeName}</Text>
          <Text style={styles.muted}>{summarize(r)}</Text>
        </View>
        <Badge label={badge.label} tone={badge.tone} />
      </Card>

      <Card style={styles.block}>
        {r.category === 'leave' && (
          <>
            <Row label="Thời gian" value={r.startDate === r.endDate ? formatDate(r.startDate) : `${formatDate(r.startDate)} → ${formatDate(r.endDate)}`} />
            <Row label="Số ngày" value={`${days(r.durationDays)} ngày${r.deductsBalance ? ' · trừ phép năm' : ''}`} />
            <Row label="Hưởng lương" value={r.isPaid ? 'Có' : 'Không'} />
          </>
        )}
        {r.category === 'overtime' && (
          <>
            <Row label="Ngày" value={formatDate(r.startAt)} />
            <Row label="Giờ" value={`${formatTime(r.startAt)} – ${formatTime(r.endAt)}`} />
            <Row label="Số giờ" value={`${days(r.durationHours ?? 0)} giờ`} />
          </>
        )}
        {r.category === 'shift_swap' && (
          <>
            <Row label="Người đề nghị" value={r.employee.fullName} />
            <Row label="Ca nhường đi" value={scheduleLabel(r.sourceSchedule)} />
            <Row label={r.targetSchedule ? 'Đổi với' : 'Người làm thay'} value={r.targetUser?.fullName ?? '—'} />
            {r.targetSchedule && <Row label="Ca nhận lại" value={scheduleLabel(r.targetSchedule)} />}
          </>
        )}
        {r.category === 'explanation' && r.attendanceLog && (
          <>
            <Row label="Ca" value={`${r.attendanceLog.shiftName ?? '—'} · ${formatDate(r.attendanceLog.workDate)}`} />
            <Row label="Vào / ra" value={`${formatTime(r.attendanceLog.checkinTime)} / ${formatTime(r.attendanceLog.checkoutTime)}`} />
            {r.attendanceLog.lateMinutes > 0 && <Row label="Đi muộn" value={`${r.attendanceLog.lateMinutes} phút`} />}
            {r.attendanceLog.earlyMinutes > 0 && <Row label="Về sớm" value={`${r.attendanceLog.earlyMinutes} phút`} />}
          </>
        )}
        <Row label="Lý do" value={r.reason} multiline />
      </Card>

      {r.attachmentUrl && (
        <Card style={styles.block}>
          <Text style={styles.label}>Ảnh minh chứng</Text>
          <Pressable onPress={() => void Linking.openURL(r.attachmentUrl!)} accessibilityRole="imagebutton" accessibilityLabel="Mở ảnh minh chứng">
            <Image source={{ uri: r.attachmentUrl }} style={styles.photo} resizeMode="cover" />
          </Pressable>
        </Card>
      )}

      <Card style={styles.block}>
        <Text style={styles.label}>Tiến trình</Text>
        <Step done icon="paper-plane-outline" title="Đã gửi đơn" detail={dateTime(r.createdAt)} />
        {r.category === 'shift_swap' && (
          <Step
            done={r.peerRespondedAt != null}
            failed={r.status === 'peer_declined'}
            icon="people-outline"
            title={
              r.status === 'peer_declined'
                ? `${r.targetUser?.fullName} từ chối`
                : r.peerRespondedAt
                  ? `${r.targetUser?.fullName} đã đồng ý`
                  : `Chờ ${r.targetUser?.fullName} đồng ý`
            }
            detail={r.peerRespondedAt ? `${dateTime(r.peerRespondedAt)}${r.peerNote ? ` · “${r.peerNote}”` : ''}` : undefined}
          />
        )}
        {r.status !== 'peer_declined' && (
          <Step
            done={r.status === 'approved' || r.status === 'rejected'}
            failed={r.status === 'rejected'}
            icon="shield-checkmark-outline"
            title={
              r.status === 'approved'
                ? `${r.approverName ?? 'Quản lý'} đã duyệt`
                : r.status === 'rejected'
                  ? `${r.approverName ?? 'Quản lý'} từ chối`
                  : r.status === 'cancelled'
                    ? 'Bạn đã rút đơn'
                    : 'Chờ Quản lý duyệt'
            }
            detail={r.approvedAt ? `${dateTime(r.approvedAt)}${r.approverNote ? ` · “${r.approverNote}”` : ''}` : undefined}
          />
        )}
      </Card>

      {r.canRespond && (
        <View style={styles.actions}>
          <Button
            title="Từ chối"
            variant="danger"
            style={styles.flex}
            disabled={busy}
            onPress={() => confirmAction('Từ chối đổi ca?', `${r.employee.fullName} sẽ được báo bạn từ chối.`, 'Từ chối', () => void run(() => requestApi.respond(r.id, false)), true)}
          />
          <Button
            title="Đồng ý"
            style={styles.flex}
            loading={busy}
            onPress={() => confirmAction('Đồng ý đổi ca?', 'Đơn sẽ được chuyển tới Quản lý duyệt.', 'Đồng ý', () => void run(() => requestApi.respond(r.id, true)))}
          />
        </View>
      )}
      {r.canCancel && (
        <Button
          title="Rút đơn"
          variant="danger"
          loading={busy}
          onPress={() => confirmAction('Rút đơn này?', 'Đơn sẽ không được xử lý nữa.', 'Rút đơn', () => void run(() => requestApi.cancel(r.id)), true)}
        />
      )}
    </ScrollView>
  );
}

function Row({ label, value, multiline }: { label: string; value: string; multiline?: boolean }) {
  return (
    <View style={[styles.row, multiline && styles.rowColumn]}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={[styles.rowValue, multiline && styles.rowValueMultiline]}>{value}</Text>
    </View>
  );
}

function Step({ done, failed, icon, title, detail }: { done: boolean; failed?: boolean; icon: 'paper-plane-outline' | 'people-outline' | 'shield-checkmark-outline'; title: string; detail?: ReactNode }) {
  const tint = failed ? colors.danger : done ? colors.success : colors.muted;
  return (
    <View style={styles.step}>
      <View style={[styles.stepIcon, { borderColor: tint }]}>
        <Ionicons name={icon} size={16} color={tint} />
      </View>
      <View style={styles.flex}>
        <Text style={[styles.stepTitle, { color: done || failed ? colors.foreground : colors.muted }]}>{title}</Text>
        {detail ? <Text style={styles.muted}>{detail}</Text> : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  center: { flex: 1, padding: 16, justifyContent: 'center' },
  content: { padding: 16, paddingBottom: 40, gap: 12 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  icon: { width: 48, height: 48, borderRadius: 14, alignItems: 'center', justifyContent: 'center' },
  title: { fontSize: 17, fontWeight: '700', color: colors.foreground },
  muted: { marginTop: 2, fontSize: 13, lineHeight: 18, color: colors.muted },
  block: { gap: 10 },
  label: { fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: 12 },
  rowColumn: { flexDirection: 'column', gap: 4 },
  rowLabel: { fontSize: 14, color: colors.muted },
  rowValue: { flexShrink: 1, textAlign: 'right', fontSize: 14, fontWeight: '600', color: colors.foreground },
  rowValueMultiline: { textAlign: 'left', fontWeight: '400', lineHeight: 20 },
  photo: { width: '100%', height: 200, borderRadius: radius.md, backgroundColor: colors.background },
  step: { flexDirection: 'row', gap: 12, alignItems: 'flex-start' },
  stepIcon: { width: 30, height: 30, borderRadius: 15, borderWidth: 1.5, alignItems: 'center', justifyContent: 'center' },
  stepTitle: { fontSize: 14, fontWeight: '600' },
  actions: { flexDirection: 'row', gap: 10 },
});
