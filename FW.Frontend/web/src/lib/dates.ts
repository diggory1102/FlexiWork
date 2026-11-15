/**
 * Thao tác trên chuỗi ngày "yyyy-MM-dd" (không giờ, không múi giờ) — tính theo UTC để tránh lệch ngày.
 */

const DAY_MS = 86_400_000;

const toUtc = (iso: string) => {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  return Date.UTC(y, m - 1, d);
};

const fromUtc = (ms: number) => new Date(ms).toISOString().slice(0, 10);

export function addDays(iso: string, days: number): string {
  return fromUtc(toUtc(iso) + days * DAY_MS);
}

/** Thứ trong tuần: 0 = Thứ 2 … 6 = Chủ nhật. */
export function weekdayIndex(iso: string): number {
  return (new Date(toUtc(iso)).getUTCDay() + 6) % 7;
}

/** Ngày Thứ 2 của tuần chứa ngày đã cho. */
export function startOfWeek(iso: string): string {
  return addDays(iso, -weekdayIndex(iso));
}

/** 7 ngày từ Thứ 2 tới Chủ nhật. */
export function weekDays(monday: string): string[] {
  return Array.from({ length: 7 }, (_, i) => addDays(monday, i));
}

export const weekdayShortLabels = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
export const weekdayLabels = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'Chủ nhật'];

/** "17/11" */
export function formatDayMonth(iso: string): string {
  const [, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}`;
}

/** Số phút → "7 giờ 30 phút" / "45 phút". */
export function formatMinutes(total: number): string {
  if (total <= 0) return '0 phút';
  const h = Math.floor(total / 60);
  const m = total % 60;
  if (h === 0) return `${m} phút`;
  return m === 0 ? `${h} giờ` : `${h} giờ ${m} phút`;
}

/** "HH:mm" → số phút từ 00:00; chuỗi không hợp lệ → null. */
export function timeToMinutes(time: string | undefined | null): number | null {
  const match = /^(\d{1,2}):(\d{2})/.exec(time ?? '');
  if (!match) return null;
  const h = Number(match[1]);
  const m = Number(match[2]);
  return h < 24 && m < 60 ? h * 60 + m : null;
}
