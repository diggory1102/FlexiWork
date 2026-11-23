import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { HeartHandshake } from 'lucide-react';
import { Notice, Spinner } from '@/components/ui/Feedback';
import { Avatar } from '@/components/ui/Layout';
import { getErrorMessage } from '@/services/apiClient';
import { attritionApi } from '@/services/attritionApi';
import type { AttritionAlert, AttritionSummary } from '@/types/assistant';
import { AttritionLevelBadge, ScoreDelta } from './AttritionBits';
import { AttritionDrawer } from './AttritionDrawer';

const TOP = 5;

/** Ô "Nhân sự cần quan tâm" trên Dashboard (UC 10.4): vài người điểm cao nhất kèm lý do chính; bấm để xem xu hướng và diễn giải. */
export function AttritionWidget({ branchId }: { branchId: number | '' }) {
  const [summary, setSummary] = useState<AttritionSummary | null>(null);
  const [alerts, setAlerts] = useState<AttritionAlert[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<AttritionAlert | null>(null);

  useEffect(() => {
    let current = true;
    setLoading(true);
    const id = branchId === '' ? undefined : branchId;
    Promise.all([attritionApi.summary({ branchId: id }), attritionApi.alerts({ branchId: id, limit: TOP })])
      .then(([sum, list]) => {
        if (!current) return;
        setSummary(sum.data);
        setAlerts(list.data);
        setError(null);
      })
      .catch((err) => current && setError(getErrorMessage(err)))
      .finally(() => current && setLoading(false));
    return () => {
      current = false;
    };
  }, [branchId]);

  const hasResult = summary?.weekStart != null;

  return (
    <Card padding={5}>
      <VStack gap={3}>
        <HStack hAlign="between" vAlign="start" gap={3}>
          <VStack gap={1}>
            <Heading level={3}>Nhân sự cần quan tâm</Heading>
            <Text type="supporting" as="p">
              {hasResult
                ? `${summary.high} mức cao, ${summary.medium} mức trung bình trên ${summary.scored} nhân viên đã chấm điểm. Chỉ là tín hiệu tham khảo.`
                : 'Điểm được tính mỗi sáng thứ Hai từ thay đổi hành vi so với chính nhân viên đó.'}
            </Text>
          </VStack>
          <Icon icon={HeartHandshake} size="lg" color="accent" />
        </HStack>

        <Notice title={error} />
        {loading && (
          <div className="flex justify-center py-4">
            <Spinner />
          </div>
        )}
        {!loading && !error && alerts.length === 0 && (
          <Text type="supporting">{hasResult ? 'Hiện không ai ở mức cần quan tâm.' : 'Chưa có kết quả chấm điểm nào.'}</Text>
        )}

        {alerts.length > 0 && (
          <VStack gap={0} className="-mx-2">
            {alerts.map((a) => (
              <button key={a.userId} type="button" onClick={() => setSelected(a)} className="flex items-center gap-3 rounded-lg px-2 py-2 text-left hover:bg-muted">
                <Avatar name={a.employeeName} size={36} />
                <VStack gap={0.5} className="min-w-0 flex-1">
                  <Text weight="medium" maxLines={1}>
                    {a.employeeName}
                  </Text>
                  <Text type="supporting" maxLines={1}>
                    {a.reasons[0] ?? '—'}
                  </Text>
                </VStack>
                <VStack gap={0.5} className="shrink-0 items-end">
                  <HStack gap={2} vAlign="center">
                    <span className="font-semibold tabular-nums">{a.score}</span>
                    <AttritionLevelBadge level={a.level} />
                  </HStack>
                  <ScoreDelta delta={a.delta} />
                </VStack>
              </button>
            ))}
          </VStack>
        )}

        <HStack hAlign="end">
          <Button label="Xem tất cả" variant="secondary" size="sm" href="/attrition" />
        </HStack>
      </VStack>
      <AttritionDrawer alert={selected} onClose={() => setSelected(null)} />
    </Card>
  );
}
