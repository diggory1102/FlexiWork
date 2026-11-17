import type { DateRange, DateRangePreset } from '@astryxdesign/core/DateRangeInput';
import { addDays } from './dates';
import type { ISODateString } from './format';

/** Chuỗi yyyy-MM-dd → kiểu ngày của Astryx. */
export const iso = (value: string) => value as ISODateString;

/** Ngày đầu tháng của một ngày. */
export const monthStart = (day: string) => iso(`${day.slice(0, 8)}01`);

/** Các lựa chọn nhanh cho bộ chọn khoảng ngày: hôm nay, hôm qua, 7 / 30 ngày qua, tháng này, tháng trước. */
export function rangePresets(today: ISODateString): DateRangePreset[] {
  const thisMonth = monthStart(today);
  const lastMonth = monthStart(addDays(thisMonth, -1));
  return [
    { label: 'Hôm nay', getRange: () => ({ start: today, end: today }) },
    { label: 'Hôm qua', getRange: () => ({ start: iso(addDays(today, -1)), end: iso(addDays(today, -1)) }) },
    { label: '7 ngày qua', getRange: () => ({ start: iso(addDays(today, -6)), end: today }) },
    { label: '30 ngày qua', getRange: () => ({ start: iso(addDays(today, -29)), end: today }) },
    { label: 'Tháng này', getRange: () => ({ start: thisMonth, end: today }) },
    { label: 'Tháng trước', getRange: () => ({ start: lastMonth, end: iso(addDays(thisMonth, -1)) }) },
  ];
}

/** Khoảng ngày mặc định: 7 ngày gần nhất tới hôm nay. */
export const lastDays = (today: ISODateString, days: number): DateRange => ({ start: iso(addDays(today, -(days - 1))), end: today });
