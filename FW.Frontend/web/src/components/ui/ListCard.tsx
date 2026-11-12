import type { ReactNode } from 'react';
import { Card } from '@astryxdesign/core/Card';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Search } from 'lucide-react';

/** Khung chứa bảng danh sách: thẻ không đệm, bo góc, cắt phần tràn của bảng. */
export function ListCard({ children }: { children: ReactNode }) {
  return (
    <Card padding={0}>
      <div className="overflow-hidden rounded-[inherit]">{children}</div>
    </Card>
  );
}

/** Ô tìm kiếm trên thanh lọc (có nút xóa nhanh). */
export function SearchInput({ value, onChange, placeholder, width = 300 }: { value: string; onChange: (value: string) => void; placeholder: string; width?: number }) {
  return (
    <TextInput
      label="Tìm kiếm"
      isLabelHidden
      type="search"
      startIcon={Search}
      placeholder={placeholder}
      value={value}
      onChange={onChange}
      hasClear
      width={width}
    />
  );
}
