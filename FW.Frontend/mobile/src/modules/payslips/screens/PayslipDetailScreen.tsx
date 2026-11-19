import { useCallback, useState } from 'react';
import { ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useFocusEffect, useLocalSearchParams } from 'expo-router';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { payslipApi } from '@/services/payslipApi';
import { getErrorMessage } from '@/services/apiClient';
import { employmentTypeLabels, formatCurrency, formatHours } from '@/lib/format';
import { colors, radius } from '@/theme';
import type { PayslipDetail, PayslipItemKind } from '@/types/payslip';

const kindTitles: Record<PayslipItemKind, string> = {
  basic: 'Lương cơ bản',
  allowance: 'Phụ cấp',
  overtime: 'Tăng ca',
  penalty: 'Khoản trừ (phạt)',
  adjustment: 'Điều chỉnh',
};
const kindOrder: PayslipItemKind[] = ['basic', 'allowance', 'overtime', 'adjustment', 'penalty'];

const formatQuantity = (n: number) => Number(n.toFixed(2)).toString().replace('.', ',');

/** Chi tiết một phiếu lương: tổng thực nhận + các khoản theo nhóm. */
export function PayslipDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [detail, setDetail] = useState<PayslipDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(
    async (refresh = false) => {
      if (refresh) setRefreshing(true);
      try {
        const { data } = await payslipApi.get(Number(id));
        setDetail(data);
        setError(null);
      } catch (err) {
        setError(getErrorMessage(err));
      } finally {
        setRefreshing(false);
      }
    },
    [id],
  );

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load]),
  );

  if (!detail) {
    return (
      <View style={styles.center}>
        {error ? <Notice tone="danger" message={error} /> : <ActivityIndicator color={colors.primary} />}
      </View>
    );
  }

  const { payslip: p, items } = detail;
  const groups = kindOrder
    .map((kind) => ({ kind, rows: items.filter((i) => i.kind === kind) }))
    .filter((g) => g.rows.length > 0);

  return (
    <ScrollView contentContainerStyle={styles.content} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} />}>
      <View style={styles.hero} testID="payslip-hero">
        <Text style={styles.heroLabel}>
          Thực nhận tháng {p.month}/{p.year}
        </Text>
        <Text style={styles.heroValue}>{formatCurrency(p.netPay)}</Text>
        <View style={styles.heroBadges}>
          <Badge label={employmentTypeLabels[p.employmentType]} tone="info" />
          {p.branchName ? <Badge label={p.branchName} tone="info" /> : null}
        </View>
      </View>

      <Card style={styles.summary}>
        <Row label={p.employmentType === 'part_time' ? 'Đơn giá theo giờ' : 'Lương cơ bản tháng'} value={formatCurrency(p.employmentType === 'part_time' ? p.hourlyRate : p.baseSalary)} />
        <Row
          label="Công thực tế"
          value={p.employmentType === 'part_time' ? formatHours(p.workHours) : `${formatQuantity(p.workDays)} / ${formatQuantity(p.standardWorkDays)} ngày`}
        />
        {p.overtimeHours > 0 && <Row label="Giờ tăng ca" value={formatHours(p.overtimeHours)} />}
      </Card>

      {groups.map(({ kind, rows }) => (
        <View key={kind} style={styles.group}>
          <Text style={styles.groupTitle}>{kindTitles[kind]}</Text>
          <Card style={styles.groupCard}>
            {rows.map((item, index) => (
              <View key={`${kind}-${index}`} style={[styles.item, index > 0 && styles.itemBorder]}>
                <View style={styles.flex}>
                  <Text style={styles.itemName}>{item.name}</Text>
                  {item.quantity !== 1 && (
                    <Text style={styles.itemMeta}>
                      {formatQuantity(item.quantity)} × {formatCurrency(item.unitAmount)}
                    </Text>
                  )}
                  {item.note ? <Text style={styles.itemMeta}>{item.note}</Text> : null}
                </View>
                <Text style={[styles.itemAmount, item.amount < 0 && { color: colors.danger }]}>{formatCurrency(item.amount)}</Text>
              </View>
            ))}
          </Card>
        </View>
      ))}
    </ScrollView>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={styles.rowValue}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  center: { flex: 1, justifyContent: 'center', padding: 20 },
  content: { padding: 16, paddingBottom: 32, gap: 12 },
  hero: { backgroundColor: colors.header, borderRadius: radius.lg, padding: 18, gap: 6 },
  heroLabel: { fontSize: 13, color: 'rgba(255,255,255,0.7)' },
  heroValue: { fontSize: 32, fontWeight: '800', color: colors.white },
  heroBadges: { flexDirection: 'row', gap: 8, flexWrap: 'wrap' },
  summary: { gap: 10 },
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: 12 },
  rowLabel: { fontSize: 14, color: colors.muted },
  rowValue: { fontSize: 14, fontWeight: '600', color: colors.foreground },
  group: { gap: 6 },
  groupTitle: { marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  groupCard: { padding: 0 },
  item: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 16, paddingVertical: 12 },
  itemBorder: { borderTopWidth: 1, borderTopColor: colors.border },
  itemName: { fontSize: 14, fontWeight: '600', color: colors.foreground },
  itemMeta: { marginTop: 2, fontSize: 12, color: colors.muted },
  itemAmount: { fontSize: 14, fontWeight: '700', color: colors.foreground },
});
