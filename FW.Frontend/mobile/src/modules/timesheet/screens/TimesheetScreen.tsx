import { useCallback, useRef, useState } from 'react';
import { ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { MonthSwitcher, type YearMonth } from '@/components/ui/MonthSwitcher';
import { timesheetApi } from '@/services/timesheetApi';
import { getErrorMessage } from '@/services/apiClient';
import { formatCurrency, formatHours, formatTime } from '@/lib/format';
import { formatDayMonth, todayIso, weekdayIndex, weekdayLabels } from '@/lib/dates';
import { colors, radius } from '@/theme';
import type { Timesheet, TimesheetDetail } from '@/types/timesheet';
import { formatDays, outcomeBadges, statusLabels } from '../timesheetUi';

function currentMonth(): YearMonth {
  const [year, month] = todayIso().split('-').map(Number);
  return { year, month };
}

/** Bảng công cá nhân theo tháng (UC 4.x): số liệu tổng hợp + chi tiết từng ca. */
export function TimesheetScreen() {
  const now = useRef(currentMonth()).current;
  const [period, setPeriod] = useState<YearMonth>(now);
  const [sheet, setSheet] = useState<Timesheet | null>(null);
  const [detail, setDetail] = useState<TimesheetDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const seq = useRef(0);

  const load = useCallback(async (target: YearMonth, refresh = false) => {
    const mySeq = ++seq.current;
    if (refresh) setRefreshing(true);
    else setLoading(true);
    try {
      const { data } = await timesheetApi.mine(target.year, target.month);
      const lines = data.id != null ? (await timesheetApi.detail(data.id)).data : null;
      if (mySeq !== seq.current) return;
      setSheet(data);
      setDetail(lines);
      setError(null);
    } catch (err) {
      if (mySeq !== seq.current) return;
      setError(getErrorMessage(err));
      setSheet(null);
      setDetail(null);
    } finally {
      if (mySeq === seq.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void load(period);
    }, [load, period]),
  );

  const f = sheet?.figures;

  return (
    <ScrollView
      style={styles.flex}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(period, true)} />}
    >
      <MonthSwitcher value={period} onChange={setPeriod} max={now} />
      <Notice tone="danger" message={error} />

      {loading && !sheet ? (
        <ActivityIndicator color={colors.primary} style={styles.loading} />
      ) : sheet && f ? (
        <>
          <View style={styles.hero} testID="timesheet-hero">
            <View style={styles.heroTop}>
              <Text style={styles.heroLabel}>Ngày công thực tế</Text>
              <Badge label={sheet.isPreview ? 'Tạm tính' : statusLabels[sheet.status]} tone={sheet.isPreview ? 'warning' : sheet.status === 'finalized' ? 'success' : 'info'} />
            </View>
            <Text style={styles.heroValue}>
              {formatDays(f.actualWorkDays)} <Text style={styles.heroUnit}>/ {formatDays(sheet.standardWorkDays)} ngày chuẩn</Text>
            </Text>
            <Text style={styles.heroHint}>
              {formatHours(f.actualWorkHours)} làm việc · {f.workedShifts}/{f.scheduledShifts} ca
            </Text>
          </View>

          {sheet.isPreview && <Notice tone="info" message="Số liệu tạm tính từ dữ liệu chấm công hiện tại; bảng công chính thức sẽ được tổng hợp khi chốt kỳ." />}

          <View style={styles.grid}>
            <Stat label="Đi muộn" value={`${f.totalLateMinutes} phút`} tone={f.totalLateMinutes > 0 ? 'warning' : undefined} />
            <Stat label="Về sớm" value={`${f.totalEarlyMinutes} phút`} tone={f.totalEarlyMinutes > 0 ? 'warning' : undefined} />
            <Stat label="Tăng ca" value={formatHours(f.totalOvertimeHours)} />
            <Stat label="Ca vắng" value={String(f.absentShifts)} tone={f.absentShifts > 0 ? 'danger' : undefined} />
            <Stat label="Nghỉ có lương" value={`${formatDays(f.paidLeaveDays)} ngày`} />
            <Stat label="Nghỉ không lương" value={`${formatDays(f.unpaidLeaveDays)} ngày`} />
            <Stat label="Vi phạm" value={String(f.violationCount)} tone={f.violationCount > 0 ? 'danger' : undefined} />
            <Stat label="Tiền phạt" value={formatCurrency(f.penaltyAmount)} tone={f.penaltyAmount > 0 ? 'danger' : undefined} />
          </View>

          {(f.pendingLogs > 0 || f.missingCheckouts > 0 || f.rejectedLogs > 0) && (
            <Notice
              tone="warning"
              message={[
                f.pendingLogs > 0 ? `${f.pendingLogs} lượt chấm công đang chờ duyệt` : null,
                f.missingCheckouts > 0 ? `${f.missingCheckouts} ca thiếu check-out (nhờ quản lý chỉnh giờ)` : null,
                f.rejectedLogs > 0 ? `${f.rejectedLogs} lượt bị từ chối` : null,
              ]
                .filter(Boolean)
                .join(' · ')}
            />
          )}

          <Text style={styles.section}>Chi tiết từng ca</Text>
          {detail === null ? (
            <Card style={styles.empty}>
              <Ionicons name="time-outline" size={26} color={colors.muted} />
              <Text style={styles.emptyText}>Chưa có bảng công đã lưu cho tháng này.</Text>
            </Card>
          ) : detail.lines.length === 0 ? (
            <Card style={styles.empty}>
              <Ionicons name="calendar-outline" size={26} color={colors.muted} />
              <Text style={styles.emptyText}>Tháng này bạn không có ca làm việc.</Text>
            </Card>
          ) : (
            detail.lines.map((line) => {
              const badge = outcomeBadges[line.outcome];
              return (
                <Card key={line.scheduleId} style={styles.line}>
                  <View style={styles.lineTop}>
                    <View style={styles.flex}>
                      <Text style={styles.lineDate}>
                        {weekdayLabels[weekdayIndex(line.workDate)]}, {formatDayMonth(line.workDate)}
                      </Text>
                      <Text style={styles.lineShift}>
                        {line.shiftName} · {line.startTime.slice(0, 5)}–{line.endTime.slice(0, 5)}
                      </Text>
                    </View>
                    <Badge label={badge.label} tone={badge.tone} />
                  </View>
                  {(line.checkIn || line.checkOut) && (
                    <Text style={styles.lineMeta}>
                      Vào {formatTime(line.checkIn)} · Ra {formatTime(line.checkOut)}
                      {line.workHours > 0 ? ` · ${formatHours(line.workHours)}` : ''}
                    </Text>
                  )}
                  {(line.lateMinutes > 0 || line.earlyMinutes > 0 || line.penalty > 0) && (
                    <Text style={[styles.lineMeta, styles.lineWarn]}>
                      {[
                        line.lateMinutes > 0 ? `Muộn ${line.lateMinutes} phút` : null,
                        line.earlyMinutes > 0 ? `Về sớm ${line.earlyMinutes} phút` : null,
                        line.penalty > 0 ? `Phạt ${formatCurrency(line.penalty)}${line.exempted ? ' (đã miễn)' : ''}` : null,
                      ]
                        .filter(Boolean)
                        .join(' · ')}
                    </Text>
                  )}
                </Card>
              );
            })
          )}

          {detail && detail.overtime.length > 0 && (
            <>
              <Text style={styles.section}>Tăng ca</Text>
              {detail.overtime.map((o, i) => (
                <Card key={`${o.startAt}-${i}`} style={styles.line}>
                  <Text style={styles.lineDate}>
                    {formatDayMonth(o.startAt)} · {formatTime(o.startAt)}–{formatTime(o.endAt)}
                  </Text>
                  <Text style={styles.lineMeta}>{formatHours(o.hours)}</Text>
                </Card>
              ))}
            </>
          )}
        </>
      ) : null}
    </ScrollView>
  );
}

function Stat({ label, value, tone }: { label: string; value: string; tone?: 'warning' | 'danger' }) {
  return (
    <View style={styles.stat}>
      <Text style={styles.statLabel}>{label}</Text>
      <Text style={[styles.statValue, tone === 'warning' && { color: colors.warning }, tone === 'danger' && { color: colors.danger }]}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 32, gap: 12 },
  loading: { marginTop: 32 },
  hero: { backgroundColor: colors.header, borderRadius: radius.lg, padding: 18, gap: 4 },
  heroTop: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  heroLabel: { fontSize: 13, color: 'rgba(255,255,255,0.7)' },
  heroValue: { fontSize: 30, fontWeight: '800', color: colors.white },
  heroUnit: { fontSize: 14, fontWeight: '600', color: 'rgba(255,255,255,0.7)' },
  heroHint: { fontSize: 13, color: 'rgba(255,255,255,0.7)' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  stat: {
    width: '48%',
    flexGrow: 1,
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 12,
    gap: 2,
  },
  statLabel: { fontSize: 12, color: colors.muted },
  statValue: { fontSize: 16, fontWeight: '700', color: colors.foreground },
  section: { marginTop: 8, marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  line: { gap: 4 },
  lineTop: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  lineDate: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  lineShift: { marginTop: 2, fontSize: 13, color: colors.muted },
  lineMeta: { fontSize: 13, color: colors.muted },
  lineWarn: { color: colors.warning },
  empty: { alignItems: 'center', gap: 8, paddingVertical: 24 },
  emptyText: { fontSize: 14, color: colors.muted, textAlign: 'center' },
});
