import { useMemo, useRef, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { InputGroup } from '@astryxdesign/core/InputGroup';
import { Typeahead, TypeaheadItem, type SearchableItem, type SearchSource } from '@astryxdesign/core/Typeahead';
import { LocateFixed, Search } from 'lucide-react';
import { branchApi } from '@/services/organizationApi';
import { getErrorMessage } from '@/services/apiClient';
import type { GeocodeCandidate } from '@/types/organization';

type PlaceItem = SearchableItem<GeocodeCandidate>;

interface Props {
  /** Ưu tiên gợi ý gần vị trí này (tâm Geofence hiện tại). */
  near: { latitude: number; longitude: number };
  onPick: (place: GeocodeCandidate) => void;
  /** Lấy vị trí hiện tại của trình duyệt làm tâm. */
  onLocate: () => void;
  locating: boolean;
}

/**
 * Hàng chọn tâm Geofence: ô tìm vị trí có gợi ý khi gõ (Geoapify qua backend — từ 3 ký tự, chờ ngừng gõ 0,5 s rồi mới tìm
 * để tiết kiệm hạn mức miễn phí) liền với nút "Vị trí hiện tại". Chọn một gợi ý thì điền tâm Geofence.
 */
export function PlaceSearch({ near, onPick, onLocate, locating }: Props) {
  const [value, setValue] = useState<PlaceItem | null>(null);
  const [error, setError] = useState<string | null>(null);
  const nearRef = useRef(near);
  nearRef.current = near;

  const source = useMemo<SearchSource<PlaceItem>>(
    () => ({
      search: async (query) => {
        try {
          const { data } = await branchApi.suggestPlaces(query, nearRef.current.latitude, nearRef.current.longitude);
          setError(null);
          return data.map((place, i) => ({
            id: `${place.latitude},${place.longitude},${i}`,
            label: place.type || place.displayName.split(',')[0],
            auxiliaryData: place,
          }));
        } catch (err) {
          setError(getErrorMessage(err));
          return [];
        }
      },
      bootstrap: () => [],
    }),
    [],
  );

  return (
    <InputGroup label="Tìm vị trí" isLabelHidden status={error ? { type: 'warning', message: error } : undefined}>
      <Typeahead
        label="Tìm vị trí"
        isLabelHidden
        placeholder="Tìm tên đường, tòa nhà, địa danh… VD: Vincom Bà Triệu"
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
        emptySearchText="Không tìm thấy vị trí phù hợp. Thử ghi thêm tên phường, quận."
        renderItem={(item) => <TypeaheadItem item={item} description={item.auxiliaryData?.displayName} />}
      />
      <Button label="Vị trí hiện tại" variant="secondary" icon={<Icon icon={LocateFixed} />} isLoading={locating} onClick={onLocate} />
    </InputGroup>
  );
}
