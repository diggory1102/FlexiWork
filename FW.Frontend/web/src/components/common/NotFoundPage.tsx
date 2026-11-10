import { Link } from 'react-router-dom';

export function NotFoundPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-2 bg-background px-4 text-center">
      <h1 className="text-2xl font-semibold text-foreground">404 — Không tìm thấy trang</h1>
      <Link to="/" className="text-sm text-primary hover:underline">
        Quay về trang chủ
      </Link>
    </div>
  );
}
