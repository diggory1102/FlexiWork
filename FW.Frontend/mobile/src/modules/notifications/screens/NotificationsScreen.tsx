import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { router, useFocusEffect, useNavigation } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Card, Notice } from '@/components/ui/Feedback';
import { notificationApi } from '@/services/notificationApi';
import { getErrorMessage } from '@/services/apiClient';
import { useNotificationStore } from '@/stores/notificationStore';
import { colors } from '@/theme';
import type { AppNotification } from '@/types/notification';
import { notificationIcons, notificationTarget, timeAgo } from '../notificationUi';

const PAGE_SIZE = 20;

/** Trung tâm thông báo (UC 6.x): danh sách có phân trang, chạm để đánh dấu đã đọc và mở màn hình liên quan. */
export function NotificationsScreen() {
  const navigation = useNavigation();
  const unread = useNotificationStore((s) => s.unread);
  const [items, setItems] = useState<AppNotification[] | null>(null);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const requestSeq = useRef(0);

  const load = useCallback(async (pageIndex: number, mode: 'replace' | 'append') => {
    const seq = ++requestSeq.current;
    try {
      const { data } = await notificationApi.list({ pageIndex, pageSize: PAGE_SIZE });
      if (seq !== requestSeq.current) return;
      setItems((prev) => (mode === 'append' ? [...(prev ?? []), ...data.items] : data.items));
      setPage(pageIndex);
      setHasMore(data.hasNextPage);
      setError(null);
    } catch (err) {
      if (seq !== requestSeq.current) return;
      setError(getErrorMessage(err));
      setItems((prev) => prev ?? []);
    } finally {
      setRefreshing(false);
      setLoadingMore(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void load(1, 'replace');
    }, [load]),
  );

  // Thông báo mới đẩy về khi đang xem → số chưa đọc đổi → nạp lại trang đầu
  const lastUnread = useRef(unread);
  useEffect(() => {
    if (unread > lastUnread.current) void load(1, 'replace');
    lastUnread.current = unread;
  }, [unread, load]);

  const markAll = useCallback(async () => {
    try {
      await notificationApi.markAllRead();
      setItems((prev) => prev?.map((n) => ({ ...n, isRead: true })) ?? prev);
      useNotificationStore.getState().setUnread(0);
    } catch (err) {
      setError(getErrorMessage(err));
    }
  }, []);

  useLayoutEffect(() => {
    navigation.setOptions({
      headerRight: () =>
        unread > 0 ? (
          <Pressable onPress={() => void markAll()} hitSlop={8} accessibilityRole="button" testID="notifications-mark-all" style={styles.headerButton}>
            <Text style={styles.headerAction}>Đọc tất cả</Text>
          </Pressable>
        ) : null,
    });
  }, [navigation, unread, markAll]);

  const open = async (n: AppNotification) => {
    if (!n.isRead) {
      setItems((prev) => prev?.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)) ?? prev);
      useNotificationStore.getState().setUnread(useNotificationStore.getState().unread - 1);
      void notificationApi.markRead(n.id).catch(() => useNotificationStore.getState().refresh().catch(() => undefined));
    }
    const target = notificationTarget(n);
    if (target) router.push(target);
  };

  return (
    <FlatList
      data={items ?? []}
      keyExtractor={(n) => String(n.id)}
      contentContainerStyle={styles.content}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={() => {
            setRefreshing(true);
            void load(1, 'replace');
          }}
        />
      }
      onEndReachedThreshold={0.4}
      onEndReached={() => {
        if (hasMore && !loadingMore) {
          setLoadingMore(true);
          void load(page + 1, 'append');
        }
      }}
      ListHeaderComponent={<Notice tone="danger" message={error} style={styles.error} />}
      ListEmptyComponent={
        items === null ? (
          <ActivityIndicator color={colors.primary} style={styles.loading} />
        ) : (
          <Card style={styles.empty}>
            <Ionicons name="notifications-off-outline" size={28} color={colors.muted} />
            <Text style={styles.emptyText}>Chưa có thông báo nào.</Text>
          </Card>
        )
      }
      ListFooterComponent={loadingMore ? <ActivityIndicator color={colors.primary} style={styles.loading} /> : undefined}
      renderItem={({ item }) => <NotificationRow notification={item} onPress={() => void open(item)} />}
    />
  );
}

function NotificationRow({ notification: n, onPress }: { notification: AppNotification; onPress: () => void }) {
  const visual = notificationIcons[n.type] ?? notificationIcons.attendance;
  return (
    <Pressable onPress={onPress} accessibilityRole="button" style={({ pressed }) => [pressed && styles.pressed]} testID={`notification-${n.id}`}>
      <Card style={[styles.item, !n.isRead && styles.itemUnread]}>
        <View style={[styles.icon, { backgroundColor: `${visual.tint}1A` }]}>
          <Ionicons name={visual.icon} size={20} color={visual.tint} />
        </View>
        <View style={styles.texts}>
          <Text style={[styles.title, !n.isRead && styles.titleUnread]} numberOfLines={2}>
            {n.title}
          </Text>
          <Text style={styles.body} numberOfLines={3}>
            {n.content}
          </Text>
          <Text style={styles.time}>{timeAgo(n.createdAt)}</Text>
        </View>
        {!n.isRead && <View style={styles.dot} />}
      </Card>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  content: { padding: 16, paddingBottom: 32, gap: 10, flexGrow: 1 },
  headerButton: { paddingHorizontal: 12, paddingVertical: 6 },
  headerAction: { fontSize: 14, fontWeight: '600', color: colors.primary },
  error: { marginBottom: 4 },
  item: { flexDirection: 'row', alignItems: 'flex-start', gap: 12 },
  itemUnread: { backgroundColor: colors.primarySoft, borderColor: '#C7D7FE' },
  icon: { width: 40, height: 40, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  texts: { flex: 1, gap: 2 },
  title: { fontSize: 14, fontWeight: '600', color: colors.foreground },
  titleUnread: { fontWeight: '800' },
  body: { fontSize: 13, lineHeight: 18, color: colors.muted },
  time: { marginTop: 2, fontSize: 12, color: colors.muted },
  dot: { width: 10, height: 10, borderRadius: 5, backgroundColor: colors.primary, marginTop: 6 },
  empty: { alignItems: 'center', gap: 8, paddingVertical: 28 },
  emptyText: { fontSize: 14, color: colors.muted },
  loading: { marginVertical: 20 },
  pressed: { opacity: 0.85 },
});
