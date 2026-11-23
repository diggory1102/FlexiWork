/** Khớp với FW.Application.Features.Assistant (AssistantService, DraftView, MessageView) và Features.Attrition (AttritionDtos) ở backend. */

export type DraftKind = 'leave' | 'shift_swap' | 'overtime' | 'explanation';
export type DraftStatus = 'pending' | 'confirmed' | 'cancelled' | 'expired';

export interface DraftLine {
  label: string;
  value: string;
}

/** Thẻ bản nháp đơn do trợ lý soạn — chưa phải đơn thật cho tới khi người dùng bấm xác nhận. */
export interface DraftView {
  id: string;
  kind: DraftKind;
  title: string;
  lines: DraftLine[];
  warnings: string[];
  requiresAttachment: boolean;
  status: DraftStatus;
  expiresAt: string;
}

export interface ChatReply {
  conversationId: number;
  reply: string;
  drafts: DraftView[];
  toolsUsed: string[];
}

export interface AssistantConversation {
  id: number;
  userId: number;
  title: string;
  createdAt: string;
  updatedAt: string;
}

export interface AssistantMessage {
  id: number;
  role: 'user' | 'assistant';
  content: string;
  drafts: DraftView[];
  createdAt: string;
}

export interface ConfirmDraftResult {
  draft: DraftView;
  requestId: number;
}

/** Sự kiện SSE của POST /assistant/chat/stream. `done.reply` là nội dung chính thức; `delta` chỉ để hiện dần. */
export type AssistantStreamEvent =
  | { type: 'conversation'; conversationId: number }
  | { type: 'tool'; name: string }
  | { type: 'delta'; text: string }
  | { type: 'reset' }
  | { type: 'done'; reply: ChatReply }
  | { type: 'error'; message: string; status: number };

// ---- Cảnh báo nhân sự có dấu hiệu nghỉ việc (UC 10.3 – 10.4) ----

export type AttritionLevel = 'low' | 'medium' | 'high';

export interface AttritionSignal {
  key: string;
  label: string;
  recent: number;
  baseline: number;
  subScore: number;
  weight: number;
  contribution: number;
  reason: string | null;
}

export interface AttritionAlert {
  userId: number;
  employeeName: string;
  branchId: number | null;
  weekStart: string;
  score: number;
  level: AttritionLevel;
  dataSufficient: boolean;
  previousScore: number | null;
  delta: number | null;
  reasons: string[];
  signals: AttritionSignal[];
  computedAt: string;
}

export interface AttritionSummary {
  weekStart: string | null;
  computedAt: string | null;
  scored: number;
  medium: number;
  high: number;
}

export interface AttritionNarrative {
  userId: number;
  text: string;
}

export interface AttritionRunResult {
  weekStart: string;
  employees: number;
  scored: number;
  medium: number;
  high: number;
}
