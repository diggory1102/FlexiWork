import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Badge, Notice } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { pickImage } from '@/lib/pickImage';
import { getErrorMessage } from '@/services/apiClient';
import { assistantApi } from '@/services/assistantApi';
import type { PickedImage } from '@/services/multipart';
import type { DraftView } from '@/types/assistant';
import { colors, radius } from '@/theme';
import { DRAFT_KINDS, DRAFT_STATUS_TEXT, effectiveDraftStatus } from '../assistantUi';

const STATUS_TONE = { pending: 'info', confirmed: 'success', cancelled: 'info', expired: 'warning' } as const;

/**
 * Thẻ bản nháp đơn do trợ lý soạn. Đơn CHỈ được gửi khi người dùng bấm "Xác nhận gửi đơn" (qua đúng luồng kiểm tra của đơn tạo tay);
 * loại đơn yêu cầu minh chứng thì phải chọn ảnh trước khi xác nhận.
 */
export function DraftCard({ draft, onChange }: { draft: DraftView; onChange: (draft: DraftView) => void }) {
  const [busy, setBusy] = useState<'confirm' | 'cancel' | null>(null);
  const [image, setImage] = useState<PickedImage | null>(null);
  const [error, setError] = useState<string | null>(null);

  const status = effectiveDraftStatus(draft);
  const pending = status === 'pending';
  const meta = DRAFT_KINDS[draft.kind];

  const choose = async (source: 'camera' | 'library') => {
    const picked = await pickImage(source);
    if (!picked) return;
    if (!picked.ok) return setError(picked.message);
    setError(null);
    setImage(picked.image);
  };

  const confirm = async () => {
    if (draft.requiresAttachment && !image) return setError('Loại đơn này cần kèm ảnh minh chứng (VD: giấy khám bệnh).');
    setBusy('confirm');
    setError(null);
    try {
      const { data } = await assistantApi.confirmDraft(draft.id, image);
      onChange(data.draft);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(null);
    }
  };

  const cancel = async () => {
    setBusy('cancel');
    setError(null);
    try {
      onChange((await assistantApi.cancelDraft(draft.id)).data);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setBusy(null);
    }
  };

  return (
    <View style={styles.card} testID="draft-card">
      <View style={styles.header}>
        <View style={styles.title}>
          <Ionicons name={meta?.icon ?? 'document-outline'} size={18} color={colors.primary} />
          <Text style={styles.titleText}>{meta?.label ?? draft.title}</Text>
        </View>
        <Badge label={DRAFT_STATUS_TEXT[status]} tone={STATUS_TONE[status]} />
      </View>

      <View style={styles.body}>
        {draft.lines.map((line, i) => (
          <View key={`${line.label}-${i}`} style={styles.line}>
            <Text style={styles.label}>{line.label}</Text>
            <Text style={styles.value}>{line.value}</Text>
          </View>
        ))}

        {draft.warnings.map((w) => (
          <Notice key={w} tone="warning" message={w} />
        ))}

        {pending && draft.requiresAttachment && (
          <View style={styles.attach}>
            <Pressable onPress={() => void choose('library')} accessibilityRole="button" testID="draft-pick-image" style={styles.attachButton}>
              <Ionicons name="image-outline" size={18} color={colors.primary} />
              <Text style={styles.attachText}>{image ? 'Đổi ảnh' : 'Chọn ảnh minh chứng'}</Text>
            </Pressable>
            <Pressable onPress={() => void choose('camera')} accessibilityRole="button" accessibilityLabel="Chụp ảnh minh chứng" style={styles.attachButton}>
              <Ionicons name="camera-outline" size={18} color={colors.primary} />
            </Pressable>
            {image ? <Text style={styles.fileName} numberOfLines={1}>{image.fileName ?? 'minh-chung.jpg'}</Text> : null}
          </View>
        )}

        <Notice tone="danger" message={error} />

        {pending ? (
          <View style={styles.actions}>
            <Button title="Hủy" variant="secondary" onPress={() => void cancel()} loading={busy === 'cancel'} disabled={busy !== null} style={styles.action} testID="draft-cancel" />
            <Button title="Xác nhận gửi đơn" onPress={() => void confirm()} loading={busy === 'confirm'} disabled={busy !== null} style={styles.actionWide} testID="draft-confirm" />
          </View>
        ) : status === 'confirmed' ? (
          <View style={styles.done}>
            <Ionicons name="checkmark-circle" size={18} color={colors.success} />
            <Text style={styles.doneText}>Đơn đã được gửi và đang chờ duyệt.</Text>
          </View>
        ) : (
          <Text style={styles.doneText}>{status === 'expired' ? 'Bản nháp đã hết hạn — hãy nhờ trợ lý soạn lại.' : 'Bạn đã hủy bản nháp này.'}</Text>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: { alignSelf: 'stretch', borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface, overflow: 'hidden' },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8, paddingHorizontal: 12, paddingVertical: 10, backgroundColor: colors.background, borderBottomWidth: 1, borderBottomColor: colors.border },
  title: { flexDirection: 'row', alignItems: 'center', gap: 6, flexShrink: 1 },
  titleText: { fontSize: 15, fontWeight: '700', color: colors.foreground },
  body: { padding: 12, gap: 8 },
  line: { flexDirection: 'row', gap: 12 },
  label: { width: 96, fontSize: 13, color: colors.muted },
  value: { flex: 1, fontSize: 14, fontWeight: '600', color: colors.foreground },
  attach: { flexDirection: 'row', alignItems: 'center', gap: 8, flexWrap: 'wrap' },
  attachButton: { flexDirection: 'row', alignItems: 'center', gap: 6, height: 36, paddingHorizontal: 12, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border },
  attachText: { fontSize: 13, fontWeight: '600', color: colors.primary },
  fileName: { flexShrink: 1, fontSize: 12, color: colors.muted },
  actions: { flexDirection: 'row', gap: 8, marginTop: 4 },
  action: { flex: 1, height: 44 },
  actionWide: { flex: 2, height: 44 },
  done: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  doneText: { fontSize: 13, color: colors.muted },
});
