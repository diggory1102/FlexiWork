import { useMemo, useRef, useState } from 'react';
import { Link } from '@astryxdesign/core/Link';
import { VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Typeahead, TypeaheadItem, type SearchableItem, type SearchSource, type TypeaheadStatus } from '@astryxdesign/core/Typeahead';
import { Search } from 'lucide-react';
import { branchApi } from '@/services/organizationApi';
import { getErrorMessage } from '@/services/apiClient';
import type { GeocodeCandidate } from '@/types/organization';

type PlaceItem = SearchableItem<GeocodeCandidate>;

interface Props {
  /** Ưu tiên gợi ý gần vị trí này (tâm Geofence hiện tại). */
  near: { latitude: number; longitude: number };
  onPick: (place: GeocodeCandidate) => void;
}

/**
 * Ô tìm địa điểm có gợi ý khi gõ (Geoapify qua backend — từ 3 ký tự, chờ ngừng gõ 0,5 s rồi mới tìm để tiết kiệm hạn mức
 * miễn phí). Chọn một gợi ý thì điền tâm Geofence.
 */
export function PlaceSearch({ near, onPick }: Props) {
  const [value, setValue] = useState<PlaceItem | null>(null);
  const [status, setStatus] = useState<TypeaheadStatus | undefined>();
  const nearRef = useRef(near);
  nearRef.current = near;

  const source = useMemo<SearchSource<PlaceItem>>(
    () => ({
      search: async (query) => {
        try {
          const { data } = await branchApi.suggestPlaces(query, nearRef.current.latitude, nearRef.current.longitude);
          setStatus(undefined);
          return data.map((place, i) => ({
            id: `${place.latitude},${place.longitude},${i}`,
            label: place.type || place.displayName.split(',')[0],
            auxiliaryData: place,
          }));
        } catch (err) {
          setStatus({ type: 'warning', message: getErrorMessage(err) });
          return [];
        }
      },
      bootstrap: () => [],
    }),
    [],
  );

  return (
    <VStack gap={1}>
      <Typeahead
        label="Tìm địa điểm"
        placeholder="Gõ tên đường, tòa nhà, địa danh… VD: Vincom Bà Triệu"
        startIcon={Search}
        searchSource={source}
        value={value}
        onChange={(item) => {
          setValue(item);
          if (item?.auxiliaryData) onPick(item.auxiliaryData);
        }}
        minQueryLength={3}
        debounceMs={500}
        maxMenuItems={6}
        status={status}
        statusVariant="detached"
        emptySearchText="Không tìm thấy địa điểm phù hợp. Thử ghi thêm tên phường, quận."
        renderItem={(item) => <TypeaheadItem item={item} description={item.auxiliaryData?.displayName} />}
      />
      <Text type="supporting" size="sm">
        Gợi ý địa điểm: Powered by <Link href="https://www.geoapify.com/" target="_blank" rel="noreferrer">Geoapify</Link> · © OpenStreetMap contributors
      </Text>
    </VStack>
  );
}
