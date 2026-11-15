import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { attendanceApi } from '@/services/attendanceApi';
import { getErrorMessage } from '@/services/apiClient';
import { formatDate, formatHours, formatTime } from '@/lib/format';
import { todayIso } from '@/lib/dates';
import { colors, radius } from '@/theme';
import type { AttendanceLogItem } from '@/types/attendance';
import { attendanceStatusBadges } from '../attendanceUi';

const PAGE_SIZE = 40;

/** Tháng dạng "yyyy-MM" → ngày đầu / cuối tháng (yyyy-MM-dd). */
function monthRange(month: string): [string, string] {
  const [y, m] = month.split('-').map(Number);
  const last = new Date(Date.UTC(y, m, 0)).getUTCDate();
  return [`${month}-01`, `${month}-${String(last).padStart(2, '0')}`];
}

function shiftMonth(month: string, delta: number): string {
  const [y, m] = month.split('-').map(Number);
  const d = new Date(Date.UTC(y, m - 1 + delta, 1));
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`;
}

/** ViewModel: lịch sử chấm công của tôi theo tháng, tải thêm theo trang. */
function useHistory(month: string) {
  const [items, setItems] = useState<AttendanceLogItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (mode: 'initial' | 'refresh' = 'initial') => {
      if (mode === 'refresh') setRefreshing(true);
      else setLoading(true);
      try {
        const [from, to] = monthRange(month);
        const { data } = await attendanceApi.mine(from, to, 1, PAGE_SIZE);
        setItems(data.items);
        setTotal(data.totalCount);
        setPage(1);
        setHasMore(data.hasNextPage);
        setError(null);
      } catch (err) {
        setError(getErrorMessage(err));
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [month],
  );

  const loadMore = async () => {
    setLoadingMore(true);
    try {
      const [from, to] = monthRange(month);
      const { data } = await attendanceApi.mine(from, to, page + 1, PAGE_SIZE);
      setItems((prev) => [...prev, ...data.items]);
      setPage(page + 1);
      setHasMore(data.hasNextPage);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoadingMore(false);
    }
  };

  useEffect(() => {
    void load();
  }, [load]);

  return { items, total, hasMore, loading, refreshing, loadingMore, error, refresh: () => load('refresh'), retry: () => load(), loadMore };
}

/** Lịch sử chấm công cá nhân theo tháng: giờ vào/ra, đi muộn/về sớm, giờ làm, trạng thái duyệt. */
export function AttendanceHistoryScreen() {
  const thisMonth = todayIso().slice(0, 7);
  const [month, setMonth] = useState(thisMonth);
  const vm = useHistory(month);
  const [y, m] = month.split('-');

  const counted = vm.items.filter((i) => i.status !== 'invalid' && i.checkoutTime);
  const hours = counted.reduce((sum, i) => sum + i.actualWorkHours, 0);
  const late = vm.items.filter((i) => i.lateMinutes > 0).length;
  const pending = vm.items.filter((i) => i.status === 'pending_approval').length;

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={vm.refreshing} onRefresh={vm.refresh} tintColor={colors.primary} />}
    >
      <View style={styles.monthBar}>
        <Pressable onPress={() => setMonth(shiftMonth(month, -1))} style={styles.navButton} accessibilityRole="button" accessibilityLabel="Tháng trước" hitSlop={8}>
          <Ionicons name="chevron-back" size={20} color={colors.foreground} />
        </Pressable>
        <Pressable onPress={() => setMonth(thisMonth)} style={styles.monthInfo} accessibilityRole="button" accessibilityLabel="Về tháng này">
          <Text style={styles.monthLabel}>
            Tháng {Number(m)}/{y}
          </Text>
          {month === thisMonth && <Text style={styles.muted}>Tháng này</Text>}
        </Pressable>
        <Pressable
          onPress={() => setMonth(shiftMonth(month, 1))}
          disabled={month >= thisMonth}
          style={[styles.navButton, month >= thisMonth && styles.disabled]}
          accessibilityRole="button"
          accessibilityLabel="Tháng sau"
          hitSlop={8}
        >
          <Ionicons name="chevron-forward" size={20} color={colors.foreground} />
        </Pressable>
      </View>

      {vm.loading ? (
        <ActivityIndicator color={colors.primary} style={styles.loading} />
      ) : vm.error && vm.items.length === 0 ? (
        <View style={styles.gap}>
          <Notice tone="danger" message={vm.error} />
          <Button title="Thử lại" variant="secondary" onPress={vm.retry} />
        </View>
      ) : (
        <>
          <View style={styles.stats}>
            <Stat label="Lượt chấm công" value={String(vm.total)} />
            <Stat label="Giờ làm" value={formatHours(hours).replace(' giờ', '')} unit="giờ" />
            <Stat label="Đi muộn" value={String(late)} unit="lần" tone={late > 0 ? colors.warning : undefined} />
            <Stat label="Chờ duyệt" value={String(pending)} tone={pending > 0 ? colors.warning : undefined} />
          </View>

          {vm.items.length === 0 ? (
            <Card style={styles.empty}>
              <Ionicons name="receipt-outline" size={28} color={colors.muted} />
              <Text style={styles.muted}>Chưa có lượt chấm công nào trong tháng.</Text>
            </Card>
          ) : (
            vm.items.map((item) => <LogCard key={item.id} item={item} />)
          )}

          {vm.hasMore && <Button title="Xem thêm" variant="secondary" loading={vm.loadingMore} onPress={() => void vm.loadMore()} />}
          <Text style={styles.footnote}>
            Lượt "Chờ duyệt" (VD không đọc được Wi-Fi) được tính công sau khi Quản lý chấp nhận. Giờ làm chỉ tính thời gian trong khung ca.
          </Text>
        </>
      )}
    </ScrollView>
  );
}

function Stat({ label, value, unit, tone }: { label: string; value: string; unit?: string; tone?: string }) {
  return (
    <View style={styles.stat}>
      <Text style={[styles.statValue, tone ? { color: tone } : null]}>
        {value}
        {unit ? <Text style={styles.statUnit}> {unit}</Text> : null}
      </Text>
      <Text style={styles.statLabel}>{label}</Text>
    </View>
  );
}

function LogCard({ item }: { item: AttendanceLogItem }) {
  const badge = attendanceStatusBadges[item.status];
  return (
    <Card style={styles.logCard}>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Text style={styles.logDate}>{formatDate(item.workDate)}</Text>
          <Text style={styles.muted}>
            {item.shiftName ?? 'Ca đã xóa'}
            {item.shiftStartTime ? ` · ${item.shiftStartTime} – ${item.shiftEndTime}` : ''}
          </Text>
        </View>
        <Badge label={badge.label} tone={badge.tone} />
      </View>
      <View style={styles.row}>
        <Ionicons name="log-in-outline" size={16} color={colors.muted} />
        <Text style={styles.time}>{formatTime(item.checkinTime)}</Text>
        {item.lateMinutes > 0 && <Text style={styles.warn}>muộn {item.lateMinutes}'</Text>}
        <View style={styles.spacer} />
        <Ionicons name="log-out-outline" size={16} color={colors.muted} />
        <Text style={styles.time}>{formatTime(item.checkoutTime)}</Text>
        {item.earlyMinutes > 0 && <Text style={styles.warn}>sớm {item.earlyMinutes}'</Text>}
        <View style={styles.spacer} />
        <Text style={styles.hours}>{item.checkoutTime ? formatHours(item.actualWorkHours) : 'Chưa ra ca'}</Text>
      </View>
      {item.note && <Text style={styles.note}>{item.note}</Text>}
    </Card>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background },
  content: { padding: 16, paddingBottom: 32, gap: 10 },
  flex: { flex: 1 },
  gap: { gap: 12 },
  loading: { marginTop: 48 },
  muted: { fontSize: 13, lineHeight: 18, color: colors.muted },
  disabled: { opacity: 0.35 },
  monthBar: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 6,
  },
  navButton: { width: 44, height: 44, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  monthInfo: { flex: 1, alignItems: 'center' },
  monthLabel: { fontSize: 15, fontWeight: '700', color: colors.foreground },
  stats: { flexDirection: 'row', gap: 8 },
  stat: { flex: 1, backgroundColor: colors.surface, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, paddingVertical: 10, alignItems: 'center' },
  statValue: { fontSize: 18, fontWeight: '800', color: colors.foreground },
  statUnit: { fontSize: 12, fontWeight: '600', color: colors.muted },
  statLabel: { marginTop: 2, fontSize: 11, color: colors.muted, textAlign: 'center' },
  empty: { alignItems: 'center', gap: 8, paddingVertical: 28 },
  logCard: { gap: 10, paddingVertical: 12 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  logDate: { fontSize: 15, fontWeight: '700', color: colors.foreground },
  time: { fontSize: 15, fontWeight: '600', color: colors.foreground, fontVariant: ['tabular-nums'] },
  warn: { fontSize: 12, fontWeight: '600', color: colors.warning },
  spacer: { width: 10 },
  hours: { marginLeft: 'auto', fontSize: 13, fontWeight: '600', color: colors.muted },
  note: { fontSize: 13, lineHeight: 18, color: colors.warning },
  footnote: { marginTop: 4, marginHorizontal: 4, fontSize: 12, lineHeight: 17, color: colors.muted },
});
