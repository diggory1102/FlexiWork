import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { HStack } from '@astryxdesign/core/Stack';
import { Pencil, Trash2 } from 'lucide-react';

/**
 * Cặp nút Sửa / Xóa cuối mỗi dòng bảng. Không truyền hàm nào thì không hiện nút đó (VD: chỉ xem).
 * Chặn sự kiện bấm nổi lên dòng để không kích hoạt thao tác "bấm dòng" (mở sửa / chi tiết) cùng lúc.
 */
export function RowActions({ onEdit, onDelete }: { onEdit?: () => void; onDelete?: () => void }) {
  return (
    <div onClick={(e) => e.stopPropagation()}>
      <HStack gap={0.5} hAlign="end">
        {onEdit && <IconButton variant="ghost" size="sm" label="Sửa" tooltip="Sửa" icon={<Icon icon={Pencil} />} onClick={onEdit} />}
        {onDelete && <IconButton variant="ghost" size="sm" label="Xóa" tooltip="Xóa" icon={<Icon icon={Trash2} color="error" />} onClick={onDelete} />}
      </HStack>
    </div>
  );
}
