import { useEffect, useState, type ReactNode } from 'react';
import { initials } from '@/lib/format';

/** Tiêu đề trang + mô tả + nút thao tác. */
export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 className="text-lg font-semibold text-foreground">{title}</h1>
        {description && <p className="mt-0.5 text-xs text-muted">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

/** Thanh bộ lọc phía trên bảng. */
export function Toolbar({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap items-center gap-2 border-b border-border p-3">{children}</div>;
}

/** Ảnh đại diện; nếu chưa có ảnh hoặc ảnh lỗi thì hiển thị chữ cái đầu của họ tên. */
export function Avatar({ name, url, size = 32 }: { name: string; url?: string | null; size?: number }) {
  const style = { width: size, height: size };
  const [broken, setBroken] = useState(false);
  useEffect(() => setBroken(false), [url]);

  if (url && !broken) {
    return <img src={url} alt="" style={style} onError={() => setBroken(true)} className="shrink-0 rounded-full object-cover ring-1 ring-border" />;
  }
  return (
    <span
      style={{ ...style, fontSize: size * 0.38 }}
      className="inline-flex shrink-0 items-center justify-center rounded-full bg-primary/15 font-semibold text-primary"
    >
      {initials(name)}
    </span>
  );
}
