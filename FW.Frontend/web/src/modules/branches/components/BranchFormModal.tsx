import { useEffect, useState, type FormEvent } from 'react';
import { ExternalLink, LocateFixed } from 'lucide-react';
import { Modal } from '@/components/ui/Modal';
import { Button } from '@/components/ui/Button';
import { Field, FormError, Input, Select } from '@/components/ui/Form';
import { branchApi } from '@/services/organizationApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import type { Branch, BranchRequest, BranchStatus } from '@/types/organization';

const empty: BranchRequest = { branchCode: '', name: '', address: '', phone: '', email: '', latitude: 21.0285, longitude: 105.8542, radius: 50, status: 'active' };

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

  const submit = async (e: FormEvent) => {
    e.preventDefault();
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

  const mapUrl = `https://www.google.com/maps?q=${form.latitude},${form.longitude}`;

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      title={branch ? `Sửa chi nhánh ${branch.branchCode}` : 'Thêm chi nhánh'}
      description="Tâm tọa độ và bán kính dùng để kiểm tra lớp 1 (GPS Geofencing) khi nhân viên chấm công."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Hủy
          </Button>
          <Button type="submit" form="branch-form" loading={saving}>
            Lưu
          </Button>
        </>
      }
    >
      <form id="branch-form" onSubmit={submit} className="grid gap-4 md:grid-cols-2">
        <div className="md:col-span-2">
          <FormError message={error} />
        </div>
        <Field label="Mã chi nhánh" required hint="Tự chuyển thành chữ hoa, không trùng.">
          <Input value={form.branchCode} onChange={(e) => set('branchCode', e.target.value)} maxLength={20} required />
        </Field>
        <Field label="Tên chi nhánh" required>
          <Input value={form.name} onChange={(e) => set('name', e.target.value)} maxLength={150} required />
        </Field>
        <Field label="Địa chỉ" required className="md:col-span-2">
          <Input value={form.address} onChange={(e) => set('address', e.target.value)} maxLength={255} required />
        </Field>
        <Field label="Số điện thoại">
          <Input value={form.phone ?? ''} onChange={(e) => set('phone', e.target.value)} maxLength={15} />
        </Field>
        <Field label="Email">
          <Input type="email" value={form.email ?? ''} onChange={(e) => set('email', e.target.value)} maxLength={100} />
        </Field>

        <div className="rounded-lg border border-border bg-background/50 p-4 md:col-span-2">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
            <p className="text-sm font-medium">Vùng chấm công (Geofence)</p>
            <div className="flex gap-2">
              <Button type="button" size="sm" variant="secondary" loading={locating} icon={<LocateFixed className="h-3.5 w-3.5" />} onClick={useCurrentLocation}>
                Lấy vị trí hiện tại
              </Button>
              <a href={mapUrl} target="_blank" rel="noreferrer">
                <Button type="button" size="sm" variant="secondary" icon={<ExternalLink className="h-3.5 w-3.5" />}>
                  Xem trên bản đồ
                </Button>
              </a>
            </div>
          </div>
          <div className="grid gap-4 md:grid-cols-3">
            <Field label="Vĩ độ" required>
              <Input type="number" step="0.00000001" min={-90} max={90} value={form.latitude} onChange={(e) => set('latitude', Number(e.target.value))} required />
            </Field>
            <Field label="Kinh độ" required>
              <Input type="number" step="0.00000001" min={-180} max={180} value={form.longitude} onChange={(e) => set('longitude', Number(e.target.value))} required />
            </Field>
            <Field label="Bán kính (mét)" required hint="Từ 10 đến 5000 m.">
              <Input type="number" min={10} max={5000} value={form.radius} onChange={(e) => set('radius', Number(e.target.value))} required />
            </Field>
          </div>
        </div>

        {branch && (
          <Field label="Trạng thái">
            <Select value={form.status} onChange={(e) => set('status', e.target.value as BranchStatus)}>
              <option value="active">Đang hoạt động</option>
              <option value="inactive">Ngừng hoạt động</option>
            </Select>
          </Field>
        )}
      </form>
    </Modal>
  );
}
