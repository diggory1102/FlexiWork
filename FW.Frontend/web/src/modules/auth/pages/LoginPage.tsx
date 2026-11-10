/**
 * Trang đăng nhập Web Admin.
 * Form và lời gọi API /auth/login sẽ được hoàn thiện khi backend có Task 1.3 (Authentication).
 */
export function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="w-full max-w-sm rounded-xl border border-border bg-surface p-6 shadow-sm">
        <h1 className="text-xl font-semibold text-foreground">Đăng nhập FlexiWork</h1>
        <p className="mt-1 text-sm text-muted">Trang quản trị dành cho Admin, HR và Quản lý chi nhánh.</p>
      </div>
    </div>
  );
}
