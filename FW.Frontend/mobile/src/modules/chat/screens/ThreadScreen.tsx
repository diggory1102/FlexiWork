import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, FlatList, Image, KeyboardAvoidingView, Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { router, useLocalSearchParams, useNavigation } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import { Notice } from '@/components/ui/Feedback';
import { chatApi } from '@/services/chatApi';
import { getErrorMessage } from '@/services/apiClient';
import { confirmAction } from '@/lib/dialog';
import { pickImage } from '@/lib/pickImage';
import { formatDayLabel, formatTime } from '@/lib/format';
import { subscribeChat, useChatStore } from '@/stores/chatStore';
import { useAuthStore } from '@/stores/authStore';
import { colors, radius } from '@/theme';
import { MAX_MESSAGE_LENGTH, type ChatMessage, type Conversation } from '@/types/chat';

const PAGE_SIZE = 30;

/** Một cuộc trò chuyện: tin mới tới tức thì (SignalR), cuộn lên để xem tin cũ, gửi chữ / ảnh, thu hồi tin của mình. */
export function ThreadScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const conversationId = Number(id);
  const navigation = useNavigation();
  const insets = useSafeAreaInsets();
  const myId = useAuthStore((s) => s.user?.id ?? 0);
  const cached = useChatStore((s) => s.conversations.find((c) => c.id === conversationId));
  const [conversation, setConversation] = useState<Conversation | null>(cached ?? null);

  // Từ mới → cũ (danh sách đảo ngược: tin mới nhất nằm sát ô nhập)
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [nextBeforeId, setNextBeforeId] = useState<number | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const markRead = useCallback(() => {
    void chatApi
      .markRead(conversationId)
      .then(({ data }) => useChatStore.getState().setUnread(conversationId, data.unreadCount))
      .catch(() => undefined);
  }, [conversationId]);

  const mergeIncoming = useCallback((incoming: ChatMessage) => {
    setMessages((prev) => (prev.some((m) => m.id === incoming.id) ? prev : [incoming, ...prev]));
  }, []);

  // Mở phòng: đánh dấu phòng đang xem, tải trang tin mới nhất, báo đã đọc
  useEffect(() => {
    useChatStore.getState().setActive(conversationId);
    let cancelled = false;
    (async () => {
      try {
        const [{ data: detail }, { data: page }] = await Promise.all([chatApi.conversation(conversationId), chatApi.messages(conversationId, null, PAGE_SIZE)]);
        if (cancelled) return;
        setConversation(detail.conversation);
        setMessages([...page.items].reverse());
        setHasMore(page.hasMore);
        setNextBeforeId(page.nextBeforeId);
        setError(null);
        markRead();
      } catch (err) {
        if (!cancelled) setError(getErrorMessage(err));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
      useChatStore.getState().setActive(null);
    };
  }, [conversationId, markRead]);

  // Tin mới / tin bị thu hồi đẩy về khi đang mở phòng
  useEffect(
    () =>
      subscribeChat((event) => {
        if (event.kind === 'message' && event.message.conversationId === conversationId) {
          mergeIncoming(event.message);
          if (event.message.senderId !== myId) markRead();
        } else if (event.kind === 'deleted' && event.event.conversationId === conversationId) {
          setMessages((prev) => prev.map((m) => (m.id === event.event.messageId ? { ...m, isDeleted: true, content: null, imageUrl: null } : m)));
        }
      }),
    [conversationId, myId, mergeIncoming, markRead],
  );

  const isGroup = conversation?.type === 'group';
  useLayoutEffect(() => {
    navigation.setOptions({
      title: conversation?.title ?? 'Cuộc trò chuyện',
      headerRight: () =>
        isGroup ? (
          <Pressable
            testID="chat-info"
            onPress={() => router.push({ pathname: '/chat/info/[id]', params: { id: String(conversationId) } })}
            hitSlop={8}
            accessibilityRole="button"
            accessibilityLabel="Thông tin nhóm"
            style={styles.headerButton}
          >
            <Ionicons name="information-circle-outline" size={24} color={colors.foreground} />
          </Pressable>
        ) : null,
    });
  }, [navigation, conversation?.title, isGroup, conversationId]);

  const loadOlder = async () => {
    if (!hasMore || loadingMore || nextBeforeId == null) return;
    setLoadingMore(true);
    try {
      const { data } = await chatApi.messages(conversationId, nextBeforeId, PAGE_SIZE);
      setMessages((prev) => [...prev, ...[...data.items].reverse().filter((m) => !prev.some((p) => p.id === m.id))]);
      setHasMore(data.hasMore);
      setNextBeforeId(data.nextBeforeId);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoadingMore(false);
    }
  };

  const sendText = async () => {
    const content = draft.trim();
    if (!content || sending) return;
    setSending(true);
    setError(null);
    try {
      const { data } = await chatApi.sendText(conversationId, content);
      setDraft('');
      mergeIncoming(data);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  };

  const sendImage = async (source: 'camera' | 'library') => {
    if (sending) return;
    const picked = await pickImage(source);
    if (!picked) return;
    if (!picked.ok) return setError(picked.message);
    setSending(true);
    setError(null);
    try {
      const { data } = await chatApi.sendImage(conversationId, picked.image, draft.trim());
      setDraft('');
      mergeIncoming(data);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  };

  const recall = (message: ChatMessage) => {
    confirmAction('Thu hồi tin nhắn?', 'Mọi người trong cuộc trò chuyện sẽ không còn thấy nội dung tin này.', 'Thu hồi', () => {
      setSelectedId(null);
      chatApi
        .deleteMessage(message.id)
        .then(() => setMessages((prev) => prev.map((m) => (m.id === message.id ? { ...m, isDeleted: true, content: null, imageUrl: null } : m))))
        .catch((err) => setError(getErrorMessage(err)));
    }, true);
  };

  // Dòng ngày: hiện khi tin này khác ngày với tin cũ hơn liền kề (danh sách đảo ngược nên "cũ hơn" là phần tử kế tiếp)
  const dayLabels = useMemo(() => {
    const labels = new Map<number, string>();
    messages.forEach((m, i) => {
      const older = messages[i + 1];
      if (!older || older.createdAt.slice(0, 10) !== m.createdAt.slice(0, 10)) labels.set(m.id, formatDayLabel(m.createdAt));
    });
    return labels;
  }, [messages]);

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={Platform.OS === 'ios' ? 90 : 0}>
      <Notice tone="danger" message={error} style={styles.error} />
      {loading ? (
        <ActivityIndicator color={colors.primary} style={styles.loading} />
      ) : (
        <FlatList
          testID="message-list"
          data={messages}
          inverted
          keyExtractor={(m) => String(m.id)}
          contentContainerStyle={styles.list}
          keyboardShouldPersistTaps="handled"
          onEndReachedThreshold={0.3}
          onEndReached={() => void loadOlder()}
          ListFooterComponent={loadingMore ? <ActivityIndicator color={colors.primary} style={styles.loadingMore} /> : undefined}
          ListEmptyComponent={<Text style={styles.empty}>Chưa có tin nhắn. Hãy gửi lời chào đầu tiên!</Text>}
          renderItem={({ item }) => (
            <MessageBubble
              message={item}
              mine={item.senderId === myId}
              showSender={isGroup && item.senderId !== myId}
              dayLabel={dayLabels.get(item.id)}
              selected={selectedId === item.id}
              onPress={() => setSelectedId((cur) => (cur === item.id ? null : item.id))}
              onRecall={() => recall(item)}
            />
          )}
        />
      )}

      <View style={[styles.composer, { paddingBottom: Math.max(insets.bottom, 10) }]}>
        <Pressable onPress={() => void sendImage('library')} hitSlop={6} disabled={sending} accessibilityLabel="Gửi ảnh từ thư viện" testID="chat-image" style={styles.iconButton}>
          <Ionicons name="image-outline" size={24} color={colors.primary} />
        </Pressable>
        {Platform.OS !== 'web' && (
          <Pressable onPress={() => void sendImage('camera')} hitSlop={6} disabled={sending} accessibilityLabel="Chụp ảnh gửi" style={styles.iconButton}>
            <Ionicons name="camera-outline" size={24} color={colors.primary} />
          </Pressable>
        )}
        <TextInput
          testID="chat-input"
          style={styles.input}
          value={draft}
          onChangeText={setDraft}
          placeholder="Nhập tin nhắn…"
          placeholderTextColor="#94A3B8"
          multiline
          maxLength={MAX_MESSAGE_LENGTH}
        />
        <Pressable
          testID="chat-send"
          onPress={() => void sendText()}
          disabled={!draft.trim() || sending}
          accessibilityRole="button"
          accessibilityLabel="Gửi"
          style={[styles.send, (!draft.trim() || sending) && styles.sendDisabled]}
        >
          {sending ? <ActivityIndicator color={colors.white} /> : <Ionicons name="send" size={18} color={colors.white} />}
        </Pressable>
      </View>
    </KeyboardAvoidingView>
  );
}

function MessageBubble({ message: m, mine, showSender, dayLabel, selected, onPress, onRecall }: {
  message: ChatMessage;
  mine: boolean;
  showSender: boolean;
  dayLabel?: string;
  selected: boolean;
  onPress: () => void;
  onRecall: () => void;
}) {
  return (
    <View>
      {dayLabel ? <Text style={styles.day}>{dayLabel}</Text> : null}
      <View style={[styles.row, mine && styles.rowMine]}>
        {!mine && <Avatar name={m.senderName} url={m.senderAvatarUrl} size={30} />}
        <View style={[styles.column, mine && styles.columnMine]}>
          {showSender && <Text style={styles.sender}>{m.senderName}</Text>}
          <Pressable onPress={onPress} accessibilityRole="button" testID={`message-${m.id}`} style={[styles.bubble, mine ? styles.bubbleMine : styles.bubbleOther, m.isDeleted && styles.bubbleDeleted, !!m.imageUrl && !m.isDeleted && styles.bubbleImage]}>
            {m.isDeleted ? (
              <Text style={styles.deleted}>Tin nhắn đã được thu hồi</Text>
            ) : (
              <>
                {m.imageUrl ? <Image source={{ uri: m.imageUrl }} style={styles.image} resizeMode="cover" accessibilityLabel="Ảnh trong tin nhắn" /> : null}
                {m.content ? <Text style={[styles.text, mine && styles.textMine]}>{m.content}</Text> : null}
              </>
            )}
          </Pressable>
          {selected && (
            <View style={styles.meta}>
              <Text style={styles.time}>{formatTime(m.createdAt)}</Text>
              {mine && !m.isDeleted && (
                <Pressable onPress={onRecall} hitSlop={8} testID={`recall-${m.id}`} accessibilityRole="button">
                  <Text style={styles.recall}>Thu hồi</Text>
                </Pressable>
              )}
            </View>
          )}
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  headerButton: { paddingHorizontal: 12, paddingVertical: 6 },
  error: { margin: 12, marginBottom: 0 },
  loading: { marginTop: 32 },
  loadingMore: { marginVertical: 12 },
  list: { padding: 12, gap: 6 },
  empty: { textAlign: 'center', color: colors.muted, marginVertical: 24, transform: [{ scaleY: -1 }] },
  day: { alignSelf: 'center', marginVertical: 10, fontSize: 12, fontWeight: '600', color: colors.muted },
  row: { flexDirection: 'row', alignItems: 'flex-end', gap: 8, marginVertical: 2, paddingRight: 48 },
  rowMine: { justifyContent: 'flex-end', paddingRight: 0, paddingLeft: 48 },
  column: { flexShrink: 1, alignItems: 'flex-start' },
  columnMine: { alignItems: 'flex-end' },
  sender: { marginBottom: 2, marginLeft: 4, fontSize: 12, color: colors.muted },
  bubble: { borderRadius: 18, paddingHorizontal: 12, paddingVertical: 8, overflow: 'hidden', gap: 6 },
  bubbleMine: { backgroundColor: colors.primary, borderBottomRightRadius: 4 },
  bubbleOther: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderBottomLeftRadius: 4 },
  bubbleImage: { paddingHorizontal: 4, paddingVertical: 4 },
  bubbleDeleted: { backgroundColor: colors.background, borderWidth: 1, borderColor: colors.border },
  text: { fontSize: 15, lineHeight: 21, color: colors.foreground },
  textMine: { color: colors.white },
  deleted: { fontSize: 14, fontStyle: 'italic', color: colors.muted },
  image: { width: 220, height: 220, borderRadius: 12, backgroundColor: colors.border },
  meta: { flexDirection: 'row', gap: 12, marginTop: 3, marginHorizontal: 4 },
  time: { fontSize: 11, color: colors.muted },
  recall: { fontSize: 11, fontWeight: '700', color: colors.danger },
  composer: { flexDirection: 'row', alignItems: 'flex-end', gap: 8, paddingHorizontal: 10, paddingTop: 8, backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.border },
  iconButton: { height: 40, justifyContent: 'center' },
  input: {
    flex: 1,
    maxHeight: 110,
    minHeight: 40,
    paddingHorizontal: 14,
    paddingTop: 10,
    paddingBottom: 10,
    borderRadius: 20,
    backgroundColor: colors.background,
    fontSize: 15,
    color: colors.foreground,
  },
  send: { width: 40, height: 40, borderRadius: radius.full, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' },
  sendDisabled: { opacity: 0.45 },
});
