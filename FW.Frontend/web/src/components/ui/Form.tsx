import { forwardRef, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';

/** Nhãn + ô nhập + thông báo lỗi. */
export function Field({
  label,
  required,
  error,
  hint,
  className = '',
  children,
}: {
  label: string;
  required?: boolean;
  error?: string;
  hint?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <label className={`flex flex-col gap-1.5 ${className}`}>
      <span className="text-xs font-medium text-foreground">
        {label}
        {required && <span className="ml-0.5 text-danger">*</span>}
      </span>
      {children}
      {error ? <span className="text-xs text-danger">{error}</span> : hint && <span className="text-xs text-muted">{hint}</span>}
    </label>
  );
}

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(({ className = '', ...rest }, ref) => (
  <input ref={ref} className={`input ${className}`} {...rest} />
));
Input.displayName = 'Input';

export function Textarea({ className = '', ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={`input h-auto min-h-[72px] py-2 ${className}`} {...rest} />;
}

export function Select({ className = '', children, ...rest }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select className={`input pr-8 ${className}`} {...rest}>
      {children}
    </select>
  );
}

export function Checkbox({ label, description, ...rest }: InputHTMLAttributes<HTMLInputElement> & { label: string; description?: string }) {
  return (
    <label className={`flex items-start gap-2.5 ${rest.disabled ? 'cursor-not-allowed opacity-60' : 'cursor-pointer'}`}>
      <input type="checkbox" className="mt-0.5 h-4 w-4 rounded border-border accent-[rgb(var(--color-primary))]" {...rest} />
      <span>
        <span className="block text-sm text-foreground">{label}</span>
        {description && <span className="block text-xs text-muted">{description}</span>}
      </span>
    </label>
  );
}

/** Thông báo lỗi tổng của form (lỗi từ API). */
export function FormError({ message }: { message?: string | null }) {
  if (!message) return null;
  return <div className="rounded-md border border-danger/30 bg-danger/5 px-3 py-2 text-sm text-danger">{message}</div>;
}
