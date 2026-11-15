import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { scheduleApi } from '@/services/scheduleApi';
import { getErrorMessage } from '@/services/apiClient';
import { addDays, formatDayMonth, startOfWeek, todayIso, weekDays, weekdayLabels } from '@/lib/dates';
import { colors, radius } from '@/theme';
import type { Schedule } from '@/types/scheduling';
import { scheduleStatusBadges, shiftHours } from '../shiftUi';

/** ViewModel: tải lịch đã công bố của tuần đang xem. */
function useWeekSchedule(weekStart: string) {
  const [items, setItems] = useState<Schedule[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (mode: 'initial' | 'refresh' = 'initial') => {
      if (mode === 'refresh') setRefreshing(true);
      else setLoading(true);
      try {
        const { data } = await scheduleApi.mine(weekStart, addDays(weekStart, 6));
        setItems(data);
        setError(null);
      } catch (err) {
        setError(getErrorMessage(err));
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [weekStart],
  );

  useEffect(() => {
    void load();
  }, [load]);

  return { items, loading, refreshing, error, refresh: () => load('refresh'), retry: () => load() };
}

/**
 * Lịch ca cá nhân theo tuần (UC 3.4): chỉ hiện lịch Quản lý đã công bố; ca bị hủy vẫn hiện (gạch ngang) để nhân viên biết.
 */
export function MyScheduleScreen() {
  const today = todayIso();
  const thisWeek = startOfWeek(today);
  const [weekStart, setWeekStart] = useState(thisWeek);
  const days = useMemo(() => weekDays(weekStart), [weekStart]);
  const vm = useWeekSchedule(weekStart);

  const active = vm.items.filter((s) => s.status !== 'cancelled');
  const label = weekStart === thisWeek ? 'Tuần này' : weekStart === addDays(thisWeek, 7) ? 'Tuần sau' : weekStart === addDays(thisWeek, -7) ? 'Tuần trước' : null;

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={vm.refreshing} onRefresh={vm.refresh} tintColor={colors.primary} />}
    >
      <View style={styles.weekBar}>
        <Pressable onPress={() => setWeekStart(addDays(weekStart, -7))} style={styles.navButton} accessibilityRole="button" accessibilityLabel="Tuần trước" hitSlop={8}>
          <Ionicons name="chevron-back" size={20} color={colors.foreground} />
        </Pressable>
        <Pressable onPress={() => setWeekStart(thisWeek)} style={styles.weekInfo} accessibilityRole="button" accessibilityLabel="Về tuần này">
          <Text style={styles.weekLabel}>{label ?? 'Tuần'}</Text>
          <Text style={styles.weekRange}>
            {formatDayMonth(days[0])} – {formatDayMonth(days[6])}
          </Text>
        </Pressable>
        <Pressable onPress={() => setWeekStart(addDays(weekStart, 7))} style={styles.navButton} accessibilityRole="button" accessibilityLabel="Tuần sau" hitSlop={8}>
          <Ionicons name="chevron-forward" size={20} color={colors.foreground} />
        </Pressable>
      </View>

      {vm.loading ? (
        <ActivityIndicator color={colors.primary} style={styles.loading} />
      ) : vm.error ? (
        <View style={styles.gap}>
          <Notice tone="danger" message={vm.error} />
          <Button title="Thử lại" variant="secondary" onPress={vm.retry} />
        </View>
      ) : (
        <>
          <Text style={styles.summary}>
            {active.length === 0 ? 'Chưa có ca nào được công bố trong tuần.' : `${active.length} ca làm việc trong tuần`}
          </Text>
          {days.map((day, i) => (
            <DayCard key={day} day={day} label={weekdayLabels[i]} isToday={day === today} isPast={day < today} items={vm.items.filter((s) => s.workDate === day)} />
          ))}
          <Text style={styles.note}>Lịch chỉ hiện sau khi Quản lý công bố. Muốn đổi ca, hãy gửi đơn đổi ca ở mục Đơn từ.</Text>
        </>
      )}
    </ScrollView>
  );
}

function DayCard({ day, label, isToday, isPast, items }: { day: string; label: string; isToday: boolean; isPast: boolean; items: Schedule[] }) {
  return (
    <Card style={[styles.dayCard, isToday && styles.todayCard, isPast && styles.pastCard]}>
      <View style={styles.dayHeader}>
        <Text style={[styles.dayName, isToday && styles.todayText]}>{label}</Text>
        <Text style={styles.dayDate}>{formatDayMonth(day)}</Text>
        {isToday && <Badge label="Hôm nay" />}
      </View>
      {items.length === 0 ? (
        <Text style={styles.dayOff}>Không có ca</Text>
      ) : (
        items.map((s) => {
          const cancelled = s.status === 'cancelled';
          const badge = scheduleStatusBadges[s.status];
          return (
            <View key={s.id} style={styles.shiftRow}>
              <View style={[styles.shiftBar, cancelled && styles.cancelledBar]} />
              <View style={styles.flex}>
                <Text style={[styles.shiftName, cancelled && styles.cancelledText]}>{s.shiftName}</Text>
                <Text style={[styles.shiftTime, cancelled && styles.cancelledText]}>
                  {shiftHours(s)} · {s.shiftCode}
                </Text>
              </View>
              {badge && <Badge label={badge.label} tone={badge.tone} />}
            </View>
          );
        })
      )}
    </Card>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background },
  content: { padding: 16, paddingBottom: 32, gap: 10 },
  flex: { flex: 1 },
  gap: { gap: 12 },
  loading: { marginTop: 48 },
  weekBar: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 6,
  },
  navButton: { width: 44, height: 44, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  weekInfo: { flex: 1, alignItems: 'center' },
  weekLabel: { fontSize: 15, fontWeight: '700', color: colors.foreground },
  weekRange: { marginTop: 2, fontSize: 13, color: colors.muted },
  summary: { marginTop: 4, marginLeft: 4, fontSize: 13, color: colors.muted },
  dayCard: { paddingVertical: 12, gap: 10 },
  todayCard: { borderColor: colors.primary, borderWidth: 1.5 },
  pastCard: { opacity: 0.7 },
  dayHeader: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  dayName: { fontSize: 15, fontWeight: '700', color: colors.foreground },
  todayText: { color: colors.primary },
  dayDate: { flex: 1, fontSize: 14, color: colors.muted },
  dayOff: { fontSize: 14, color: colors.muted },
  shiftRow: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  shiftBar: { width: 4, alignSelf: 'stretch', borderRadius: 2, backgroundColor: colors.primary },
  cancelledBar: { backgroundColor: colors.border },
  shiftName: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  shiftTime: { marginTop: 2, fontSize: 13, color: colors.muted },
  cancelledText: { textDecorationLine: 'line-through', color: colors.muted },
  note: { marginTop: 4, marginHorizontal: 4, fontSize: 12, lineHeight: 17, color: colors.muted },
});
