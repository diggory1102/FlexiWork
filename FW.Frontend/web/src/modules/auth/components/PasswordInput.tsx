import { useState, type InputHTMLAttributes } from 'react';
import { Eye, EyeOff } from 'lucide-react';

/** Ô mật khẩu có nút hiện/ẩn. */
export function PasswordInput(props: InputHTMLAttributes<HTMLInputElement>) {
  const [visible, setVisible] = useState(false);
  return (
    <div className="relative">
      <input {...props} type={visible ? 'text' : 'password'} className="input pr-10" />
      <button
        type="button"
        onClick={() => setVisible((v) => !v)}
        className="absolute inset-y-0 right-0 flex w-10 items-center justify-center text-muted hover:text-foreground"
        aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
        tabIndex={-1}
      >
        {visible ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
      </button>
    </div>
  );
}

/** Kiểm tra mật khẩu ở client theo đúng quy tắc PasswordPolicy của backend. */
export function validatePassword(password: string): string | null {
  if (password.length < 8) return 'Mật khẩu phải có ít nhất 8 ký tự.';
  if (!/[A-Za-zÀ-ỹ]/.test(password)) return 'Mật khẩu phải chứa ít nhất một chữ cái.';
  if (!/\d/.test(password)) return 'Mật khẩu phải chứa ít nhất một chữ số.';
  if (/\s/.test(password)) return 'Mật khẩu không được chứa khoảng trắng.';
  return null;
}
