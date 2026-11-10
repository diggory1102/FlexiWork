import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Plus, Trash2, Wifi, WifiOff } from 'lucide-react';
import { Modal } from '@/components/ui/Modal';
import { Button, IconButton } from '@/components/ui/Button';
import { Field, FormError, Input } from '@/components/ui/Form';
import { Badge, EmptyState, Spinner } from '@/components/ui/Feedback';
import { branchApi } from '@/services/organizationApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import type { Branch, BranchWifi } from '@/types/organization';

interface Props {
  branch: Branch | null;
  canEdit: boolean;
  onClose: () => void;
}

/** Danh sách Wi-Fi nội bộ hợp lệ của chi nhánh — dùng cho lớp 2 (Wi-Fi BSSID) khi chấm công. */
export function BranchWifiModal({ branch, canEdit, onClose }: Props) {
  const [wifis, setWifis] = useState<BranchWifi[]>([]);
  const [loading, setLoading] = useState(false);
  const [ssid, setSsid] = useState('');
  const [bssid, setBssid] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!branch) return;
    setLoading(true);
    try {
      const { data } = await branchApi.get(branch.id);
      setWifis(data.wifis);
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [branch]);

  useEffect(() => {
    setSsid('');
    setBssid('');
    setError(null);
    void load();
  }, [load]);

  const add = async (e: FormEvent) => {
    e.preventDefault();
    if (!branch) return;
    setSaving(true);
    setError(null);
    try {
      await branchApi.addWifi(branch.id, { ssid, bssid, isActive: true });
      toast.success('Đã thêm Wi-Fi.');
      setSsid('');
      setBssid('');
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const toggle = async (wifi: BranchWifi) => {
    try {
      await branchApi.updateWifi(wifi.branchId, wifi.id, { ssid: wifi.ssid, bssid: wifi.bssid, isActive: !wifi.isActive });
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  const remove = async (wifi: BranchWifi) => {
    if (!window.confirm(`Xóa Wi-Fi "${wifi.ssid}" (${wifi.bssid})?`)) return;
    try {
      await branchApi.removeWifi(wifi.branchId, wifi.id);
      toast.success('Đã xóa Wi-Fi.');
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  return (
    <Modal
      open={!!branch}
      onClose={onClose}
      size="lg"
      title={`Wi-Fi nội bộ — ${branch?.name ?? ''}`}
      description="Nhân viên chỉ được chấm công khi điện thoại bắt đúng một trong các BSSID (địa chỉ MAC router) đang bật dưới đây."
    >
      {canEdit && (
        <form onSubmit={add} className="mb-4 grid gap-3 rounded-lg border border-border bg-background/50 p-4 md:grid-cols-[1fr_1fr_auto] md:items-end">
          <div className="md:col-span-3">
            <FormError message={error} />
          </div>
          <Field label="Tên mạng (SSID)" required>
            <Input value={ssid} onChange={(e) => setSsid(e.target.value)} maxLength={100} placeholder="VD: FlexiWork_Staff" required />
          </Field>
          <Field label="BSSID" required hint="Dạng AA:BB:CC:DD:EE:FF hoặc aa-bb-cc-dd-ee-ff.">
            <Input value={bssid} onChange={(e) => setBssid(e.target.value)} placeholder="AA:BB:CC:DD:EE:FF" className="font-mono" required />
          </Field>
          <Button type="submit" loading={saving} icon={<Plus className="h-4 w-4" />} className="md:mb-5">
            Thêm
          </Button>
        </form>
      )}

      {loading ? (
        <div className="flex justify-center py-10">
          <Spinner />
        </div>
      ) : wifis.length === 0 ? (
        <EmptyState title="Chưa khai báo Wi-Fi" description="Khi chưa có BSSID, lớp kiểm tra Wi-Fi sẽ không xác minh được nhân viên tại chi nhánh này." />
      ) : (
        <ul className="divide-y divide-border rounded-lg border border-border">
          {wifis.map((w) => (
            <li key={w.id} className="flex items-center gap-3 px-4 py-3">
              {w.isActive ? <Wifi className="h-5 w-5 text-success" /> : <WifiOff className="h-5 w-5 text-muted" />}
              <div className="min-w-0 flex-1">
                <p className="truncate font-medium">{w.ssid}</p>
                <p className="font-mono text-xs text-muted">{w.bssid}</p>
              </div>
              <Badge tone={w.isActive ? 'green' : 'gray'}>{w.isActive ? 'Đang dùng' : 'Tạm tắt'}</Badge>
              {canEdit && (
                <>
                  <Button size="sm" variant="secondary" onClick={() => toggle(w)}>
                    {w.isActive ? 'Tắt' : 'Bật'}
                  </Button>
                  <IconButton label="Xóa" tone="danger" onClick={() => remove(w)}>
                    <Trash2 className="h-4 w-4" />
                  </IconButton>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
    </Modal>
  );
}
