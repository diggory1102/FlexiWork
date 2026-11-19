import { useCallback, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { router, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Card, Notice } from '@/components/ui/Feedback';
import { payslipApi } from '@/services/payslipApi';
import { getErrorMessage } from '@/services/apiClient';
import { formatCurrency } from '@/lib/format';
import { colors } from '@/theme';
import type { Payslip } from '@/types/payslip';

/** Danh sách phiếu lương của tôi (chỉ các kỳ đã chốt), mới nhất trước. */
export function PayslipsScreen() {
  const [items, setItems] = useState<Payslip[] | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (refresh = false) => {
    if (refresh) setRefreshing(true);
    try {
      const { data } = await payslipApi.mine({ pageSize: 36 });
      setItems(data.items);
      setError(null);
    } catch (err) {
      setError(getErrorMessage(err));
      setItems((prev) => prev ?? []);
    } finally {
      setRefreshing(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load]),
  );

  return (
    <FlatList
      data={items ?? []}
      keyExtractor={(p) => String(p.id)}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} />}
      ListHeaderComponent={<Notice tone="danger" message={error} />}
      ListEmptyComponent={
        items === null ? (
          <ActivityIndicator color={colors.primary} style={styles.loading} />
        ) : (
          <Card style={styles.empty}>
            <Ionicons name="receipt-outline" size={28} color={colors.muted} />
            <Text style={styles.emptyText}>Chưa có phiếu lương nào. Phiếu xuất hiện khi kỳ lương được chốt.</Text>
          </Card>
        )
      }
      renderItem={({ item: p }) => (
        <Pressable
          testID={`payslip-${p.id}`}
          onPress={() => router.push({ pathname: '/payslips/[id]', params: { id: String(p.id) } })}
          accessibilityRole="button"
          style={({ pressed }) => [pressed && styles.pressed]}
        >
          <Card style={styles.item}>
            <View style={styles.icon}>
              <Ionicons name="receipt-outline" size={20} color={colors.primary} />
            </View>
            <View style={styles.flex}>
              <Text style={styles.title}>
                Lương tháng {p.month}/{p.year}
              </Text>
              <Text style={styles.meta}>{p.branchName ?? 'Chưa gán chi nhánh'}</Text>
            </View>
            <View style={styles.right}>
              <Text style={styles.amount}>{formatCurrency(p.netPay)}</Text>
              <Text style={styles.meta}>Thực nhận</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.muted} />
          </Card>
        </Pressable>
      )}
    />
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 32, gap: 10, flexGrow: 1 },
  item: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  icon: { width: 40, height: 40, borderRadius: 12, backgroundColor: colors.primarySoft, alignItems: 'center', justifyContent: 'center' },
  title: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  meta: { marginTop: 2, fontSize: 12, color: colors.muted },
  right: { alignItems: 'flex-end' },
  amount: { fontSize: 15, fontWeight: '800', color: colors.success },
  empty: { alignItems: 'center', gap: 8, paddingVertical: 28 },
  emptyText: { fontSize: 14, color: colors.muted, textAlign: 'center' },
  loading: { marginTop: 32 },
  pressed: { opacity: 0.85 },
});
