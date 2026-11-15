import { useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid, GridSpan } from '@astryxdesign/core/Grid';
import { Link } from '@astryxdesign/core/Link';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { Selector } from '@astryxdesign/core/Selector';
import { VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Modal } from '@/components/ui/Modal';
import { PlaceSearch } from './PlaceSearch';
import { FormError } from '@/components/ui/Feedback';
import { branchApi } from '@/services/organizationApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { branchStatusLabels } from '@/lib/format';
import type { Branch, BranchRequest, BranchStatus, GeocodeCandidate } from '@/types/organization';

const empty: BranchRequest = { branchCode: '', name: '', address: '', phone: '', email: '', latitude: 21.0285, longitude: 105.8542, radius: 50, status: 'active' };

const statusOptions = [
  { value: 'active', label: branchStatusLabels.active },
  { value: 'inactive', label: branchStatusLabels.inactive },
];

/** Khung bản đồ OpenStreetMap quanh tâm (độ) — đủ thấy vài dãy phố quanh chi nhánh. */
const MAP_SPAN = { lat: 0.0025, lng: 0.0035 };

/** Bản đồ OpenStreetMap nhúng có ghim tại tâm Geofence (chỉ để xem; chỉnh tâm bằng ô tìm vị trí hoặc Vĩ độ / Kinh độ). */
function GeofenceMapPreview({ latitude, longitude }: { latitude: number; longitude: number }) {
  // Chờ người dùng gõ xong mới tải lại bản đồ
  const [center, setCenter] = useState({ latitude, longitude });
  useEffect(() => {
    const id = setTimeout(() => setCenter({ latitude, longitude }), 600);
    return () => clearTimeout(id);
  }, [latitude, longitude]);

  const valid = Math.abs(center.latitude) <= 90 && Math.abs(center.longitude) <= 180;
  if (!valid) return null;
  const bbox = [
    center.longitude - MAP_SPAN.lng,
    center.latitude - MAP_SPAN.lat,
    center.longitude + MAP_SPAN.lng,
    center.latitude + MAP_SPAN.lat,
  ].join(',');
  const src = `https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${center.latitude},${center.longitude}`;
  return (
    <VStack gap={1}>
      <iframe
        key={src}
        title="Bản đồ vị trí chi nhánh"
        src={src}
        loading="lazy"
        tabIndex={-1}
        // Chỉ để xem: không nhận chuột, nên lăn chuột trên bản đồ vẫn cuộn form thay vì phóng to bản đồ
        style={{ width: '100%', height: 240, border: 0, borderRadius: 8, display: 'block', pointerEvents: 'none' }}
      />
      <Text type="supporting" size="sm">
        © OpenStreetMap · Gợi ý vị trí: Geoapify ·{' '}
        <Link href={`https://www.google.com/maps?q=${latitude},${longitude}`} target="_blank" rel="noreferrer">
          Mở Google Maps
        </Link>
      </Text>
    </VStack>
  );
}

interface Props {
  open: boolean;
  branch: Branch | null; // null = tạo mới
  onClose: () => void;
  onSaved: () => void;
}

export function BranchFormModal({ open, branch, onClose, onSaved }: Props) {
  const [form, setForm] = useState<BranchRequest>(empty);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [locating, setLocating] = useState(false);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setForm(branch ? { ...branch, phone: branch.phone ?? '', email: branch.email ?? '' } : empty);
  }, [open, branch]);

  const set = <K extends keyof BranchRequest>(key: K, value: BranchRequest[K]) => setForm((f) => ({ ...f, [key]: value }));

  const useCurrentLocation = () => {
    if (!navigator.geolocation) return toast.error('Trình duyệt không hỗ trợ định vị.');
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setForm((f) => ({ ...f, latitude: +pos.coords.latitude.toFixed(8), longitude: +pos.coords.longitude.toFixed(8) }));
        setLocating(false);
      },
      () => {
        toast.error('Không lấy được vị trí. Hãy cho phép trình duyệt truy cập vị trí.');
        setLocating(false);
      },
      { enableHighAccuracy: true, timeout: 10_000 },
    );
  };

  /** Chọn một vị trí ở ô tìm → điền tâm; chưa nhập địa chỉ thì dùng luôn địa chỉ của vị trí đó (bỏ đuôi ", Việt Nam"). */
  const pickPlace = (place: GeocodeCandidate) =>
    setForm((f) => ({
      ...f,
      latitude: place.latitude,
      longitude: place.longitude,
      address: f.address.trim() ? f.address : place.displayName.replace(/,\s*Việt Nam$/i, ''),
    }));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!form.branchCode.trim() || !form.name.trim() || !form.address.trim()) return setError('Vui lòng nhập mã, tên và địa chỉ chi nhánh.');
    setSaving(true);
    setError(null);
    try {
      const body = { ...form, phone: form.phone || null, email: form.email || null };
      const { message } = branch ? await branchApi.update(branch.id, body) : await branchApi.create(body);
      toast.success(message);
      onSaved();
      onClose();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      purpose="form"
      title={branch ? `Sửa chi nhánh ${branch.branchCode}` : 'Thêm chi nhánh'}
      description="Tâm tọa độ và bán kính dùng để kiểm tra lớp 1 (GPS Geofencing) khi nhân viên chấm công."
      footer={
        <>
          <Button label="Hủy" variant="secondary" onClick={onClose} />
          <Button label="Lưu" type="submit" form="branch-form" variant="primary" isLoading={saving} />
        </>
      }
    >
      <form id="branch-form" onSubmit={submit} noValidate>
        <VStack gap={4}>
          <FormError message={error} />
          <Grid columns={{ minWidth: 240, max: 2 }} gap={4}>
            {/* Gợi ý đặt ở labelTooltip (không dùng description) để ô nhập thẳng hàng với ô bên cạnh */}
            <TextInput
              label="Mã chi nhánh"
              isRequired
              labelTooltip="Tự chuyển thành chữ hoa, không được trùng với chi nhánh khác."
              placeholder="VD: CG01"
              value={form.branchCode}
              onChange={(v) => set('branchCode', v)}
            />
            <TextInput label="Tên chi nhánh" isRequired value={form.name} onChange={(v) => set('name', v)} />
            <GridSpan columns="full">
              <TextInput label="Địa chỉ" isRequired value={form.address} onChange={(v) => set('address', v)} />
            </GridSpan>
            <TextInput label="Số điện thoại" type="tel" value={form.phone ?? ''} onChange={(v) => set('phone', v)} />
            <TextInput label="Email" type="email" value={form.email ?? ''} onChange={(v) => set('email', v)} />
          </Grid>

          <Card variant="muted" padding={4}>
            <VStack gap={4}>
              <VStack gap={0.5}>
                <Heading level={4}>Vùng chấm công (Geofence)</Heading>
                <Text type="supporting">Nhân viên phải đứng trong bán kính này quanh tâm. Tìm vị trí chi nhánh hoặc đứng tại chi nhánh và lấy vị trí hiện tại.</Text>
              </VStack>
              <PlaceSearch
                key={open ? `place-${branch?.id ?? 'new'}` : 'closed'}
                near={{ latitude: form.latitude, longitude: form.longitude }}
                onPick={pickPlace}
                onLocate={useCurrentLocation}
                locating={locating}
              />
              {/* Tọa độ bên trái, bản đồ bên phải (màn hình hẹp thì xuống dòng) */}
              <Grid columns={{ minWidth: 260, max: 2 }} gap={4}>
                <VStack gap={3}>
                  <NumberInput label="Vĩ độ" isRequired min={-90} max={90} step={0.000001} value={form.latitude} onChange={(v) => set('latitude', v ?? 0)} />
                  <NumberInput label="Kinh độ" isRequired min={-180} max={180} step={0.000001} value={form.longitude} onChange={(v) => set('longitude', v ?? 0)} />
                  <NumberInput
                    label="Bán kính"
                    isRequired
                    units="m"
                    min={10}
                    max={5000}
                    isIntegerOnly
                    labelTooltip="Từ 10 đến 5000 m. Nên đặt 50–100 m vì GPS điện thoại thường sai lệch 10–30 m."
                    value={form.radius}
                    onChange={(v) => set('radius', v ?? 50)}
                  />
                </VStack>
                <GeofenceMapPreview latitude={form.latitude} longitude={form.longitude} />
              </Grid>
            </VStack>
          </Card>

          {branch && (
            <Selector label="Trạng thái" width={240} options={statusOptions} value={form.status} onChange={(v) => v && set('status', v as BranchStatus)} />
          )}
        </VStack>
      </form>
    </Modal>
  );
}
