import { useCallback, useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { DateRangeInput, type DateRange, type DateRangePreset } from '@astryxdesign/core/DateRangeInput';
import { Icon } from '@astryxdesign/core/Icon';
import { Selector } from '@astryxdesign/core/Selector';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { Text } from '@astryxdesign/core/Text';
import { Tooltip } from '@astryxdesign/core/Tooltip';
import { MapPin, RefreshCw, ScanFace, Wifi } from 'lucide-react';
import { Avatar, PageHeader, Toolbar } from '@/components/ui/Layout';
import { ListCard, SearchInput } from '@/components/ui/ListCard';
import { Badge } from '@/components/ui/Feedback';
import { DataTable, Pagination, type Column } from '@/components/ui/DataTable';
import { usePagedList } from '@/hooks/usePagedList';
import { useDebounce } from '@/hooks/useDebounce';
import { attendanceApi } from '@/services/attendanceApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import { useLookupStore } from '@/stores/lookupStore';
import { toast } from '@/stores/toastStore';
import { todayIso, type ISODateString } from '@/lib/format';
import { addDays, formatDayMonth, formatMinutes, weekdayIndex, weekdayShortLabels } from '@/lib/dates';
import { RoleGroups } from '@/types/auth';
import type { AttendanceLogFilter, AttendanceLogItem, AttendanceStatus } from '@/types/attendance';
import { AttendanceDetailDrawer } from '../components/AttendanceDetailDrawer';
import {
  attendanceStatusLabels,
  attendanceStatusTone,
  formatDistance,
  formatHours,
  formatScore,
  timeOf,
  wifiState,
  wifiText,
  type LayerState,
} from '../attendanceUi';

const iso = (value: string) => value as ISODateString;
const monthStart = (day: string) => iso(`${day.slice(0, 8)}01`);

function presets(today: ISODateString): DateRangePreset[] {
  const thisMonth = monthStart(today);
  const lastMonth = monthStart(addDays(thisMonth, -1));
  return [
    { label: 'Hôm nay', getRange: () => ({ start: today, end: today }) },
    {
      label: 'Hôm qua',
      getRange: () => ({
        start: iso(addDays(today, -1)),
        end: iso(addDays(today, -1)),
      }),
    },
    {
      label: '7 ngày qua',
      getRange: () => ({ start: iso(addDays(today, -6)), end: today }),
    },
    {
      label: '30 ngày qua',
      getRange: () => ({ start: iso(addDays(today, -29)), end: today }),
    },
    { label: 'Tháng này', getRange: () => ({ start: thisMonth, end: today }) },
    {
      label: 'Tháng trước',
      getRange: () => ({ start: lastMonth, end: iso(addDays(thisMonth, -1)) }),
    },
  ];
}

const layerColor: Record<LayerState, 'success' | 'error' | 'secondary'> = {
  pass: 'success',
  fail: 'error',
  skip: 'secondary',
};

/** Lấy kết quả xấu hơn giữa lượt vào và lượt ra. */
const worst = (...states: (LayerState | null)[]): LayerState => (states.includes('fail') ? 'fail' : states.includes('pass') ? 'pass' : 'skip');

/** 3 biểu tượng GPS / Wi-Fi / Khuôn mặt (xanh = đạt, đỏ = không đạt, xám = không áp dụng), rê chuột để xem chi tiết. */
function LayerIcons({ log }: { log: AttendanceLogItem }) {
  const checkedOut = log.checkoutTime != null;
  const wifiIn = wifiState(log.checkinWifiMatched, log.branchRequiresWifi);
  const wifiOut = checkedOut ? wifiState(!!log.checkoutWifiMatched, log.branchRequiresWifi) : null;
  const wifi = worst(wifiIn, wifiOut);
  const layers = [
    {
      key: 'gps',
      icon: MapPin,
      state: 'pass' as LayerState, // lượt bị từ chối GPS không được lưu
      tip: `GPS: vào cách tâm ${formatDistance(log.checkinDistance)}${checkedOut ? `, ra cách ${formatDistance(log.checkoutDistance)}` : ''}`,
    },
    {
      key: 'wifi',
      icon: Wifi,
      state: wifi,
      tip:
        wifi === 'fail'
          ? `Wi-Fi: ${[wifiIn === 'fail' && 'lượt vào', wifiOut === 'fail' && 'lượt ra'].filter(Boolean).join(' và ')} không xác minh được`
          : `Wi-Fi: ${wifiText(wifi === 'pass', null, log.branchRequiresWifi).toLowerCase()}`,
    },
    {
      key: 'face',
      icon: ScanFace,
      state: 'pass' as LayerState, // không khớp khuôn mặt thì không được lưu
      tip: `Khuôn mặt: khớp ${formatScore(log.checkinFaceScore)}${checkedOut ? ` / ${formatScore(log.checkoutFaceScore)}` : ''}`,
    },
  ];
  return (
    <HStack gap={1.5}>
      {layers.map((l) => (
        <Tooltip key={l.key} content={l.tip}>
          <span tabIndex={0} aria-label={l.tip} className="inline-flex">
            <Icon icon={l.icon} size="sm" color={layerColor[l.state]} />
          </span>
        </Tooltip>
      ))}
    </HStack>
  );
}

type StatusTab = AttendanceStatus | 'all';

/**
 * Nhật ký chấm công (Task 3.5, UC 4.5 – 4.6). Admin/HR: mọi chi nhánh (lọc theo chi nhánh); Quản lý: chi nhánh của mình.
 * Lọc theo khoảng ngày công, trạng thái, tên / mã nhân viên. Bấm một dòng → ngăn chi tiết minh chứng 3 lớp, duyệt lượt chờ duyệt.
 */
export function AttendanceLogsPage() {
  const isCompanyWide = useAuthStore((s) => s.hasAnyRole(RoleGroups.AdminOrHr));
  const { branches, load: loadLookups } = useLookupStore();

  const today = todayIso();
  const [range, setRange] = useState<DateRange>({
    start: iso(addDays(today, -6)),
    end: today,
  });
  const { query, setQuery, page, rows, loading, error, reload } = usePagedList<AttendanceLogItem, AttendanceLogFilter>(attendanceApi.logs, {
    pageSize: 20,
    fromDate: range.start,
    toDate: range.end,
  });

  const [search, setSearch] = useState('');
  const debounced = useDebounce(search);
  const [tab, setTab] = useState<StatusTab>('all');
  const [pendingCount, setPendingCount] = useState<number | null>(null);
  const [selected, setSelected] = useState<AttendanceLogItem | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);

  useEffect(() => setQuery({ search: debounced }), [debounced, setQuery]);
  useEffect(() => {
    if (isCompanyWide) void loadLookups().catch((err) => toast.error(getErrorMessage(err)));
  }, [isCompanyWide, loadLookups]);
  useEffect(() => {
    if (error) toast.error(error);
  }, [error]);

  // Số lượt chờ duyệt trong cùng bộ lọc (chi nhánh, ngày, từ khóa) — hiện trên tab "Chờ duyệt"
  const { branchId, fromDate, toDate, search: keyword } = query;
  const loadPendingCount = useCallback(() => {
    attendanceApi
      .logs({
        branchId,
        fromDate,
        toDate,
        search: keyword,
        status: 'pending_approval',
        pageSize: 1,
      })
      .then(({ data }) => setPendingCount(data.totalCount))
      .catch(() => setPendingCount(null));
  }, [branchId, fromDate, toDate, keyword]);
  useEffect(loadPendingCount, [loadPendingCount]);

  const changeTab = (value: string) => {
    setTab(value as StatusTab);
    setQuery({ status: value === 'all' ? '' : (value as AttendanceStatus) });
  };

  const changeRange = (value: DateRange | null) => {
    const next = value ?? { start: iso(addDays(today, -6)), end: today };
    setRange(next);
    setQuery({ fromDate: next.start, toDate: next.end });
  };

  const openRow = (row: AttendanceLogItem) => {
    setSelected(row);
    setDrawerOpen(true);
  };

  const refresh = () => {
    void reload();
    loadPendingCount();
  };

  const columns: Column<AttendanceLogItem>[] = [
    {
      key: 'employee',
      header: 'Nhân viên',
      width: 240,
      render: (l) => (
        <HStack gap={3} vAlign="center">
          <Avatar name={l.fullName} url={l.avatarUrl} size={36} />
          <VStack gap={0.5} className="min-w-0">
            <Text weight="medium" maxLines={1}>
              {l.fullName}
            </Text>
            <Text type="supporting" maxLines={1}>
              <span className="font-mono">{l.employeeCode}</span>
              {isCompanyWide && <> · {l.branchName}</>}
            </Text>
          </VStack>
        </HStack>
      ),
    },
    {
      key: 'shift',
      header: 'Ngày công · Ca',
      width: 190,
      render: (l) => (
        <VStack gap={0.5}>
          <Text>
            {weekdayShortLabels[weekdayIndex(l.workDate)]}, {formatDayMonth(l.workDate)}
          </Text>
          <Text type="supporting" maxLines={1}>
            {l.shiftName ? `${l.shiftStartTime}–${l.shiftEndTime} · ${l.shiftName}` : 'Lịch đã xóa'}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'in',
      header: 'Vào',
      render: (l) => (
        <VStack gap={1} hAlign="start">
          <Text weight="medium">{timeOf(l.checkinTime, l.workDate)}</Text>
          {l.lateMinutes > 0 && <Badge tone="red">Muộn {formatMinutes(l.lateMinutes)}</Badge>}
        </VStack>
      ),
    },
    {
      key: 'out',
      header: 'Ra',
      render: (l) =>
        l.checkoutTime ? (
          <VStack gap={1} hAlign="start">
            <Text weight="medium">{timeOf(l.checkoutTime, l.workDate)}</Text>
            {l.earlyMinutes > 0 && <Badge tone="red">Sớm {formatMinutes(l.earlyMinutes)}</Badge>}
          </VStack>
        ) : (
          <Text type="supporting">Chưa ra</Text>
        ),
    },
    {
      key: 'hours',
      header: 'Giờ làm',
      align: 'end',
      render: (l) => <Text>{l.checkoutTime ? formatHours(l.actualWorkHours) : '—'}</Text>,
    },
    {
      key: 'layers',
      header: 'Xác minh',
      render: (l) => <LayerIcons log={l} />,
    },
    {
      key: 'status',
      header: 'Trạng thái',
      render: (l) => <Badge tone={attendanceStatusTone[l.status]}>{attendanceStatusLabels[l.status]}</Badge>,
    },
    {
      key: 'actions',
      header: '',
      align: 'end',
      width: 90,
      render: (l) => (
        <Button
          label={l.status === 'pending_approval' ? 'Duyệt' : 'Xem'}
          size="sm"
          variant={l.status === 'pending_approval' ? 'primary' : 'ghost'}
          onClick={() => openRow(l)}
        />
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Nhật ký chấm công"
        description={
          isCompanyWide
            ? 'Các lượt chấm công 3 lớp (GPS · Wi-Fi · khuôn mặt) toàn doanh nghiệp. Lượt chờ duyệt cần Quản lý / HR xác nhận mới được tính công.'
            : 'Các lượt chấm công 3 lớp (GPS · Wi-Fi · khuôn mặt) của chi nhánh bạn. Lượt chờ duyệt cần bạn xác nhận mới được tính công.'
        }
        actions={<Button label="Làm mới" variant="secondary" icon={<Icon icon={RefreshCw} />} onClick={refresh} />}
      />
      <ListCard>
        <HStack paddingInline={3} className="border-b border-border">
          <TabList value={tab} onChange={changeTab}>
            <Tab value="all" label="Tất cả" />
            <Tab value="pending_approval" label="Chờ duyệt" endContent={pendingCount ? <Badge tone="amber">{pendingCount}</Badge> : undefined} />
            <Tab value="valid" label="Hợp lệ" />
            <Tab value="invalid" label="Không hợp lệ" />
          </TabList>
        </HStack>
        <Toolbar>
          <SearchInput placeholder="Tìm tên hoặc mã nhân viên…" value={search} onChange={setSearch} />
          <DateRangeInput
            label="Ngày công"
            isLabelHidden
            width={260}
            value={range}
            onChange={changeRange}
            presets={presets(today)}
            max={today}
            maxRangeSpan={366}
            weekStartsOn="mon"
            hasClear={false}
          />
          {isCompanyWide && (
            <Selector
              label="Chi nhánh"
              isLabelHidden
              width={220}
              options={[
                { value: '', label: 'Tất cả chi nhánh' },
                ...branches.map((b) => ({
                  value: String(b.id),
                  label: b.name,
                })),
              ]}
              value={String(query.branchId ?? '')}
              onChange={(v) => setQuery({ branchId: v ? Number(v) : '' })}
            />
          )}
        </Toolbar>
        <DataTable
          columns={columns}
          rows={rows}
          rowKey={(l) => l.id}
          loading={loading}
          emptyText={tab === 'pending_approval' ? 'Không có lượt nào chờ duyệt' : 'Chưa có lượt chấm công trong khoảng ngày này'}
          onRowClick={openRow}
        />
        <Pagination page={page} onChange={(pageIndex) => setQuery({ pageIndex })} />
      </ListCard>

      <AttendanceDetailDrawer row={selected} open={drawerOpen} onClose={() => setDrawerOpen(false)} onReviewed={refresh} />
    </>
  );
}
