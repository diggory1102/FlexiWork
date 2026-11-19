/** Khớp với FW.Application.Features.Chat (ChatDtos.cs). */
export type ConversationType = 'direct' | 'group';

export interface LastMessage {
  id: number;
  senderId: number;
  senderName: string;
  /** Nội dung rút gọn: văn bản hoặc "[Hình ảnh]". */
  preview: string;
  createdAt: string;
}

/** Một phòng chat trong danh sách. Chat trực tiếp: title / avatarUrl là của người kia. */
export interface Conversation {
  id: number;
  type: ConversationType;
  title: string;
  avatarUrl: string | null;
  branchId: number | null;
  memberCount: number;
  lastMessage: LastMessage | null;
  unreadCount: number;
  lastActivityAt: string;
  /** Nhóm tùy chỉnh (UC 7.2): đổi tên / thêm / xóa thành viên được. Nhóm chi nhánh do hệ thống quản lý. */
  isCustomGroup: boolean;
  /** Quản trị nhóm tùy chỉnh; null với phòng khác. */
  ownerId: number | null;
}

export interface ChatMember {
  userId: number;
  employeeCode: string;
  fullName: string;
  avatarUrl: string | null;
  positionTitle: string | null;
}

export interface ConversationDetail {
  conversation: Conversation;
  members: ChatMember[];
}

/** Tin nhắn. Tin đã thu hồi: isDeleted = true, content và imageUrl bị ẩn. */
export interface ChatMessage {
  id: number;
  conversationId: number;
  senderId: number;
  senderName: string;
  senderAvatarUrl: string | null;
  content: string | null;
  imageUrl: string | null;
  isDeleted: boolean;
  createdAt: string;
}

/** Một trang lịch sử (cũ → mới); trang cũ hơn: beforeId = nextBeforeId. */
export interface MessagePage {
  items: ChatMessage[];
  hasMore: boolean;
  nextBeforeId: number | null;
}

export interface ChatContact {
  id: number;
  employeeCode: string;
  fullName: string;
  avatarUrl: string | null;
  branchName: string | null;
  positionTitle: string | null;
}

export interface MessageDeletedEvent {
  conversationId: number;
  messageId: number;
}

export interface ConversationReadEvent {
  conversationId: number;
  unreadCount: number;
}

export const MAX_MESSAGE_LENGTH = 2000;
export const MAX_GROUP_TITLE_LENGTH = 100;
