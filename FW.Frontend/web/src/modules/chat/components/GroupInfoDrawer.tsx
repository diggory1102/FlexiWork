import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { LogOut, UserMinus, UserPlus } from 'lucide-react';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { Badge, FormError, Notice, Spinner } from '@/components/ui/Feedback';
import { ConfirmDialog } from '@/components/ui/Modal';
import { DetailDrawer } from '@/components/ui/DetailDrawer';
import { EmployeeCell } from '@/components/ui/EmployeeCell';
import { useApiData } from '@/hooks/useApiData';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { chatApi } from '@/services/chatApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { MAX_GROUP_TITLE_LENGTH, type ChatMember, type Conversation } from '@/types/chat';
import { AddMembersModal } from './GroupModals';

/**
 * Thông tin nhóm: danh sách thành viên; với nhóm tùy chỉnh còn có đổi tên, thêm / xóa thành viên (quản trị nhóm) và rời nhóm.
 * Nhóm chi nhánh chỉ xem — thành viên do hệ thống đồng bộ theo chi nhánh.
 */
export function GroupInfoDrawer({ conversation, myId, open, onClose }: { conversation: Conversation; myId: number; open: boolean; onClose: () => void }) {
  const isOwner = conversation.isCustomGroup && conversation.ownerId === myId;
  const { data, loading, error, reload } = useApiData(() => chatApi.conversation(conversation.id), [conversation.id, conversation.memberCount, conversation.title], {
    enabled: open,
    toastError: false,
  });
  const [title, setTitle] = useState(conversation.title);
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<ChatMember | null>(null);
  const [leaving, setLeaving] = useState(false);
  const rename = useFormSubmit();

  useEffect(() => {
    setTitle(conversation.title);
    rename.setError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversation.title, open]);

  const saveTitle = () => {
    if (title.trim().length < 2) return rename.setError('Tên nhóm từ 2 ký tự trở lên.');
    void rename.run(async () => {
      await chatApi.renameGroup(conversation.id, title.trim());
      return 'Đã đổi tên nhóm.';
    });
  };

  const removeMember = async () => {
    if (!removing) return;
    const target = removing;
    setRemoving(null);
    try {
      await chatApi.removeMember(conversation.id, target.userId);
      toast.success(`Đã xóa ${target.fullName} khỏi nhóm.`);
      await reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  const leave = async () => {
    setLeaving(false);
    try {
      await chatApi.removeMember(conversation.id, myId);
      toast.success('Bạn đã rời nhóm.');
      onClose();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  const members = data?.members ?? [];

  return (
    <>
      <DetailDrawer
        open={open}
        onClose={onClose}
        label="Thông tin nhóm"
        title={conversation.title}
        subtitle={`${conversation.memberCount} thành viên`}
        width={480}
        footer={
          conversation.isCustomGroup ? (
            <HStack hAlign="end">
              <Button label="Rời nhóm" variant="destructive" icon={<Icon icon={LogOut} />} onClick={() => setLeaving(true)} />
            </HStack>
          ) : undefined
        }
      >
        <VStack gap={5}>
          <Notice title={error} />
          {!conversation.isCustomGroup && (
            <Notice status="info" title="Nhóm tự động theo chi nhánh" description="Thành viên được hệ thống cập nhật khi nhân sự vào hoặc chuyển khỏi chi nhánh; không đổi tên hay thêm / xóa thủ công được." />
          )}

          {isOwner && (
            <VStack gap={2}>
              <FormError message={rename.error} />
              <HStack gap={2} vAlign="end">
                <div className="flex-1">
                  <TextInput label="Tên nhóm" description={`Tối đa ${MAX_GROUP_TITLE_LENGTH} ký tự`} value={title} onChange={setTitle} />
                </div>
                <Button label="Lưu tên" variant="secondary" isLoading={rename.saving} isDisabled={title.trim() === conversation.title} onClick={saveTitle} />
              </HStack>
            </VStack>
          )}

          <VStack gap={2}>
            <HStack hAlign="between" vAlign="center">
              <Heading level={3}>Thành viên</Heading>
              {isOwner && <Button label="Thêm thành viên" variant="secondary" size="sm" icon={<Icon icon={UserPlus} />} onClick={() => setAdding(true)} />}
            </HStack>
            {loading && !data && (
              <div className="flex justify-center py-8">
                <Spinner />
              </div>
            )}
            {members.map((m) => (
              <HStack key={m.userId} hAlign="between" vAlign="center" gap={2}>
                <EmployeeCell name={m.fullName} code={m.employeeCode} avatarUrl={m.avatarUrl} sub={m.positionTitle} />
                <HStack gap={1} vAlign="center">
                  {conversation.isCustomGroup && m.userId === conversation.ownerId && <Badge tone="blue">Quản trị nhóm</Badge>}
                  {m.userId === myId && <Text type="supporting">Bạn</Text>}
                  {isOwner && m.userId !== myId && (
                    <IconButton variant="ghost" size="sm" label={`Xóa ${m.fullName} khỏi nhóm`} tooltip="Xóa khỏi nhóm" icon={<Icon icon={UserMinus} color="error" />} onClick={() => setRemoving(m)} />
                  )}
                </HStack>
              </HStack>
            ))}
          </VStack>
        </VStack>
      </DetailDrawer>

      <AddMembersModal open={adding} conversationId={conversation.id} existingIds={members.map((m) => m.userId)} onClose={() => setAdding(false)} onAdded={() => void reload()} />
      <ConfirmDialog
        open={removing !== null}
        title="Xóa khỏi nhóm"
        message={
          <>
            Xóa <b>{removing?.fullName}</b> khỏi nhóm? Người này sẽ không còn thấy cuộc trò chuyện.
          </>
        }
        confirmText="Xóa"
        onConfirm={() => void removeMember()}
        onClose={() => setRemoving(null)}
      />
      <ConfirmDialog
        open={leaving}
        title="Rời nhóm"
        message={
          conversation.ownerId === myId
            ? 'Bạn là quản trị nhóm. Khi bạn rời đi, người vào nhóm sớm nhất sẽ trở thành quản trị nhóm (nhóm đóng lại nếu không còn ai).'
            : 'Bạn sẽ không còn thấy cuộc trò chuyện này và phải nhờ quản trị nhóm thêm lại nếu muốn quay lại.'
        }
        confirmText="Rời nhóm"
        onConfirm={() => void leave()}
        onClose={() => setLeaving(false)}
      />
    </>
  );
}
