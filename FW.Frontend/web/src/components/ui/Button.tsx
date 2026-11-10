import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { Loader2 } from 'lucide-react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';
type Size = 'sm' | 'md';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
  icon?: ReactNode;
}

const variants: Record<Variant, string> = {
  primary: 'bg-primary text-white hover:bg-primary-dark',
  secondary: 'border border-border bg-surface text-foreground hover:bg-background',
  danger: 'bg-danger text-white hover:bg-danger/90',
  ghost: 'text-muted hover:bg-background hover:text-foreground',
};

const sizes: Record<Size, string> = {
  sm: 'h-8 px-2.5 text-xs gap-1.5',
  md: 'h-9 px-3.5 text-sm gap-2',
};

export function Button({ variant = 'primary', size = 'md', loading, icon, className = '', children, disabled, ...rest }: ButtonProps) {
  return (
    <button
      className={`inline-flex shrink-0 items-center justify-center rounded-md font-medium transition disabled:cursor-not-allowed disabled:opacity-60 ${variants[variant]} ${sizes[size]} ${className}`}
      disabled={disabled || loading}
      {...rest}
    >
      {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : icon}
      {children}
    </button>
  );
}

/** Nút chỉ có icon (thao tác trên dòng bảng). */
export function IconButton({
  label,
  tone = 'default',
  className = '',
  children,
  ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & { label: string; tone?: 'default' | 'danger' }) {
  const toneClass = tone === 'danger' ? 'hover:bg-danger/10 hover:text-danger' : 'hover:bg-primary/10 hover:text-primary';
  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      className={`inline-flex h-8 w-8 items-center justify-center rounded-md text-muted transition disabled:opacity-40 ${toneClass} ${className}`}
      {...rest}
    >
      {children}
    </button>
  );
}
