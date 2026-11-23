import { useRef, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { CheckCircle2, ImagePlus, Send, X } from 'lucide-react';
import { Badge, Notice, type BadgeTone } from '@/components/ui/Feedback';
import { getErrorMessage } from '@/services/apiClient';
import { assistantApi } from '@/services/assistantApi';
import { toast } from '@/stores/toastStore';
import type { DraftView } from '@/types/assistant';
import { DRAFT_FALLBACK_ICON, DRAFT_KINDS, DRAFT_STATUS_TEXT, effectiveDraftStatus } from '../assistantUi';

const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/heic', 'image/heif'];
const MAX_FILE_BYTES = 5 * 1024 * 1024;
const STATUS_TONE: Record<DraftView['status'], BadgeTone> = { pending: 'blue', confirmed: 'green', cancelled: 'gray', expired: 'amber' };

/**
 * Thẻ bản nháp đơn do trợ lý soạn. Đơn CHỈ được gửi khi người dùng bấm "Xác nhận" (qua đúng luồng kiểm tra của đơn tạo tay);
 * loại đơn yêu cầu minh chứng thì phải chọn ảnh trước khi xác nhận.
 */
export function DraftCard({ draft, onChange }: { draft: DraftView; onChange: (draft: DraftView) => void }) {
  const [busy, setBusy] = useState<'confirm' | 'cancel' | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const status = effectiveDraftStatus(draft);
  const pending = status === 'pending';
  const meta = DRAFT_KINDS[draft.kind];
  const KindIcon = meta?.icon ?? DRAFT_FALLBACK_ICON;

  const pickFile = (picked: File | undefined) => {
    if (!picked) return;
    if (!IMAGE_TYPES.includes(picked.type)) return setError('Minh chứng phải là ảnh JPG, PNG, WEBP hoặc HEIC.');
    if (picked.size > MAX_FILE_BYTES) return setError('Ảnh tối đa 5 MB.');
    setError(null);
    setFile(picked);
  };

  const confirm = async () => {
    if (draft.requiresAttachment && !file) return setError('Loại đơn này cần kèm ảnh minh chứng (VD: giấy khám bệnh).');
    setBusy('confirm');
    setError(null);
    try {
      const { data, message } = await assistantApi.confirmDraft(draft.id, file ?? undefined);
      onChange(data.draft);
      toast.success(message);
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
    <div className="w-full max-w-[460px] overflow-hidden rounded-xl border border-border bg-surface text-primary">
      <HStack gap={2} vAlign="center" hAlign="between" padding={3} className="border-b border-border bg-muted">
        <HStack gap={2} vAlign="center">
          <Icon icon={KindIcon} color="accent" />
          <Text weight="semibold">{meta?.label ?? draft.title}</Text>
        </HStack>
        <Badge tone={STATUS_TONE[status]}>{DRAFT_STATUS_TEXT[status]}</Badge>
      </HStack>

      <VStack gap={2} padding={3}>
        <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1.5 text-sm">
          {draft.lines.map((line, i) => (
            <div key={`${line.label}-${i}`} className="contents">
              <dt className="text-secondary">{line.label}</dt>
              <dd className="m-0 break-words font-medium">{line.value}</dd>
            </div>
          ))}
        </dl>

        {draft.warnings.map((w) => (
          <Notice key={w} status="warning" title={w} />
        ))}

        {pending && draft.requiresAttachment && (
          <HStack gap={2} vAlign="center">
            <input ref={fileInput} type="file" accept={IMAGE_TYPES.join(',')} className="hidden" onChange={(e) => { pickFile(e.target.files?.[0]); e.target.value = ''; }} />
            <Button label={file ? 'Đổi ảnh' : 'Chọn ảnh minh chứng'} variant="secondary" size="sm" icon={<Icon icon={ImagePlus} />} onClick={() => fileInput.current?.click()} />
            {file && (
              <Text type="supporting" maxLines={1}>
                {file.name}
              </Text>
            )}
          </HStack>
        )}

        <Notice title={error} />

        {pending ? (
          <HStack gap={2} hAlign="end">
            <Button label="Hủy" variant="ghost" icon={<Icon icon={X} />} isDisabled={busy !== null} isLoading={busy === 'cancel'} onClick={() => void cancel()} />
            <Button label="Xác nhận gửi đơn" variant="primary" icon={<Icon icon={Send} />} isDisabled={busy !== null && busy !== 'confirm'} isLoading={busy === 'confirm'} onClick={() => void confirm()} />
          </HStack>
        ) : status === 'confirmed' ? (
          <HStack gap={2} vAlign="center">
            <Icon icon={CheckCircle2} color="success" size="sm" />
            <Text type="supporting">Đơn đã được gửi và đang chờ duyệt.</Text>
          </HStack>
        ) : (
          <Text type="supporting">{status === 'expired' ? 'Bản nháp đã hết hạn — hãy nhờ trợ lý soạn lại.' : 'Bạn đã hủy bản nháp này.'}</Text>
        )}
      </VStack>
    </div>
  );
}
