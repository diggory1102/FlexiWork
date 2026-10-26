export interface AppNotification {
  id: number;
  title: string;
  body: string;
  type: 'schedule_reminder' | 'approval_result' | 'announcement';
  isRead: boolean;
  createdAt: string;
}
