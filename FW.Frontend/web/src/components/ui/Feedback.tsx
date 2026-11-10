import type { ReactNode } from 'react';
import { CheckCircle2, Inbox, Info, Loader2, X, XCircle } from 'lucide-react';
import { useToastStore } from '@/stores/toastStore';

export function Spinner({ className = '' }: { className?: string }) {
  return <Loader2 className={`h-5 w-5 animate-spin text-primary ${className}`} />;
}

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 px-4 py-14 text-center">
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-primary">
        <Inbox className="h-6 w-6" />
      </div>
      <p className="font-medium text-foreground">{title}</p>
      {description && <p className="max-w-sm text-xs text-muted">{description}</p>}
      {action}
    </div>
  );
}

type BadgeTone = 'gray' | 'blue' | 'green' | 'amber' | 'red' | 'purple';

const badgeTones: Record<BadgeTone, string> = {
  gray: 'bg-slate-100 text-slate-700',
  blue: 'bg-blue-50 text-blue-700',
  green: 'bg-green-50 text-green-700',
  amber: 'bg-amber-50 text-amber-700',
  red: 'bg-red-50 text-red-700',
  purple: 'bg-violet-50 text-violet-700',
};

export function Badge({ tone = 'gray', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={`inline-flex items-center whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${badgeTones[tone]}`}>{children}</span>;
}

const toastStyles = {
  success: { icon: CheckCircle2, className: 'border-success/30 text-success' },
  error: { icon: XCircle, className: 'border-danger/30 text-danger' },
  info: { icon: Info, className: 'border-primary/30 text-primary' },
};

/** Vùng hiển thị thông báo nổi (góc trên bên phải). */
export function Toaster() {
  const { toasts, dismiss } = useToastStore();
  return (
    <div className="pointer-events-none fixed right-4 top-16 z-[60] flex w-80 flex-col gap-2">
      {toasts.map((t) => {
        const { icon: Icon, className } = toastStyles[t.type];
        return (
          <div key={t.id} className={`pointer-events-auto flex items-start gap-2.5 rounded-lg border bg-surface px-3 py-2.5 shadow-lg ${className}`}>
            <Icon className="mt-0.5 h-4 w-4 shrink-0" />
            <p className="flex-1 text-sm text-foreground">{t.message}</p>
            <button type="button" onClick={() => dismiss(t.id)} className="text-muted hover:text-foreground" aria-label="Đóng">
              <X className="h-3.5 w-3.5" />
            </button>
          </div>
        );
      })}
    </div>
  );
}
