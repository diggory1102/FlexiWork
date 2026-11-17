import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { CheckCheck } from 'lucide-react';
import { PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard } from '@/components/ui/ListCard';
import { EmptyState, Spinner } from '@/components/ui/Feedback';
import { Pagination } from '@/components/ui/DataTable';
import { NotificationRow } from '@/components/common/HeaderBadges';
import { usePagedList } from '@/hooks/usePagedList';
import { notificationApi } from '@/services/notificationApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useNotificationStore } from '@/stores/notificationStore';
import { toast } from '@/stores/toastStore';
import { RoleGroups } from '@/types/auth';
import type { AppNotification, NotificationFilter } from '@/types/notification';
import { notificationTarget } from '../notificationUi';

/** Trung tâm thông báo: toàn bộ thông báo của tôi (chấm công, đơn từ, lịch, công việc, phiếu lương); bấm để mở màn hình liên quan. */
export function NotificationsPage() {
  const navigate = useNavigate();
  const isAdminOrHr = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const unread = useNotificationStore((s) => s.unread);
  const { markAllRead } = useNotificationStore();
  const [tab, setTab] = useState<'all' | 'unread'>('all');
  const { setQuery, page, rows, loading, error, reload } = usePagedList<AppNotification, NotificationFilter>(notificationApi.list, { pageSize: 20 });

  useEffect(() => setQuery({ unreadOnly: tab === 'unread' }), [tab, setQuery]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);
  // Thông báo mới tới khi đang xem trang: huy hiệu đổi thì nạp lại danh sách
  useEffect(() => void reload(), [unread, reload]);

  const open = async (n: AppNotification) => {
    try {
      if (!n.isRead) await useNotificationStore.getState().markRead(n.id);
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
    const target = notificationTarget(n, isAdminOrHr);
    if (target) navigate(target);
    else void reload();
  };

  return (
    <>
      <PageHeader
        title="Thông báo"
        description="Chấm công, đơn từ, lịch làm việc, công việc theo ca và phiếu lương dành cho bạn."
        actions={
          <Button
            label="Đánh dấu đã đọc tất cả"
            variant="secondary"
            icon={<Icon icon={CheckCheck} />}
            isDisabled={unread === 0}
            onClick={() => void markAllRead().then(() => reload()).catch((err) => toast.error(getErrorMessage(err)))}
          />
        }
      />
      <ListCard>
        <Toolbar>
          <TabList value={tab} onChange={(v) => setTab(v as 'all' | 'unread')}>
            <Tab value="all" label="Tất cả" />
            <Tab value="unread" label={unread > 0 ? `Chưa đọc (${unread})` : 'Chưa đọc'} />
          </TabList>
        </Toolbar>
        {loading && rows.length === 0 ? (
          <div className="flex justify-center py-16">
            <Spinner />
          </div>
        ) : rows.length === 0 ? (
          <div className="py-10">
            <EmptyState title={tab === 'unread' ? 'Không có thông báo chưa đọc' : 'Chưa có thông báo nào'} />
          </div>
        ) : (
          rows.map((n) => <NotificationRow key={n.id} notification={n} onOpen={(item) => void open(item)} />)
        )}
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>
    </>
  );
}
