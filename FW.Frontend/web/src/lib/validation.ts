/** Kiểm tra mật khẩu ở client theo đúng quy tắc PasswordPolicy của backend. */
export function validatePassword(password: string): string | null {
  if (password.length < 8) return 'Mật khẩu phải có ít nhất 8 ký tự.';
  if (!/[A-Za-zÀ-ỹ]/.test(password)) return 'Mật khẩu phải chứa ít nhất một chữ cái.';
  if (!/\d/.test(password)) return 'Mật khẩu phải chứa ít nhất một chữ số.';
  if (/\s/.test(password)) return 'Mật khẩu không được chứa khoảng trắng.';
  return null;
}

/** Mật khẩu mới + xác nhận: trả về lỗi đầu tiên hoặc null. */
export function validateNewPassword(password: string, confirm: string): string | null {
  return validatePassword(password) ?? (password !== confirm ? 'Mật khẩu xác nhận không khớp.' : null);
}

/** Trạng thái lỗi cho ô nhập Astryx (undefined = không lỗi). */
export const errorStatus = (message?: string | null) => (message ? { type: 'error' as const, message } : undefined);
