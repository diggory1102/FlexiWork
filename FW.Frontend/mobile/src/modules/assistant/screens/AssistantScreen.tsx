import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, FlatList, KeyboardAvoidingView, Platform, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { router, useNavigation } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Notice } from '@/components/ui/Feedback';
import { getErrorMessage } from '@/services/apiClient';
import { assistantApi, isAbortError, streamChat } from '@/services/assistantApi';
import { useAssistantStore } from '@/stores/assistantStore';
import type { DraftView } from '@/types/assistant';
import { colors, radius } from '@/theme';
import { DraftCard } from '../components/DraftCard';
import { SUGGESTIONS, toolLabel } from '../assistantUi';

const MAX_LENGTH = 1000;
/** Sau chừng này mà chưa có chữ nào → báo mô hình đang khởi động (nạp lạnh có thể mất 1–2 phút). */
const SLOW_AFTER_MS = 8_000;

interface Item {
  key: string;
  role: 'user' | 'assistant';
  content: string;
  drafts: DraftView[];
  /** Trợ lý đang trả lời: hiện chữ dần + dòng trạng thái. */
  streaming?: boolean;
  status?: string;
  failed?: boolean;
}

let keySeed = 0;
const nextKey = () => `m${++keySeed}`;

/**
 * Màn hình trò chuyện với trợ lý AI. Hội thoại đang mở lấy từ assistantStore (màn hình lịch sử / nút "mới" đặt giá trị này).
 * Gắn `key` theo lần chọn — hội thoại mới được máy chủ cấp mã giữa chừng không làm mất luồng đang chạy.
 */
export function AssistantScreen() {
  const conversationId = useAssistantStore((s) => s.conversationId);
  const nonce = useAssistantStore((s) => s.nonce);
  // Rời khỏi trợ lý (về trang chủ) → lần mở sau bắt đầu hội thoại mới
  useEffect(() => () => useAssistantStore.getState().open(null), []);
  return <AssistantThread key={nonce} initialConversationId={conversationId} />;
}

function AssistantThread({ initialConversationId }: { initialConversationId: number | null }) {
  const navigation = useNavigation();
  const insets = useSafeAreaInsets();
  const [items, setItems] = useState<Item[]>([]);
  const [loading, setLoading] = useState(initialConversationId != null);
  const [error, setError] = useState<string | null>(null);
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);
  const [slow, setSlow] = useState(false);
  const conversationId = useRef<number | null>(initialConversationId);
  const abort = useRef<AbortController | null>(null);
  const slowTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useLayoutEffect(() => {
    navigation.setOptions({
      headerRight: () => (
        <View style={styles.headerButtons}>
          <Pressable onPress={() => router.push('/assistant/history')} hitSlop={8} accessibilityRole="button" accessibilityLabel="Lịch sử hội thoại" testID="assistant-history" style={styles.headerButton}>
            <Ionicons name="time-outline" size={24} color={colors.foreground} />
          </Pressable>
          <Pressable
            onPress={() => useAssistantStore.getState().open(null)}
            hitSlop={8}
            accessibilityRole="button"
            accessibilityLabel="Hội thoại mới"
            testID="assistant-new"
            style={styles.headerButton}
          >
            <Ionicons name="create-outline" size={24} color={colors.foreground} />
          </Pressable>
        </View>
      ),
    });
  }, [navigation]);

  // Mở hội thoại cũ: tải toàn bộ tin
  useEffect(() => {
    if (initialConversationId == null) return;
    let current = true;
    assistantApi
      .messages(initialConversationId)
      .then(({ data }) => current && setItems(data.map((m) => ({ key: `s${m.id}`, role: m.role, content: m.content, drafts: m.drafts }))))
      .catch((err) => current && setError(getErrorMessage(err)))
      .finally(() => current && setLoading(false));
    return () => {
      current = false;
    };
  }, [initialConversationId]);

  useEffect(
    () => () => {
      abort.current?.abort();
      if (slowTimer.current) clearTimeout(slowTimer.current);
    },
    [],
  );

  const patchAssistant = useCallback((key: string, patch: Partial<Item> | ((item: Item) => Partial<Item>)) => {
    setItems((prev) => prev.map((m) => (m.key === key ? { ...m, ...(typeof patch === 'function' ? patch(m) : patch) } : m)));
  }, []);

  const clearSlow = () => {
    if (slowTimer.current) clearTimeout(slowTimer.current);
    slowTimer.current = null;
    setSlow(false);
  };

  const send = async (raw: string) => {
    const message = raw.trim();
    if (!message || sending) return;
    const assistantKey = nextKey();
    setError(null);
    setText('');
    setSending(true);
    setItems((prev) => [
      ...prev,
      { key: nextKey(), role: 'user', content: message, drafts: [] },
      { key: assistantKey, role: 'assistant', content: '', drafts: [], streaming: true, status: 'Đang suy nghĩ…' },
    ]);
    slowTimer.current = setTimeout(() => setSlow(true), SLOW_AFTER_MS);

    const controller = new AbortController();
    abort.current = controller;
    let finished = false;
    try {
      await streamChat(
        { conversationId: conversationId.current, message },
        (event) => {
          switch (event.type) {
            case 'conversation':
              conversationId.current = event.conversationId;
              break;
            case 'tool':
              clearSlow();
              patchAssistant(assistantKey, { status: toolLabel(event.name) });
              break;
            case 'delta':
              clearSlow();
              patchAssistant(assistantKey, (m) => ({ content: m.content + event.text, status: undefined }));
              break;
            case 'reset':
              patchAssistant(assistantKey, { content: '', status: 'Đang xử lý…' });
              break;
            case 'done':
              finished = true;
              clearSlow();
              // `reply` là nội dung chính thức; delta chỉ là bản xem trước
              patchAssistant(assistantKey, { content: event.reply.reply, drafts: event.reply.drafts, streaming: false, status: undefined });
              break;
            case 'error':
              finished = true;
              clearSlow();
              patchAssistant(assistantKey, { content: event.message, streaming: false, failed: true, status: undefined });
              break;
          }
        },
        controller.signal,
      );
      if (!finished) {
        patchAssistant(assistantKey, (m) => ({ streaming: false, failed: !m.content, content: m.content || 'Kết nối bị gián đoạn, vui lòng thử lại.', status: undefined }));
      }
    } catch (err) {
      if (isAbortError(err)) return;
      clearSlow();
      // Lỗi trước khi luồng mở (hết hạn mức, hội thoại không tồn tại…): bỏ bong bóng trống, báo ở khung nhập
      setItems((prev) => prev.filter((m) => m.key !== assistantKey));
      setError(getErrorMessage(err));
      setText(message);
    } finally {
      setSending(false);
    }
  };

  const updateDraft = (messageKey: string, draft: DraftView) =>
    patchAssistant(messageKey, (m) => ({ drafts: m.drafts.map((d) => (d.id === draft.id ? draft : d)) }));

  // Danh sách đảo ngược (tin mới nhất sát ô nhập, tự cuộn theo chữ đang hiện dần)
  const data = useMemo(() => [...items].reverse(), [items]);
  const empty = !loading && items.length === 0;

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={Platform.OS === 'ios' ? 90 : 0}>
      {loading ? (
        <ActivityIndicator color={colors.primary} style={styles.loading} />
      ) : empty ? (
        <View style={styles.empty}>
          <View style={styles.sparkle}>
            <Ionicons name="sparkles" size={26} color={colors.primary} />
          </View>
          <Text style={styles.emptyTitle}>Tôi có thể giúp gì cho bạn?</Text>
          <Text style={styles.emptyHint}>Hỏi về lịch ca, công, ngày phép, đơn từ — hoặc nhờ soạn đơn nghỉ, đổi ca, làm thêm giờ, giải trình.</Text>
          <View style={styles.chips}>
            {SUGGESTIONS.map((s) => (
              <Pressable key={s} onPress={() => void send(s)} accessibilityRole="button" style={styles.chip}>
                <Text style={styles.chipText}>{s}</Text>
              </Pressable>
            ))}
          </View>
        </View>
      ) : (
        <FlatList
          testID="assistant-list"
          data={data}
          inverted
          keyExtractor={(m) => m.key}
          contentContainerStyle={styles.list}
          keyboardShouldPersistTaps="handled"
          renderItem={({ item: m }) => (
            <View style={[styles.row, m.role === 'user' && styles.rowMine]}>
              {m.role === 'user' ? (
                <View style={[styles.bubble, styles.bubbleMine]}>
                  <Text style={[styles.text, styles.textMine]}>{m.content}</Text>
                </View>
              ) : (
                <View style={styles.assistantColumn}>
                  {(m.content || m.streaming) && (
                    <View style={[styles.bubble, styles.bubbleOther, m.failed && styles.bubbleFailed]}>
                      {m.content ? <Text style={styles.text}>{m.content}</Text> : null}
                      {m.streaming && (
                        <View style={styles.status}>
                          <ActivityIndicator size="small" color={colors.muted} />
                          <Text style={styles.statusText}>{m.status ?? 'Đang trả lời…'}</Text>
                        </View>
                      )}
                      {m.streaming && slow && !m.content ? <Text style={styles.statusText}>Mô hình đang khởi động, lần đầu có thể mất 1–2 phút.</Text> : null}
                    </View>
                  )}
                  {m.drafts.map((d) => (
                    <DraftCard key={d.id} draft={d} onChange={(draft) => updateDraft(m.key, draft)} />
                  ))}
                </View>
              )}
            </View>
          )}
        />
      )}

      <View style={[styles.composerWrap, { paddingBottom: Math.max(insets.bottom, 10) }]}>
        <Notice tone="danger" message={error} style={styles.error} />
        <View style={styles.composer}>
          <TextInput
            testID="assistant-input"
            style={styles.input}
            value={text}
            onChangeText={setText}
            placeholder="Hỏi trợ lý hoặc nhờ soạn đơn…"
            placeholderTextColor={colors.placeholder}
            multiline
            maxLength={MAX_LENGTH}
          />
          <Pressable
            testID="assistant-send"
            onPress={() => void send(text)}
            disabled={!text.trim() || sending}
            accessibilityRole="button"
            accessibilityLabel="Gửi"
            style={[styles.send, (!text.trim() || sending) && styles.sendDisabled]}
          >
            {sending ? <ActivityIndicator color={colors.onPrimary} /> : <Ionicons name="send" size={18} color={colors.onPrimary} />}
          </Pressable>
        </View>
        <Text style={styles.disclaimer}>Trợ lý có thể nhầm — hãy kiểm tra kỹ bản nháp trước khi xác nhận gửi đơn.</Text>
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  headerButtons: { flexDirection: 'row', alignItems: 'center' },
  headerButton: { paddingHorizontal: 8, paddingVertical: 6 },
  loading: { marginTop: 32 },
  empty: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 10, padding: 24 },
  sparkle: { width: 52, height: 52, borderRadius: radius.full, backgroundColor: colors.primarySoft, alignItems: 'center', justifyContent: 'center' },
  emptyTitle: { fontSize: 18, fontWeight: '700', color: colors.foreground },
  emptyHint: { textAlign: 'center', fontSize: 14, lineHeight: 20, color: colors.muted },
  chips: { gap: 8, marginTop: 8, alignSelf: 'stretch' },
  chip: { paddingHorizontal: 14, paddingVertical: 10, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface },
  chipText: { fontSize: 14, color: colors.foreground },
  list: { padding: 12, gap: 10 },
  row: { flexDirection: 'row' },
  rowMine: { justifyContent: 'flex-end', paddingLeft: 48 },
  assistantColumn: { flex: 1, gap: 8, paddingRight: 24, alignItems: 'flex-start' },
  bubble: { borderRadius: 18, paddingHorizontal: 12, paddingVertical: 8, gap: 6 },
  bubbleMine: { flexShrink: 1, backgroundColor: colors.primary, borderBottomRightRadius: 4 },
  bubbleOther: { backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border, borderBottomLeftRadius: 4, maxWidth: '100%' },
  bubbleFailed: { borderColor: colors.danger },
  text: { fontSize: 15, lineHeight: 21, color: colors.foreground },
  textMine: { color: colors.onPrimary },
  status: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  statusText: { fontSize: 13, color: colors.muted },
  composerWrap: { paddingHorizontal: 10, paddingTop: 8, gap: 6, backgroundColor: colors.surface, borderTopWidth: 1, borderTopColor: colors.border },
  error: { marginBottom: 2 },
  composer: { flexDirection: 'row', alignItems: 'flex-end', gap: 8 },
  input: { flex: 1, maxHeight: 110, minHeight: 40, paddingHorizontal: 14, paddingTop: 10, paddingBottom: 10, borderRadius: 20, backgroundColor: colors.background, fontSize: 15, color: colors.foreground },
  send: { width: 40, height: 40, borderRadius: radius.full, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' },
  sendDisabled: { opacity: 0.45 },
  disclaimer: { textAlign: 'center', fontSize: 11, color: colors.muted },
});
