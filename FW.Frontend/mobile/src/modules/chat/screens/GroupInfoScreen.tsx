import { useCallback, useState } from 'react';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router, useFocusEffect, useLocalSearchParams } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import { Button } from '@/components/ui/Button';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { chatApi } from '@/services/chatApi';
import { getErrorMessage } from '@/services/apiClient';
import { confirmAction } from '@/lib/dialog';
import { useAuthStore } from '@/stores/authStore';
import { useChatStore } from '@/stores/chatStore';
import { colors } from '@/theme';
import { MAX_GROUP_TITLE_LENGTH, type ConversationDetail } from '@/types/chat';
import { ChatAvatar } from '../components/ChatAvatar';

/** Thông tin nhóm (UC 7.2): quản trị nhóm đổi tên / thêm / xóa thành viên; ai cũng rời được nhóm tự tạo. Nhóm chi nhánh chỉ xem. */
export function GroupInfoScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const conversationId = Number(id);
  const myId = useAuthStore((s) => s.user?.id ?? 0);
  const [detail, setDetail] = useState<ConversationDetail | null>(null);
  const [title, setTitle] = useState('');
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (refresh = false) => {
      if (refresh) setRefreshing(true);
      try {
        const { data } = await chatApi.conversation(conversationId);
        setDetail(data);
        setTitle((prev) => (editing ? prev : data.conversation.title));
        setError(null);
      } catch (err) {
        setError(getErrorMessage(err));
      } finally {
        setRefreshing(false);
      }
    },
    [conversationId, editing],
  );

  // Quay lại từ màn "Thêm thành viên" thì nạp lại danh sách
  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load]),
  );

  if (!detail) {
    return <View style={styles.center}>{error ? <Notice tone="danger" message={error} /> : <ActivityIndicator color={colors.primary} />}</View>;
  }

  const { conversation: c, members } = detail;
  const isOwner = c.isCustomGroup && c.ownerId === myId;

  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      return true;
    } catch (err) {
      setError(getErrorMessage(err));
      return false;
    } finally {
      setBusy(false);
    }
  };

  const rename = async () => {
    const name = title.trim();
    if (name.length < 2) return setError('Tên nhóm từ 2 đến 100 ký tự.');
    const ok = await run(async () => {
      const { data } = await chatApi.renameGroup(conversationId, name);
      useChatStore.getState().upsert(data);
      setDetail((prev) => (prev ? { ...prev, conversation: data } : prev));
    });
    if (ok) setEditing(false);
  };

  const remove = (userId: number, name: string) =>
    confirmAction('Xóa khỏi nhóm?', `${name} sẽ không còn thấy nhóm này nữa.`, 'Xóa', () => {
      void run(async () => {
        await chatApi.removeMember(conversationId, userId);
        await load();
      });
    }, true);

  const leave = () =>
    confirmAction('Rời nhóm?', 'Bạn sẽ không còn nhận tin nhắn của nhóm này.', 'Rời nhóm', () => {
      void (async () => {
        const ok = await run(() => chatApi.removeMember(conversationId, myId));
        if (!ok) return;
        await useChatStore.getState().load().catch(() => undefined);
        router.dismiss(2); // đóng thông tin nhóm + cuộc trò chuyện → về danh sách phòng đang có sẵn trong ngăn xếp
      })();
    }, true);

  return (
    <ScrollView contentContainerStyle={styles.content} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void load(true)} />}>
      <Notice tone="danger" message={error} />

      <View style={styles.hero}>
        <ChatAvatar conversation={c} size={72} />
        {editing ? (
          <View style={styles.editRow}>
            <View style={styles.flex}>
              <TextField value={title} onChangeText={setTitle} maxLength={MAX_GROUP_TITLE_LENGTH} testID="group-rename-input" autoFocus />
            </View>
            <Button title="Lưu" onPress={() => void rename()} loading={busy} style={styles.saveButton} testID="group-rename-save" />
          </View>
        ) : (
          <View style={styles.nameRow}>
            <Text style={styles.name} testID="group-name">
              {c.title}
            </Text>
            {isOwner && (
              <Pressable onPress={() => setEditing(true)} hitSlop={10} accessibilityLabel="Đổi tên nhóm" testID="group-rename">
                <Ionicons name="create-outline" size={20} color={colors.primary} />
              </Pressable>
            )}
          </View>
        )}
        <Text style={styles.sub}>
          {c.memberCount} thành viên · {c.isCustomGroup ? 'Nhóm tự tạo' : 'Nhóm chi nhánh'}
        </Text>
      </View>

      {!c.isCustomGroup && <Notice tone="info" message="Nhóm chi nhánh do hệ thống quản lý: thành viên là toàn bộ nhân sự đang làm tại chi nhánh." />}

      {isOwner && (
        <Button
          testID="group-add-members"
          title="Thêm thành viên"
          variant="secondary"
          icon={<Ionicons name="person-add-outline" size={18} color={colors.foreground} />}
          onPress={() => router.push({ pathname: '/chat/new', params: { mode: 'add', conversationId: String(conversationId) } })}
        />
      )}

      <Text style={styles.section}>Thành viên</Text>
      <Card style={styles.members}>
        {members.map((m, index) => (
          <View key={m.userId} style={[styles.member, index > 0 && styles.memberBorder]} testID={`member-${m.userId}`}>
            <Avatar name={m.fullName} url={m.avatarUrl} size={40} />
            <View style={styles.flex}>
              <Text style={styles.memberName} numberOfLines={1}>
                {m.fullName}
                {m.userId === myId ? ' (Bạn)' : ''}
              </Text>
              <Text style={styles.memberMeta} numberOfLines={1}>
                {[m.employeeCode, m.positionTitle].filter(Boolean).join(' · ')}
              </Text>
            </View>
            {c.isCustomGroup && m.userId === c.ownerId && <Badge label="Quản trị" tone="info" />}
            {isOwner && m.userId !== myId && (
              <Pressable onPress={() => remove(m.userId, m.fullName)} hitSlop={8} accessibilityLabel={`Xóa ${m.fullName}`} testID={`member-remove-${m.userId}`}>
                <Ionicons name="close-circle-outline" size={22} color={colors.danger} />
              </Pressable>
            )}
          </View>
        ))}
      </Card>

      {c.isCustomGroup && <Button testID="group-leave" title="Rời nhóm" variant="danger" onPress={leave} loading={busy} />}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  center: { flex: 1, justifyContent: 'center', padding: 20 },
  content: { padding: 16, paddingBottom: 32, gap: 14 },
  hero: { alignItems: 'center', gap: 8, paddingVertical: 8 },
  nameRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  name: { fontSize: 20, fontWeight: '800', color: colors.foreground },
  editRow: { flexDirection: 'row', alignItems: 'center', gap: 8, alignSelf: 'stretch' },
  saveButton: { height: 48, paddingHorizontal: 18 },
  sub: { fontSize: 13, color: colors.muted },
  section: { marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  members: { padding: 0 },
  member: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 14, paddingVertical: 10 },
  memberBorder: { borderTopWidth: 1, borderTopColor: colors.border },
  memberName: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  memberMeta: { marginTop: 2, fontSize: 12, color: colors.muted },
});
