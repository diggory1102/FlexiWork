import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { DateRangeInput, type DateRange } from '@astryxdesign/core/DateRangeInput';
import { Icon } from '@astryxdesign/core/Icon';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { Text } from '@astryxdesign/core/Text';
import { RefreshCw } from 'lucide-react';
import { Avatar, PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { Badge } from '@/components/ui/Feedback';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { requestApi } from '@/services/requestApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { formatDateTime, todayIso } from '@/lib/format';
import { rangePresets } from '@/lib/dateRanges';
import { RoleGroups } from '@/types/auth';
import type { LeaveRequest, LeaveRequestFilter, RequestCategory, RequestStatus } from '@/types/requests';
import { RequestDetailDrawer } from '../components/RequestDetailDrawer';
import { categoryLabels, categoryTone, statusLabels, statusTone, summarize } from '../requestUi';

type StatusTab = RequestStatus | 'all';

const categoryOptions = [
  { value: '', label: 'Mọi loại đơn' },
  ...(Object.keys(categoryLabels) as RequestCategory[]).map((c) => ({ value: c, label: categoryLabels[c] })),
];

/**
 * Duyệt đơn (Task 4.5, UC 5.3): danh sách đơn nghỉ / đổi ca / OT / giải trình theo phạm vi vai trò (Admin/HR: mọi chi nhánh;
 * Quản lý: chi nhánh mình). Mặc định hiện đơn chờ duyệt; bấm một dòng → ngăn chi tiết để duyệt / từ chối.
 */
export function RequestsPage() {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const { branches, load: loadLookups } = useLookupStore();
  const { query, setQuery, page, rows, loading, error, reload } = usePagedList<LeaveRequest, LeaveRequestFilter>(requestApi.list, {
    pageSize: 20,
    status: 'pending',
  });

  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [tab, setTab] = useState<StatusTab>('pending');
  const [range, setRange] = useState<DateRange | null>(null);
  const [pendingCount, setPendingCount] = useState<number | null>(null);
  const [selected, setSelected] = useState<LeaveRequest | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const today = todayIso();

  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    if (isCompanyWide) void loadLookups().catch((err) => toast.error(getErrorMessage(err)));
  }, [isCompanyWide, loadLookups]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  // Số đơn chờ duyệt theo cùng bộ lọc chi nhánh / loại đơn / từ khóa — hiện trên tab "Chờ duyệt"
  const { branchId, category, search: keyword } = query;
  const loadPendingCount = useCallback(() => {
    requestApi
      .list({ branchId, category, search: keyword, status: 'pending', pageSize: 1 })
      .then(({ data }) => setPendingCount(data.totalCount))
      .catch(() => setPendingCount(null));
  }, [branchId, category, keyword]);
  useEffect(loadPendingCount, [loadPendingCount]);

  const changeTab = (value: string) => {
    setTab(value as StatusTab);
    setQuery({ status: value === 'all' ? '' : (value as RequestStatus) });
  };

  const changeRange = (value: DateRange | null) => {
    setRange(value);
    setQuery({ fromDate: value?.start, toDate: value?.end });
  };

  const openRow = (row: LeaveRequest) => {
    setSelected(row);
    setDrawerOpen(true);
  };

  const refresh = () => {
    void reload();
    loadPendingCount();
  };

  const columns: Column<LeaveRequest>[] = [
    {
      key: 'employee',
      header: 'Nhân viên',
      width: 240,
      render: (r) => (
        <HStack gap={3} vAlign="center">
          <Avatar name={r.employee.fullName} url={r.employee.avatarUrl} size={36} />
          <VStack gap={0.5} className="min-w-0">
            <Text weight="medium" maxLines={1}>
              {r.employee.fullName}
            </Text>
            <Text type="supporting" maxLines={1}>
              <span className="font-mono">{r.employee.employeeCode}</span>
              {isCompanyWide && r.branchName && <> · {r.branchName}</>}
            </Text>
          </VStack>
        </HStack>
      ),
    },
    {
      key: 'request',
      header: 'Nội dung đơn',
      render: (r) => (
        <VStack gap={1} hAlign="start">
          <HStack gap={1.5} vAlign="center" wrap="wrap">
            <Badge tone={categoryTone[r.category]}>{categoryLabels[r.category]}</Badge>
            <Text weight="medium" maxLines={1}>
              {r.leaveTypeName}
            </Text>
          </HStack>
          <Text type="supporting" maxLines={2}>
            {summarize(r)}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'reason',
      header: 'Lý do',
      width: 240,
      render: (r) => (
        <Text type="supporting" maxLines={2}>
          {r.reason}
        </Text>
      ),
    },
    { key: 'created', header: 'Gửi lúc', width: 140, render: (r) => <Text type="supporting">{formatDateTime(r.createdAt)}</Text> },
    {
      key: 'status',
      header: 'Trạng thái',
      width: 160,
      render: (r) => (
        <VStack gap={0.5} hAlign="start">
          <Badge tone={statusTone[r.status]}>{statusLabels[r.status]}</Badge>
          {r.approverName && (
            <Text type="supporting" size="sm" maxLines={1}>
              {r.approverName}
            </Text>
          )}
        </VStack>
      ),
    },
    {
      key: 'actions',
      header: '',
      width: 90,
      align: 'end',
      render: (r) => (
        <Button label={r.canReview ? 'Duyệt' : 'Xem'} size="sm" variant={r.canReview ? 'primary' : 'ghost'} onClick={() => openRow(r)} />
      ),
    },
  ];

  const emptyText = tab === 'pending' ? 'Không có đơn nào chờ duyệt' : 'Không có đơn nào phù hợp bộ lọc';

  return (
    <>
      <PageHeader
        title="Duyệt đơn"
        description={
          isCompanyWide
            ? 'Đơn nghỉ, đổi ca, làm thêm giờ và giải trình của toàn doanh nghiệp. Đơn của Quản lý do HR / Quản trị viên duyệt.'
            : 'Đơn nghỉ, đổi ca, làm thêm giờ và giải trình của chi nhánh bạn quản lý. Bạn không tự duyệt đơn của chính mình.'
        }
        actions={<Button label="Làm mới" variant="secondary" icon={<Icon icon={RefreshCw} />} onClick={refresh} />}
      />
      <ListCard>
        <HStack paddingInline={3} className="border-b border-border">
          <TabList value={tab} onChange={changeTab}>
            <Tab value="pending" label="Chờ duyệt" endContent={pendingCount ? <Badge tone="amber">{pendingCount}</Badge> : undefined} />
            <Tab value="pending_peer" label="Chờ đồng nghiệp" />
            <Tab value="approved" label="Đã duyệt" />
            <Tab value="rejected" label="Bị từ chối" />
            <Tab value="all" label="Tất cả" />
          </TabList>
        </HStack>
        <Toolbar>
          <SearchInput placeholder="Tìm tên hoặc mã nhân viên…" value={search} onChange={setSearch} />
          <Selector
            label="Loại đơn"
            isLabelHidden
            width={190}
            options={categoryOptions}
            value={query.category ?? ''}
            onChange={(v) => setQuery({ category: (v ?? '') as RequestCategory | '' })}
          />
          <DateRangeInput
            label="Khoảng ngày"
            isLabelHidden
            placeholder="Mọi ngày"
            width={260}
            value={range}
            onChange={changeRange}
            presets={rangePresets(today)}
            weekStartsOn="mon"
          />
          {isCompanyWide && (
            <Selector
              label="Chi nhánh"
              isLabelHidden
              width={220}
              options={[{ value: '', label: 'Tất cả chi nhánh' }, ...branches.map((b) => ({ value: String(b.id), label: b.name }))]}
              value={String(query.branchId ?? '')}
              onChange={(v) => setQuery({ branchId: v ? Number(v) : '' })}
            />
          )}
        </Toolbar>
        <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} loading={loading} emptyText={emptyText} onRowClick={openRow} />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <RequestDetailDrawer request={selected} open={drawerOpen} onClose={() => setDrawerOpen(false)} onReviewed={refresh} />
    </>
  );
}
