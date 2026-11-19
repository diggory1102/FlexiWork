import { useCallback, useEffect, useRef, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import { TextField } from '@/components/ui/TextField';
import { Notice } from '@/components/ui/Feedback';
import { chatApi } from '@/services/chatApi';
import { getErrorMessage } from '@/services/apiClient';
import { colors, radius } from '@/theme';
import type { ChatContact } from '@/types/chat';

interface ContactListProps {
  /** Mã người đang được chọn (chọn nhiều); bỏ qua ở chế độ chọn một. */
  selectedIds?: number[];
  /** Người không chọn được (đã ở trong nhóm). */
  disabledIds?: number[];
  onPress: (contact: ChatContact) => void;
  /** Phần trên danh sách (VD: ô nhập tên nhóm). */
  header?: React.ReactElement;
}

const PAGE_SIZE = 30;

/** Danh bạ chat: tìm theo tên / mã nhân viên, cuộn để tải thêm; dùng cho chat mới, tạo nhóm và thêm thành viên. */
export function ContactList({ selectedIds, disabledIds = [], onPress, header }: ContactListProps) {
  const [search, setSearch] = useState('');
  const [items, setItems] = useState<ChatContact[] | null>(null);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const seq = useRef(0);

  const load = useCallback(async (term: string, pageIndex: number) => {
    const mySeq = ++seq.current;
    try {
      const { data } = await chatApi.contacts({ search: term.trim() || undefined, pageIndex, pageSize: PAGE_SIZE });
      if (mySeq !== seq.current) return;
      setItems((prev) => (pageIndex === 1 ? data.items : [...(prev ?? []), ...data.items]));
      setPage(pageIndex);
      setHasMore(data.hasNextPage);
      setError(null);
    } catch (err) {
      if (mySeq !== seq.current) return;
      setError(getErrorMessage(err));
      setItems((prev) => prev ?? []);
    } finally {
      if (mySeq === seq.current) setLoadingMore(false);
    }
  }, []);

  // Gõ tìm kiếm: chờ ngừng gõ 300 ms rồi mới gọi
  useEffect(() => {
    const timer = setTimeout(() => void load(search, 1), search ? 300 : 0);
    return () => clearTimeout(timer);
  }, [search, load]);

  return (
    <FlatList
      data={items ?? []}
      keyExtractor={(c) => String(c.id)}
      keyboardShouldPersistTaps="handled"
      contentContainerStyle={styles.content}
      ListHeaderComponent={
        <View style={styles.header}>
          {header}
          <TextField icon="search-outline" placeholder="Tìm theo tên hoặc mã nhân viên" value={search} onChangeText={setSearch} autoCapitalize="none" testID="contact-search" />
          <Notice tone="danger" message={error} />
        </View>
      }
      ListEmptyComponent={
        items === null ? <ActivityIndicator color={colors.primary} style={styles.loading} /> : <Text style={styles.empty}>Không tìm thấy ai phù hợp.</Text>
      }
      ListFooterComponent={loadingMore ? <ActivityIndicator color={colors.primary} style={styles.loading} /> : undefined}
      onEndReachedThreshold={0.4}
      onEndReached={() => {
        if (hasMore && !loadingMore) {
          setLoadingMore(true);
          void load(search, page + 1);
        }
      }}
      renderItem={({ item: c }) => {
        const disabled = disabledIds.includes(c.id);
        const selected = !!selectedIds?.includes(c.id);
        return (
          <Pressable
            testID={`contact-${c.id}`}
            disabled={disabled}
            onPress={() => onPress(c)}
            accessibilityRole="button"
            accessibilityState={{ selected, disabled }}
            style={({ pressed }) => [styles.row, selected && styles.rowSelected, disabled && styles.rowDisabled, pressed && styles.pressed]}
          >
            <Avatar name={c.fullName} url={c.avatarUrl} size={42} />
            <View style={styles.flex}>
              <Text style={styles.name} numberOfLines={1}>
                {c.fullName}
              </Text>
              <Text style={styles.meta} numberOfLines={1}>
                {[c.employeeCode, c.positionTitle, c.branchName].filter(Boolean).join(' · ')}
              </Text>
            </View>
            {selectedIds && (
              <Ionicons
                name={disabled ? 'checkmark-done' : selected ? 'checkbox' : 'square-outline'}
                size={22}
                color={disabled ? colors.muted : selected ? colors.primary : colors.border}
              />
            )}
          </Pressable>
        );
      }}
    />
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 32, gap: 8, flexGrow: 1 },
  header: { gap: 12, marginBottom: 4 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 10,
  },
  rowSelected: { backgroundColor: colors.primarySoft, borderColor: colors.primary },
  rowDisabled: { opacity: 0.55 },
  name: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  meta: { marginTop: 2, fontSize: 12, color: colors.muted },
  empty: { textAlign: 'center', color: colors.muted, marginTop: 24 },
  loading: { marginVertical: 20 },
  pressed: { opacity: 0.85 },
});
