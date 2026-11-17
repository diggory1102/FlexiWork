import { useEffect, useState, type ComponentProps } from 'react';
import { ActivityIndicator, Linking, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { useCameraPermissions } from 'expo-camera';
import { Ionicons } from '@expo/vector-icons';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { SelfieCamera } from '@/components/camera/SelfieCamera';
import { confirmAction } from '@/lib/dialog';
import { useUiStore } from '@/stores/uiStore';
import { distanceMeters, formatDistance } from '@/lib/device';
import { formatHours, formatLongDate, formatTime } from '@/lib/format';
import { colors, radius } from '@/theme';
import type { AttendanceCapture, AttendanceResult, AttendanceShift, AttendanceToday } from '@/types/attendance';
import { useAttendance, type Probe } from '../useAttendance';
import { attendanceStatusBadges, parseLocal, shiftRange, wifiStatusLabels } from '../attendanceUi';

type IconName = ComponentProps<typeof Ionicons>['name'];
type Mode = 'overview' | 'locating' | 'camera' | 'submitting';
type Action = 'check_in' | 'check_out';

function useClock() {
  const [now, setNow] = useState(new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 15_000);
    return () => clearInterval(id);
  }, []);
  return now;
}

/**
 * Chấm công 3 lớp (UC 4.1): đọc GPS + Wi-Fi + mã thiết bị → chụp selfie camera trước → máy chủ xác minh và trả kết quả
 * từng lớp. Trước khi chụp, màn hình hiện khoảng cách tới chi nhánh và tình trạng Wi-Fi để nhân viên tự kiểm tra.
 */
export function AttendanceScreen() {
  const vm = useAttendance();
  const now = useClock();
  const [permission, requestPermission] = useCameraPermissions();
  const [mode, setMode] = useState<Mode>('overview');
  const [pending, setPending] = useState<{ action: Action; capture: AttendanceCapture } | null>(null);
  const [result, setResult] = useState<AttendanceResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Camera và màn hình chờ xác minh chiếm toàn màn hình (ẩn tiêu đề + thanh tab)
  const setImmersive = useUiStore((s) => s.setImmersive);
  useEffect(() => {
    setImmersive(mode === 'camera' || mode === 'submitting');
    return () => setImmersive(false);
  }, [mode, setImmersive]);

  const today = vm.today;
  const action = today?.nextAction === 'check_in' || today?.nextAction === 'check_out' ? today.nextAction : null;
  const target = today?.shifts.find((s) => s.scheduleId === today.actionScheduleId) ?? null;

  const start = async (act: Action) => {
    setError(null);
    setResult(null);
    const camera = permission?.granted ? permission : await requestPermission();
    if (!camera.granted) {
      setError('Bạn cần cho phép FlexiWork dùng camera để chụp ảnh chấm công.');
      return;
    }
    setMode('locating');
    const prepared = await vm.prepareCapture();
    if (!prepared.ok) {
      setError(prepared.message);
      setMode('overview');
      return;
    }
    setPending({ action: act, capture: prepared.capture });
    setMode('camera');
  };

  const onPress = () => {
    if (!action) return;
    if (action === 'check_out' && target && new Date() < parseLocal(target.endAt)) {
      confirmAction(
        'Check-out trước giờ kết thúc ca?',
        `Ca kết thúc lúc ${formatTime(target.endAt)}. Check-out bây giờ sẽ bị tính là về sớm và không check-out lại được.`,
        'Check-out',
        () => void start(action),
      );
      return;
    }
    void start(action);
  };

  const onCapture = async (uri: string) => {
    if (!pending) return;
    setMode('submitting');
    const outcome = await vm.submit(pending.action, pending.capture, uri);
    setPending(null);
    setMode('overview');
    if (outcome.ok) setResult(outcome.result);
    else setError(outcome.message);
  };

  if (mode === 'camera') {
    return (
      <SelfieCamera
        hint={pending?.action === 'check_out' ? 'Chụp ảnh để check-out' : 'Chụp ảnh để check-in'}
        onCapture={(uri) => void onCapture(uri)}
        onCancel={() => {
          setPending(null);
          setMode('overview');
        }}
        onError={(message) => {
          setError(message);
          setPending(null);
          setMode('overview');
        }}
      />
    );
  }

  if (mode === 'submitting') {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color={colors.primary} />
        <Text style={styles.busyTitle}>Đang xác minh…</Text>
        <Text style={styles.muted}>Kiểm tra vị trí, Wi-Fi và khuôn mặt của bạn</Text>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={vm.refreshing} onRefresh={vm.refresh} tintColor={colors.primary} />}
    >
      <View style={styles.clockCard}>
        <Text style={styles.date}>{formatLongDate(now)}</Text>
        <Text style={styles.clock}>
          {String(now.getHours()).padStart(2, '0')}:{String(now.getMinutes()).padStart(2, '0')}
        </Text>
      </View>

      {result && <ResultCard result={result} onClose={() => setResult(null)} />}

      {vm.loading ? (
        <ActivityIndicator color={colors.primary} style={styles.loading} />
      ) : vm.error || !today ? (
        <View style={styles.gap}>
          <Notice tone="danger" message={vm.error ?? 'Không tải được thông tin chấm công.'} />
          <Button title="Thử lại" variant="secondary" onPress={vm.retry} />
        </View>
      ) : (
        <>
          {!today.faceRegistered && (
            <Card style={styles.gap}>
              <Notice tone="warning" message="Bạn chưa đăng ký khuôn mặt nên chưa chấm công được. Chỉ cần đăng ký một lần." />
              <Button title="Đăng ký khuôn mặt" variant="secondary" onPress={() => router.push('/face-id')} />
            </Card>
          )}

          {action && target ? (
            <ActionCard
              action={action}
              shift={target}
              today={today}
              probe={vm.probe}
              onReprobe={() => {
                setError(null);
                void vm.reprobe();
              }}
              error={error}
              busy={mode === 'locating'}
              disabled={!today.faceRegistered}
              onPress={onPress}
            />
          ) : (
            <Card style={styles.idleCard}>
              <Ionicons name="time-outline" size={28} color={colors.muted} />
              <Text style={styles.idleText}>{today.message}</Text>
              <Notice tone="danger" message={error} style={styles.stretch} />
            </Card>
          )}

          <Text style={styles.section}>Ca hôm nay</Text>
          {today.shifts.length === 0 ? (
            <Card>
              <Text style={styles.muted}>Hôm nay bạn không có ca nào đã công bố.</Text>
            </Card>
          ) : (
            today.shifts.map((s) => <ShiftRow key={s.scheduleId} shift={s} />)
          )}

          <Pressable onPress={() => router.push('/tasks')} style={({ pressed }) => [styles.historyLink, pressed && styles.pressed]} accessibilityRole="button">
            <Ionicons name="checkbox-outline" size={20} color={colors.primary} />
            <Text style={styles.historyText}>Công việc trong ca</Text>
            <Ionicons name="chevron-forward" size={18} color={colors.muted} />
          </Pressable>
          <Pressable onPress={() => router.push('/attendance-history')} style={({ pressed }) => [styles.historyLink, pressed && styles.pressed]} accessibilityRole="button">
            <Ionicons name="receipt-outline" size={20} color={colors.primary} />
            <Text style={styles.historyText}>Lịch sử chấm công</Text>
            <Ionicons name="chevron-forward" size={18} color={colors.muted} />
          </Pressable>
        </>
      )}
    </ScrollView>
  );
}

// ---------------------------------------------------------------- Thẻ việc cần làm

interface ActionCardProps {
  action: Action;
  shift: AttendanceShift;
  today: AttendanceToday;
  probe: Probe;
  onReprobe: () => void;
  error: string | null;
  busy: boolean;
  disabled: boolean;
  onPress: () => void;
}

function ActionCard({ action, shift, today, probe, onReprobe, error, busy, disabled, onPress }: ActionCardProps) {
  const branch = today.branch;
  const distance = branch && probe.position ? distanceMeters(probe.position.latitude, probe.position.longitude, branch.latitude, branch.longitude) : null;
  const inside = distance !== null && branch !== null && distance <= branch.radius;
  const checkIn = action === 'check_in';

  return (
    <Card style={styles.actionCard}>
      <View style={styles.actionHeader}>
        <View style={styles.flex}>
          <Text style={styles.shiftName}>{shift.shiftName}</Text>
          <Text style={styles.muted}>
            {shiftRange(shift)} · {shift.shiftCode}
          </Text>
        </View>
        <Badge label={checkIn ? 'Chưa vào ca' : 'Đang trong ca'} tone={checkIn ? 'info' : 'success'} />
      </View>
      <Text style={styles.message}>{today.message}</Text>

      {branch && (
        <View style={styles.checks}>
          <CheckRow
            icon="location"
            tone={probe.loading ? 'muted' : probe.error ? 'danger' : inside ? 'success' : distance !== null ? 'danger' : 'muted'}
            title={branch.name}
            detail={
              probe.loading
                ? 'Đang lấy vị trí…'
                : probe.error
                  ? probe.error
                  : distance !== null
                    ? `Cách khoảng ${formatDistance(distance)} · cho phép ${formatDistance(branch.radius)}${probe.position?.accuracy ? ` (sai số GPS ±${formatDistance(probe.position.accuracy)})` : ''}`
                    : 'Chưa có vị trí'
            }
            actionLabel={probe.loading ? undefined : probe.canOpenSettings ? 'Cài đặt' : 'Cập nhật'}
            onAction={probe.canOpenSettings ? () => void Linking.openSettings() : onReprobe}
          />
          <CheckRow
            icon="wifi"
            tone={!branch.requiresWifi ? 'muted' : probe.bssid ? 'success' : 'warning'}
            title="Wi-Fi nội bộ"
            detail={
              !branch.requiresWifi
                ? 'Chi nhánh không yêu cầu Wi-Fi'
                : probe.bssid
                  ? `Đã đọc được Wi-Fi (${probe.bssid}) — máy chủ sẽ đối chiếu`
                  : 'Không đọc được Wi-Fi — lượt chấm công sẽ chờ Quản lý duyệt. Hãy kết nối Wi-Fi của chi nhánh.'
            }
          />
          <CheckRow icon="scan" tone={today.faceRegistered ? 'success' : 'danger'} title="Khuôn mặt" detail={today.faceRegistered ? 'Chụp selfie để xác minh' : 'Chưa đăng ký khuôn mặt'} />
        </View>
      )}

      <Notice tone="danger" message={error} />
      <Button
        title={busy ? 'Đang lấy vị trí…' : checkIn ? 'Check-in' : 'Check-out'}
        onPress={onPress}
        loading={busy}
        disabled={disabled}
        icon={<Ionicons name={checkIn ? 'log-in-outline' : 'log-out-outline'} size={20} color={colors.white} />}
        style={styles.bigButton}
      />
    </Card>
  );
}

const toneColors = { success: colors.success, warning: colors.warning, danger: colors.danger, muted: colors.muted } as const;

function CheckRow(props: {
  icon: IconName;
  tone: keyof typeof toneColors;
  title: string;
  detail: string;
  actionLabel?: string;
  onAction?: () => void;
}) {
  const color = toneColors[props.tone];
  return (
    <View style={styles.checkRow}>
      <View style={[styles.checkIcon, { backgroundColor: `${color}1A` }]}>
        <Ionicons name={props.icon} size={18} color={color} />
      </View>
      <View style={styles.flex}>
        <Text style={styles.checkTitle}>{props.title}</Text>
        <Text style={styles.checkDetail}>{props.detail}</Text>
      </View>
      {props.actionLabel && props.onAction && (
        <Pressable onPress={props.onAction} hitSlop={8} accessibilityRole="button">
          <Text style={styles.link}>{props.actionLabel}</Text>
        </Pressable>
      )}
    </View>
  );
}

// ---------------------------------------------------------------- Kết quả

function ResultCard({ result, onClose }: { result: AttendanceResult; onClose: () => void }) {
  const valid = result.status === 'valid';
  const badge = attendanceStatusBadges[result.status];
  return (
    <Card style={[styles.resultCard, { borderColor: valid ? colors.success : colors.warning }]}>
      <View style={styles.resultHeader}>
        <Ionicons name={valid ? 'checkmark-circle' : 'hourglass-outline'} size={28} color={valid ? colors.success : colors.warning} />
        <View style={styles.flex}>
          <Text style={styles.resultTitle}>
            {result.action === 'check_in' ? 'Check-in' : 'Check-out'} lúc {formatTime(result.time)}
          </Text>
          <Text style={styles.muted}>{result.shiftName}</Text>
        </View>
        <Badge label={badge.label} tone={badge.tone} />
      </View>
      <Text style={styles.message}>{result.message}</Text>

      <View style={styles.checks}>
        <CheckRow
          icon="location"
          tone="success"
          title="Vị trí GPS"
          detail={`Cách chi nhánh ${formatDistance(result.gps.distanceMeters)} (cho phép ${formatDistance(result.gps.radiusMeters)})`}
        />
        <CheckRow
          icon="wifi"
          tone={result.wifi.status === 'matched' || result.wifi.status === 'not_required' ? 'success' : 'warning'}
          title="Wi-Fi nội bộ"
          detail={wifiStatusLabels[result.wifi.status] + (result.wifi.bssid ? ` · ${result.wifi.bssid}` : '')}
        />
        <CheckRow icon="scan" tone="success" title="Khuôn mặt" detail={`Khớp ${Math.round(result.face.score * 100)}% (ngưỡng ${Math.round(result.face.threshold * 100)}%)`} />
      </View>

      {result.action === 'check_out' && <Text style={styles.muted}>Giờ làm được tính: {formatHours(result.actualWorkHours)}</Text>}
      {result.warnings.map((w) => (
        <Notice key={w} tone="warning" message={w} />
      ))}
      {result.action === 'check_in' && (
        <Button
          title="Xem công việc trong ca"
          icon={<Ionicons name="checkbox-outline" size={18} color={colors.white} />}
          onPress={() => {
            onClose();
            router.push('/tasks');
          }}
        />
      )}
      <Button title="Đóng" variant="secondary" onPress={onClose} />
    </Card>
  );
}

// ---------------------------------------------------------------- Ca trong ngày

function ShiftRow({ shift }: { shift: AttendanceShift }) {
  const log = shift.log;
  const badge = log ? attendanceStatusBadges[log.status] : null;
  return (
    <Card style={styles.shiftRow}>
      <View style={styles.actionHeader}>
        <View style={styles.flex}>
          <Text style={styles.rowTitle}>{shift.shiftName}</Text>
          <Text style={styles.muted}>{shiftRange(shift)}</Text>
        </View>
        {badge ? <Badge label={badge.label} tone={badge.tone} /> : <Badge label="Chưa chấm công" />}
      </View>
      {log && (
        <View style={styles.times}>
          <TimeBox label="Vào" value={formatTime(log.checkinTime)} extra={log.lateMinutes > 0 ? `muộn ${log.lateMinutes}'` : undefined} />
          <TimeBox label="Ra" value={formatTime(log.checkoutTime)} extra={log.earlyMinutes > 0 ? `sớm ${log.earlyMinutes}'` : undefined} />
          <TimeBox label="Giờ làm" value={log.checkoutTime ? formatHours(log.actualWorkHours) : '—'} />
        </View>
      )}
      {log?.note && <Text style={styles.note}>{log.note}</Text>}
    </Card>
  );
}

function TimeBox({ label, value, extra }: { label: string; value: string; extra?: string }) {
  return (
    <View style={styles.timeBox}>
      <Text style={styles.timeLabel}>{label}</Text>
      <Text style={styles.timeValue}>{value}</Text>
      {extra && <Text style={styles.timeExtra}>{extra}</Text>}
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background },
  content: { padding: 16, paddingBottom: 32, gap: 12 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 8, padding: 24, backgroundColor: colors.background },
  flex: { flex: 1 },
  gap: { gap: 12 },
  stretch: { alignSelf: 'stretch' },
  loading: { marginTop: 40 },
  muted: { fontSize: 13, lineHeight: 18, color: colors.muted },
  busyTitle: { marginTop: 8, fontSize: 18, fontWeight: '700', color: colors.foreground },
  clockCard: { backgroundColor: colors.header, borderRadius: radius.lg, paddingVertical: 18, alignItems: 'center' },
  date: { fontSize: 14, color: 'rgba(255,255,255,0.75)' },
  clock: { marginTop: 2, fontSize: 44, fontWeight: '800', color: colors.white, fontVariant: ['tabular-nums'] },
  actionCard: { gap: 14 },
  actionHeader: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  shiftName: { fontSize: 18, fontWeight: '800', color: colors.foreground },
  message: { fontSize: 14, lineHeight: 20, color: colors.foreground },
  checks: { gap: 12, paddingVertical: 4 },
  checkRow: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  checkIcon: { width: 36, height: 36, borderRadius: 18, alignItems: 'center', justifyContent: 'center' },
  checkTitle: { fontSize: 14, fontWeight: '600', color: colors.foreground },
  checkDetail: { marginTop: 1, fontSize: 13, lineHeight: 18, color: colors.muted },
  link: { fontSize: 14, fontWeight: '600', color: colors.primary },
  bigButton: { height: 56 },
  idleCard: { alignItems: 'center', gap: 10, paddingVertical: 24 },
  idleText: { fontSize: 15, lineHeight: 21, color: colors.foreground, textAlign: 'center' },
  resultCard: { gap: 12, borderWidth: 1.5 },
  resultHeader: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  resultTitle: { fontSize: 17, fontWeight: '700', color: colors.foreground },
  section: { marginTop: 8, marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase', letterSpacing: 0.5 },
  shiftRow: { gap: 12, paddingVertical: 12 },
  rowTitle: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  times: { flexDirection: 'row', gap: 8 },
  timeBox: { flex: 1, backgroundColor: colors.background, borderRadius: radius.md, paddingVertical: 8, alignItems: 'center' },
  timeLabel: { fontSize: 12, color: colors.muted },
  timeValue: { marginTop: 2, fontSize: 16, fontWeight: '700', color: colors.foreground },
  timeExtra: { marginTop: 1, fontSize: 12, fontWeight: '600', color: colors.warning },
  note: { fontSize: 13, lineHeight: 18, color: colors.warning },
  historyLink: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    paddingHorizontal: 16,
    paddingVertical: 14,
  },
  historyText: { flex: 1, fontSize: 15, fontWeight: '600', color: colors.foreground },
  pressed: { opacity: 0.85 },
});
