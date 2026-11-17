import type { ChatMessage } from '@/types/chat';

/** Giờ ngắn của một thời điểm: hôm nay "14:05", ngày khác "17/11". */
export function shortTime(iso: string, now: Date = new Date()): string {
  const today = new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
  if (iso.slice(0, 10) === today) return iso.slice(11, 16);
  const [, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}`;
}

/** Nhãn ngăn cách ngày trong luồng tin: "Hôm nay", "Hôm qua", hoặc "17/11/2026". */
export function dayLabel(iso: string, now: Date = new Date()): string {
  const local = (d: Date) => new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
  const day = iso.slice(0, 10);
  if (day === local(now)) return 'Hôm nay';
  if (day === local(new Date(now.getTime() - 86_400_000))) return 'Hôm qua';
  const [y, m, d] = day.split('-');
  return `${d}/${m}/${y}`;
}

export interface MessageGroup {
  day: string;
  /** Các tin liên tiếp của cùng một người trong cùng ngày. */
  runs: { senderId: number; messages: ChatMessage[] }[];
}

/** Gom tin theo ngày rồi theo từng lượt liên tiếp của một người gửi (để chỉ hiện tên / ảnh đại diện một lần). */
export function groupMessages(messages: ChatMessage[]): MessageGroup[] {
  const groups: MessageGroup[] = [];
  for (const message of messages) {
    const day = message.createdAt.slice(0, 10);
    let group = groups[groups.length - 1];
    if (!group || group.day !== day) {
      group = { day, runs: [] };
      groups.push(group);
    }
    const run = group.runs[group.runs.length - 1];
    if (run && run.senderId === message.senderId) run.messages.push(message);
    else group.runs.push({ senderId: message.senderId, messages: [message] });
  }
  return groups;
}
