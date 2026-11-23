import { useEffect, useMemo, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { Sparkles } from 'lucide-react';
import { ChartTooltip, axisProps, chartColors } from '@/components/charts/ChartCard';
import { DetailDrawer } from '@/components/ui/DetailDrawer';
import { Notice, Spinner } from '@/components/ui/Feedback';
import { formatDayMonth } from '@/lib/dates';
import { getErrorMessage } from '@/services/apiClient';
import { attritionApi } from '@/services/attritionApi';
import type { AttritionAlert } from '@/types/assistant';
import { ATTRITION_DISCLAIMER, AttritionLevelBadge, ScoreDelta } from './AttritionBits';

/** Chi tiết một nhân viên: điểm theo tuần, các tín hiệu đóng góp vào điểm, và đoạn diễn giải bằng lời (gọi khi bấm nút). */
export function AttritionDrawer({ alert, onClose }: { alert: AttritionAlert | null; onClose: () => void }) {
  const [trend, setTrend] = useState<AttritionAlert[]>([]);
  const [trendError, setTrendError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [narrative, setNarrative] = useState<string | null>(null);
  const [narrativeError, setNarrativeError] = useState<string | null>(null);
  const [writing, setWriting] = useState(false);

  const userId = alert?.userId ?? null;
  useEffect(() => {
    setTrend([]);
    setTrendError(null);
    setNarrative(null);
    setNarrativeError(null);
    if (userId == null) return;
    let current = true;
    setLoading(true);
    attritionApi
      .trend(userId)
      .then(({ data }) => current && setTrend([...data].sort((a, b) => a.weekStart.localeCompare(b.weekStart))))
      .catch((err) => current && setTrendError(getErrorMessage(err)))
      .finally(() => current && setLoading(false));
    return () => {
      current = false;
    };
  }, [userId]);

  const points = useMemo(() => trend.map((t) => ({ week: formatDayMonth(t.weekStart), 'Điểm nguy cơ': t.score })), [trend]);

  const writeNarrative = async () => {
    if (userId == null) return;
    setWriting(true);
    setNarrativeError(null);
    try {
      setNarrative((await attritionApi.narrative(userId)).data.text);
    } catch (err) {
      setNarrativeError(getErrorMessage(err));
    } finally {
      setWriting(false);
    }
  };

  const signals = [...(alert?.signals ?? [])].sort((a, b) => b.contribution - a.contribution);
  const maxContribution = Math.max(1, ...signals.map((s) => s.contribution));

  return (
    <DetailDrawer
      open={alert !== null}
      onClose={onClose}
      label="Chi tiết nhân sự cần quan tâm"
      title={alert?.employeeName ?? ''}
      subtitle={alert ? `Tuần bắt đầu ${formatDayMonth(alert.weekStart)}` : undefined}
      purpose="info"
      width={620}
    >
      {alert && (
        <VStack gap={4}>
          <Notice status="info" title={ATTRITION_DISCLAIMER} />

          <HStack gap={6} vAlign="center">
            <VStack gap={0.5}>
              <Text type="supporting">Điểm nguy cơ</Text>
              <Heading level={2} type="display-2" className="tabular-nums">
                {alert.score}
                <span className="text-base font-normal text-secondary"> / 100</span>
              </Heading>
            </VStack>
            <VStack gap={1}>
              <Text type="supporting">Mức</Text>
              <AttritionLevelBadge level={alert.level} />
            </VStack>
            <VStack gap={1}>
              <Text type="supporting">So với tuần trước</Text>
              <ScoreDelta delta={alert.delta} />
            </VStack>
          </HStack>

          <Card padding={4}>
            <VStack gap={3}>
              <HStack hAlign="between" vAlign="center" gap={3}>
                <Heading level={4}>Diễn giải</Heading>
                <Button label={narrative ? 'Viết lại' : 'Diễn giải bằng lời'} variant="secondary" size="sm" icon={<Icon icon={Sparkles} />} isLoading={writing} onClick={() => void writeNarrative()} />
              </HStack>
              {narrative ? (
                <Text as="p" className="whitespace-pre-wrap">
                  {narrative}
                </Text>
              ) : (
                <Text type="supporting">{writing ? 'Trợ lý đang viết, có thể mất vài giây…' : 'Nhờ trợ lý AI tóm tắt các tín hiệu thành một đoạn ngắn kèm gợi ý trò chuyện.'}</Text>
              )}
              <Notice title={narrativeError} />
            </VStack>
          </Card>

          <VStack gap={2}>
            <Heading level={4}>Lý do</Heading>
            {alert.reasons.length === 0 ? (
              <Text type="supporting">Không có tín hiệu nào vượt mức bình thường.</Text>
            ) : (
              <VStack gap={1}>
                {alert.reasons.map((r) => (
                  <Text key={r}>• {r}</Text>
                ))}
              </VStack>
            )}
          </VStack>

          {signals.length > 0 && (
            <VStack gap={2}>
              <Heading level={4}>Các tín hiệu (2 tuần gần nhất so với nền 8 tuần trước)</Heading>
              {signals.map((s) => (
                <VStack key={s.key} gap={1}>
                  <HStack hAlign="between" gap={3}>
                    <Text weight="medium">{s.label}</Text>
                    <Text type="supporting" className="tabular-nums">
                      đóng góp {s.contribution.toFixed(1)} điểm
                    </Text>
                  </HStack>
                  <div className="h-1.5 overflow-hidden rounded-full bg-muted" role="presentation">
                    <div className="h-full rounded-full bg-[var(--color-accent)]" style={{ width: `${Math.max(2, (s.contribution / maxContribution) * 100)}%` }} />
                  </div>
                  {s.reason && <Text type="supporting">{s.reason}</Text>}
                </VStack>
              ))}
            </VStack>
          )}

          <VStack gap={2}>
            <Heading level={4}>Điểm theo tuần</Heading>
            <Notice title={trendError} />
            {loading && (
              <div className="flex justify-center py-8">
                <Spinner />
              </div>
            )}
            {!loading && points.length > 0 && (
              <div style={{ height: 200 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={points} margin={{ top: 8, right: 8, left: -20, bottom: 0 }}>
                    <CartesianGrid stroke={chartColors.grid} vertical={false} />
                    <XAxis dataKey="week" {...axisProps} />
                    <YAxis domain={[0, 100]} allowDecimals={false} {...axisProps} />
                    <Tooltip content={<ChartTooltip />} />
                    <Line type="monotone" dataKey="Điểm nguy cơ" stroke={chartColors.accent} strokeWidth={2} dot={{ r: 3 }} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            )}
            {!loading && !trendError && points.length === 0 && <Text type="supporting">Chưa có lịch sử điểm.</Text>}
          </VStack>
        </VStack>
      )}
    </DetailDrawer>
  );
}
