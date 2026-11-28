import { useCallback, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { router, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Card, Notice } from '@/components/ui/Feedback';
import { useChatStore } from '@/stores/chatStore';
import { getErrorMessage } from '@/services/apiClient';
import { formatShortTime } from '@/lib/format';
import { colors, radius } from '@/theme';
import type { Conversation } from '@/types/chat';
import { ChatAvatar } from '../components/ChatAvatar';

/** Danh sách phòng chat (UC 7.1, 7.2): 1-1, nhóm chi nhánh, nhóm tự tạo — sắp theo hoạt động gần nhất. */
export function ConversationsScreen() {
  const conversations = useChatStore((s) => s.conversations);
  const loaded = useChatStore((s) => s.loaded);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async (refresh = false) => {
    if (refresh) setRefreshing(true);
    try {
      await useChatStore.getState().load();
      setError(null);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setRefreshing(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void reload();
    }, [reload]),
  );

  return (
    <FlatList
      data={conversations}
      keyExtractor={(c) => String(c.id)}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void reload(true)} />}
      ListHeaderComponent={
        <View style={styles.header}>
          <Pressable
            testID="chat-new-direct"
            onPress={() => router.push({ pathname: '/chat/new', params: { mode: 'direct' } })}
            accessibilityRole="button"
            style={({ pressed }) => [styles.action, pressed && styles.pressed]}
          >
            <Ionicons name="chatbubble-ellipses-outline" size={20} color={colors.primary} />
            <Text style={styles.actionText}>Nhắn tin mới</Text>
          </Pressable>
          <Pressable
            testID="chat-new-group"
            onPress={() => router.push({ pathname: '/chat/new', params: { mode: 'group' } })}
            accessibilityRole="button"
            style={({ pressed }) => [styles.action, pressed && styles.pressed]}
          >
            <Ionicons name="people-outline" size={20} color={colors.primary} />
            <Text style={styles.actionText}>Tạo nhóm</Text>
          </Pressable>
          <Notice tone="danger" message={error} style={styles.fullRow} />
        </View>
      }
      ListEmptyComponent={
        !loaded ? (
          <ActivityIndicator color={colors.primary} style={styles.loading} />
        ) : (
          <Card style={styles.empty}>
            <Ionicons name="chatbubbles-outline" size={28} color={colors.muted} />
            <Text style={styles.emptyText}>Chưa có cuộc trò chuyện nào. Bắt đầu bằng “Nhắn tin mới”.</Text>
          </Card>
        )
      }
      renderItem={({ item }) => <ConversationRow conversation={item} />}
    />
  );
}

function ConversationRow({ conversation: c }: { conversation: Conversation }) {
  const unread = c.unreadCount > 0;
  const last = c.lastMessage;
  const preview = last ? (c.type === 'group' ? `${last.senderName}: ${last.preview}` : last.preview) : 'Chưa có tin nhắn';
  return (
    <Pressable
      testID={`conversation-${c.id}`}
      onPress={() => router.push({ pathname: '/chat/[id]', params: { id: String(c.id) } })}
      accessibilityRole="button"
      style={({ pressed }) => [pressed && styles.pressed]}
    >
      <Card style={styles.item}>
        <ChatAvatar conversation={c} />
        <View style={styles.flex}>
          <View style={styles.top}>
            <Text style={[styles.title, unread && styles.titleUnread]} numberOfLines={1}>
              {c.title}
            </Text>
            <Text style={styles.time}>{formatShortTime(last?.createdAt ?? c.lastActivityAt)}</Text>
          </View>
          <View style={styles.top}>
            <Text style={[styles.preview, unread && styles.previewUnread]} numberOfLines={1}>
              {preview}
            </Text>
            {unread && (
              <View style={styles.badge}>
                <Text style={styles.badgeText}>{c.unreadCount > 99 ? '99+' : c.unreadCount}</Text>
              </View>
            )}
          </View>
        </View>
      </Card>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 32, gap: 10, flexGrow: 1 },
  header: { flexDirection: 'row', flexWrap: 'wrap', gap: 10, marginBottom: 2 },
  action: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    height: 46,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.primary,
    backgroundColor: colors.primarySoft,
  },
  actionText: { fontSize: 14, fontWeight: '600', color: colors.primary },
  fullRow: { width: '100%' },
  item: { flexDirection: 'row', alignItems: 'center', gap: 12, padding: 12 },
  top: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  title: { flex: 1, fontSize: 15, fontWeight: '600', color: colors.foreground },
  titleUnread: { fontWeight: '800' },
  time: { fontSize: 12, color: colors.muted },
  preview: { flex: 1, marginTop: 3, fontSize: 13, color: colors.muted },
  previewUnread: { color: colors.foreground, fontWeight: '600' },
  badge: { minWidth: 20, height: 20, paddingHorizontal: 6, borderRadius: 10, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' },
  badgeText: { fontSize: 11, fontWeight: '700', color: colors.onPrimary },
  empty: { alignItems: 'center', gap: 8, paddingVertical: 28 },
  emptyText: { fontSize: 14, color: colors.muted, textAlign: 'center' },
  loading: { marginTop: 32 },
  pressed: { opacity: 0.85 },
});
