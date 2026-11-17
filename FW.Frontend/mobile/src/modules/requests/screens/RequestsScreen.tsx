import { useCallback, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { requestApi } from '@/services/requestApi';
import { getErrorMessage } from '@/services/apiClient';
import { confirmAction, notify } from '@/lib/dialog';
import { formatDate } from '@/lib/format';
import { colors, radius } from '@/theme';
import type { LeaveBalance, LeaveRequest } from '@/types/requests';
import { categoryInfo, days, statusBadges, summarize } from '../requestUi';

type Filter = 'all' | 'open' | 'approved' | 'closed';

const filters: { key: Filter; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'open', label: 'Đang chờ' },
  { key: 'approved', label: 'Đã duyệt' },
  { key: 'closed', label: 'Từ chối / đã rút' },
];

const matches = (r: LeaveRequest, f: Filter) =>
  f === 'all' ||
  (f === 'open' && (r.status === 'pending' || r.status === 'pending_peer')) ||
  (f === 'approved' && r.status === 'approved') ||
  (f === 'closed' && (r.status === 'rejected' || r.status === 'peer_declined' || r.status === 'cancelled'));

/** ViewModel: quỹ phép, đơn đổi ca chờ tôi trả lời, đơn của tôi — tải lại mỗi lần quay về tab. */
function useRequests() {
  const [mine, setMine] = useState<LeaveRequest[] | null>(null);
  const [incoming, setIncoming] = useState<LeaveRequest[]>([]);
  const [balance, setBalance] = useState<LeaveBalance | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async (refresh = false) => {
    if (refresh) setRefreshing(true);
    try {
      const [m, p, b] = await Promise.all([
        requestApi.mine({ pageSize: 100 }),
        requestApi.peer({ status: 'pending_peer', pageSize: 50 }),
        requestApi.balance(),
      ]);
      setMine(m.data.items);
      setIncoming(p.data.items);
      setBalance(b.data);
      setError(null);
    } catch (err) {
      setError(getErrorMessage(err));
      setMine((prev) => prev ?? []);
    } finally {
      setRefreshing(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load]),
  );

  return { mine, incoming, balance, error, refreshing, reload: load };
}

/** Tab Đơn từ (UC 5.1, 5.2, 5.6). */
export function RequestsScreen() {
  const vm = useRequests();
  const [filter, setFilter] = useState<Filter>('all');
  const [busyId, setBusyId] = useState<number | null>(null);

  const respond = (r: LeaveRequest, accepted: boolean) => {
    const run = async () => {
      setBusyId(r.id);
      try {
        const { message } = await requestApi.respond(r.id, accepted);
        notify(message);
        await vm.reload();
      } catch (err) {
        notify(getErrorMessage(err), 'Không thực hiện được');
      } finally {
        setBusyId(null);
      }
    };
    confirmAction(
      accepted ? 'Đồng ý đổi ca?' : 'Từ chối đổi ca?',
      accepted ? `Đơn sẽ được chuyển tới Quản lý duyệt.\n${summarize(r)}` : `${r.employee.fullName} sẽ được báo bạn từ chối.`,
      accepted ? 'Đồng ý' : 'Từ chối',
      () => void run(),
      !accepted,
    );
  };

  const shown = (vm.mine ?? []).filter((r) => matches(r, filter));

  return (
    <ScrollView
      style={styles.flex}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={vm.refreshing} onRefresh={() => void vm.reload(true)} />}
    >
      {vm.balance && (
        <View style={styles.balance}>
          <View style={styles.flex}>
            <Text style={styles.balanceLabel}>Phép năm {vm.balance.year} còn xin được</Text>
            <Text style={styles.balanceValue}>
              {days(vm.balance.availableDays)} <Text style={styles.balanceUnit}>/ {days(vm.balance.totalDays)} ngày</Text>
            </Text>
            <Text style={styles.balanceHint}>
              Đã dùng {days(vm.balance.usedDays)}
              {vm.balance.pendingDays > 0 ? ` · đang chờ duyệt ${days(vm.balance.pendingDays)}` : ''}
            </Text>
          </View>
          <Ionicons name="sunny-outline" size={36} color="rgba(255,255,255,0.8)" />
        </View>
      )}

      <Button title="Tạo đơn mới" icon={<Ionicons name="add" size={20} color={colors.white} />} onPress={() => router.push('/request-new')} />
      <Notice tone="danger" message={vm.error} />

      {vm.incoming.length > 0 && (
        <>
          <Text style={styles.section}>Cần bạn trả lời ({vm.incoming.length})</Text>
          {vm.incoming.map((r) => (
            <Card key={r.id} style={styles.incoming}>
              <View style={styles.row}>
                <Ionicons name="swap-horizontal" size={20} color={categoryInfo.shift_swap.tint} />
                <Text style={styles.title}>{r.employee.fullName} đề nghị {r.targetSchedule ? 'đổi ca' : 'bạn làm thay'}</Text>
              </View>
              <Text style={styles.summary}>{summarize(r)}</Text>
              <Text style={styles.reason} numberOfLines={3}>
                “{r.reason}”
              </Text>
              <View style={styles.actions}>
                <Button title="Từ chối" variant="danger" style={styles.flex} disabled={busyId === r.id} onPress={() => respond(r, false)} />
                <Button title="Đồng ý" style={styles.flex} loading={busyId === r.id} onPress={() => respond(r, true)} />
              </View>
            </Card>
          ))}
        </>
      )}

      <Text style={styles.section}>Đơn của tôi</Text>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips}>
        {filters.map((f) => (
          <Pressable
            key={f.key}
            onPress={() => setFilter(f.key)}
            style={[styles.chip, filter === f.key && styles.chipActive]}
            accessibilityRole="button"
            accessibilityState={{ selected: filter === f.key }}
          >
            <Text style={[styles.chipText, filter === f.key && styles.chipTextActive]}>{f.label}</Text>
          </Pressable>
        ))}
      </ScrollView>

      {vm.mine === null ? (
        <ActivityIndicator color={colors.primary} style={styles.loading} />
      ) : shown.length === 0 ? (
        <Card style={styles.empty}>
          <Ionicons name="document-text-outline" size={28} color={colors.muted} />
          <Text style={styles.emptyText}>{filter === 'all' ? 'Bạn chưa gửi đơn nào.' : 'Không có đơn nào ở mục này.'}</Text>
        </Card>
      ) : (
        shown.map((r) => <RequestRow key={r.id} request={r} />)
      )}
    </ScrollView>
  );
}

function RequestRow({ request: r }: { request: LeaveRequest }) {
  const info = categoryInfo[r.category];
  const badge = statusBadges[r.status];
  return (
    <Pressable
      onPress={() => router.push({ pathname: '/request/[id]', params: { id: String(r.id) } })}
      style={({ pressed }) => [pressed && styles.pressed]}
      accessibilityRole="button"
    >
      <Card style={styles.item}>
        <View style={[styles.icon, { backgroundColor: `${info.tint}1A` }]}>
          <Ionicons name={info.icon} size={20} color={info.tint} />
        </View>
        <View style={styles.flex}>
          <Text style={styles.title} numberOfLines={1}>
            {r.leaveTypeName}
          </Text>
          <Text style={styles.summary} numberOfLines={2}>
            {summarize(r)}
          </Text>
          <Text style={styles.meta}>Gửi {formatDate(r.createdAt)}</Text>
        </View>
        <Badge label={badge.label} tone={badge.tone} />
      </Card>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 32, gap: 12 },
  balance: { flexDirection: 'row', alignItems: 'center', gap: 12, backgroundColor: colors.header, borderRadius: radius.lg, padding: 18 },
  balanceLabel: { fontSize: 13, color: 'rgba(255,255,255,0.7)' },
  balanceValue: { marginTop: 2, fontSize: 28, fontWeight: '800', color: colors.white },
  balanceUnit: { fontSize: 15, fontWeight: '600', color: 'rgba(255,255,255,0.7)' },
  balanceHint: { marginTop: 2, fontSize: 12, color: 'rgba(255,255,255,0.6)' },
  section: { marginTop: 8, marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  incoming: { gap: 8, borderColor: colors.warning, backgroundColor: colors.warningSoft },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  actions: { flexDirection: 'row', gap: 10, marginTop: 4 },
  chips: { gap: 8, paddingVertical: 2 },
  chip: { paddingHorizontal: 14, paddingVertical: 8, borderRadius: radius.full, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface },
  chipActive: { backgroundColor: colors.primary, borderColor: colors.primary },
  chipText: { fontSize: 13, fontWeight: '600', color: colors.foreground },
  chipTextActive: { color: colors.white },
  item: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  icon: { width: 40, height: 40, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  title: { flexShrink: 1, fontSize: 15, fontWeight: '600', color: colors.foreground },
  summary: { marginTop: 2, fontSize: 13, lineHeight: 18, color: colors.muted },
  reason: { fontSize: 13, lineHeight: 19, color: colors.foreground, fontStyle: 'italic' },
  meta: { marginTop: 4, fontSize: 12, color: colors.muted },
  empty: { alignItems: 'center', gap: 8, paddingVertical: 28 },
  emptyText: { fontSize: 14, color: colors.muted },
  loading: { marginTop: 24 },
  pressed: { opacity: 0.85 },
});
