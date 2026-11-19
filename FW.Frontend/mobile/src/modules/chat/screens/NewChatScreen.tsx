import { useEffect, useLayoutEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { router, useLocalSearchParams, useNavigation } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { Notice } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { chatApi } from '@/services/chatApi';
import { getErrorMessage } from '@/services/apiClient';
import { useChatStore } from '@/stores/chatStore';
import { colors } from '@/theme';
import { MAX_GROUP_TITLE_LENGTH } from '@/types/chat';
import { ContactList } from '../components/ContactList';

type Mode = 'direct' | 'group' | 'add';

const titles: Record<Mode, string> = { direct: 'Nhắn tin mới', group: 'Tạo nhóm', add: 'Thêm thành viên' };

/**
 * Chọn người để chat: `direct` (chạm một người), `group` (đặt tên + chọn nhiều người), `add` (thêm vào nhóm `conversationId`).
 */
export function NewChatScreen() {
  const params = useLocalSearchParams<{ mode?: string; conversationId?: string }>();
  const mode: Mode = params.mode === 'group' || params.mode === 'add' ? params.mode : 'direct';
  const conversationId = Number(params.conversationId);
  const navigation = useNavigation();

  const [selected, setSelected] = useState<number[]>([]);
  const [existing, setExisting] = useState<number[]>([]);
  const [title, setTitle] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useLayoutEffect(() => {
    navigation.setOptions({ title: titles[mode] });
  }, [navigation, mode]);

  // Thêm vào nhóm: đánh dấu những người đã ở trong nhóm
  useEffect(() => {
    if (mode !== 'add') return;
    chatApi
      .conversation(conversationId)
      .then(({ data }) => setExisting(data.members.map((m) => m.userId)))
      .catch((err) => setError(getErrorMessage(err)));
  }, [mode, conversationId]);

  const open = async (userId: number) => {
    setBusy(true);
    try {
      const { data } = await chatApi.openDirect(userId);
      useChatStore.getState().upsert(data);
      router.replace({ pathname: '/chat/[id]', params: { id: String(data.id) } });
    } catch (err) {
      setError(getErrorMessage(err));
      setBusy(false);
    }
  };

  const submit = async () => {
    setError(null);
    if (mode === 'group') {
      const name = title.trim();
      if (name.length < 2) return setError('Tên nhóm từ 2 đến 100 ký tự.');
      if (selected.length === 0) return setError('Chọn ít nhất một thành viên.');
    }
    setBusy(true);
    try {
      if (mode === 'group') {
        const { data } = await chatApi.createGroup(title.trim(), selected);
        useChatStore.getState().upsert(data);
        router.replace({ pathname: '/chat/[id]', params: { id: String(data.id) } });
      } else {
        await chatApi.addMembers(conversationId, selected);
        await useChatStore.getState().load().catch(() => undefined);
        router.back();
      }
    } catch (err) {
      setError(getErrorMessage(err));
      setBusy(false);
    }
  };

  const toggle = (id: number) => setSelected((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));

  return (
    <View style={styles.flex}>
      <ContactList
        selectedIds={mode === 'direct' ? undefined : selected}
        disabledIds={mode === 'add' ? existing : []}
        onPress={(c) => (mode === 'direct' ? !busy && void open(c.id) : toggle(c.id))}
        header={
          <>
            {mode === 'group' && (
              <TextField
                label="Tên nhóm"
                placeholder="VD: Ca sáng quầy 1"
                value={title}
                onChangeText={setTitle}
                maxLength={MAX_GROUP_TITLE_LENGTH}
                testID="group-title"
              />
            )}
            <Notice tone="danger" message={error} />
          </>
        }
      />
      {mode !== 'direct' && (
        <View style={styles.footer}>
          <Button
            testID="chat-submit"
            title={mode === 'group' ? `Tạo nhóm (${selected.length} người)` : `Thêm ${selected.length} người`}
            disabled={selected.length === 0}
            loading={busy}
            onPress={() => void submit()}
          />
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  footer: { padding: 16, borderTopWidth: 1, borderTopColor: colors.border, backgroundColor: colors.surface },
});
