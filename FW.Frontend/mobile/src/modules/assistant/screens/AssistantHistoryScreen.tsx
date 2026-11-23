import { useEffect, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Notice } from '@/components/ui/Feedback';
import { getErrorMessage } from '@/services/apiClient';
import { assistantApi } from '@/services/assistantApi';
import { useAssistantStore } from '@/stores/assistantStore';
import { formatDate } from '@/lib/format';
import type { AssistantConversation } from '@/types/assistant';
import { colors, radius } from '@/theme';

/** Danh sách các cuộc trò chuyện trước đây với trợ lý; chọn một cuộc để mở lại. */
export function AssistantHistoryScreen() {
  const [items, setItems] = useState<AssistantConversation[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let current = true;
    assistantApi
      .conversations()
      .then(({ data }) => current && setItems(data))
      .catch((err) => current && setError(getErrorMessage(err)));
    return () => {
      current = false;
    };
  }, []);

  if (error) return <Notice tone="danger" message={error} style={styles.notice} />;
  if (!items) return <ActivityIndicator color={colors.primary} style={styles.loading} />;

  return (
    <FlatList
      data={items}
      keyExtractor={(c) => String(c.id)}
      contentContainerStyle={styles.list}
      ListEmptyComponent={<Text style={styles.empty}>Chưa có hội thoại nào.</Text>}
      renderItem={({ item }) => (
        <Pressable
          onPress={() => {
            useAssistantStore.getState().open(item.id);
            router.back();
          }}
          accessibilityRole="button"
          testID={`assistant-conversation-${item.id}`}
          style={({ pressed }) => [styles.row, pressed && styles.pressed]}
        >
          <Ionicons name="chatbubble-ellipses-outline" size={20} color={colors.primary} />
          <View style={styles.text}>
            <Text style={styles.title} numberOfLines={1}>
              {item.title}
            </Text>
            <Text style={styles.date}>{formatDate(item.updatedAt)}</Text>
          </View>
          <Ionicons name="chevron-forward" size={18} color={colors.muted} />
        </Pressable>
      )}
    />
  );
}

const styles = StyleSheet.create({
  notice: { margin: 16 },
  loading: { marginTop: 32 },
  list: { padding: 16, gap: 8 },
  empty: { textAlign: 'center', color: colors.muted, marginTop: 32 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, padding: 14, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface },
  pressed: { opacity: 0.7 },
  text: { flex: 1, gap: 2 },
  title: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  date: { fontSize: 12, color: colors.muted },
});
