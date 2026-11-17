import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { VStack } from '@astryxdesign/core/Stack';
import { TextInput } from '@astryxdesign/core/TextInput';
import { FormError } from '@/components/ui/Feedback';
import { Modal } from '@/components/ui/Modal';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { chatApi } from '@/services/chatApi';
import { MAX_GROUP_TITLE_LENGTH, type ChatContact, type Conversation } from '@/types/chat';
import { ContactPicker } from './ContactPicker';

/** Tạo nhóm chat tùy chỉnh (UC 7.2): đặt tên và chọn thành viên; người tạo là quản trị nhóm. */
export function NewGroupModal({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: (conversation: Conversation) => void }) {
  const [title, setTitle] = useState('');
  const [members, setMembers] = useState<ChatContact[]>([]);
  const { saving, error, setError, run } = useFormSubmit();

  useEffect(() => {
    if (!open) return;
    setTitle('');
    setMembers([]);
    setError(null);
  }, [open, setError]);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (title.trim().length < 2) return setError('Tên nhóm từ 2 ký tự trở lên.');
    if (members.length === 0) return setError('Chọn ít nhất một thành viên.');
    let created: Conversation | null = null;
    void run(
      async () => {
        created = (await chatApi.createGroup(title.trim(), members.map((m) => m.id))).data;
        return 'Đã tạo nhóm chat.';
      },
      () => {
        if (created) onCreated(created);
        onClose();
      },
    );
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="md"
      purpose="form"
      title="Tạo nhóm chat"
      description="Bạn là quản trị nhóm: đổi tên, thêm hoặc xóa thành viên sau này."
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Tạo nhóm" type="submit" form="new-group-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      {open && (
      <form id="new-group-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <FormError message={error} />
          <TextInput label="Tên nhóm" isRequired description={`Từ 2 đến ${MAX_GROUP_TITLE_LENGTH} ký tự`} value={title} onChange={(v) => { setTitle(v); setError(null); }} hasAutoFocus placeholder="VD: Nhóm quầy bar ca tối" />
          <ContactPicker selected={members} onChange={(next) => { setMembers(next); setError(null); }} />
        </VStack>
      </form>
      )}
    </Modal>
  );
}

/** Thêm thành viên vào nhóm (quản trị nhóm). Người đã trong nhóm hiện mờ. */
export function AddMembersModal({
  open,
  conversationId,
  existingIds,
  onClose,
  onAdded,
}: {
  open: boolean;
  conversationId: number;
  existingIds: number[];
  onClose: () => void;
  onAdded: () => void;
}) {
  const [members, setMembers] = useState<ChatContact[]>([]);
  const { saving, error, setError, run } = useFormSubmit();

  useEffect(() => {
    if (!open) return;
    setMembers([]);
    setError(null);
  }, [open, setError]);

  const submit = () => {
    if (members.length === 0) return setError('Chọn ít nhất một người để thêm.');
    void run(
      async () => {
        await chatApi.addMembers(conversationId, members.map((m) => m.id));
        return 'Đã thêm thành viên.';
      },
      () => {
        onAdded();
        onClose();
      },
    );
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="md"
      purpose="form"
      title="Thêm thành viên"
      description="Người mới vào xem được lịch sử nhóm nhưng không bị tính là tin chưa đọc."
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label={members.length > 0 ? `Thêm ${members.length} người` : 'Thêm'} variant="primary" isLoading={saving} onClick={submit} />
        </>
      }
    >
      <VStack gap={4}>
        <FormError message={error} />
        {open && <ContactPicker selected={members} onChange={(next) => { setMembers(next); setError(null); }} excludeIds={existingIds} />}
      </VStack>
    </Modal>
  );
}
