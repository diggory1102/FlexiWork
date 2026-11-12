import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Plus, Trash2, Wifi, WifiOff } from 'lucide-react';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { Badge, EmptyState, FormError, Spinner } from '@/components/ui/Feedback';
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
  const [deleting, setDeleting] = useState<BranchWifi | null>(null);

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
    if (!ssid.trim() || !bssid.trim()) return setError('Vui lòng nhập tên mạng và BSSID.');
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

  const remove = async () => {
    if (!deleting) return;
    try {
      await branchApi.removeWifi(deleting.branchId, deleting.id);
      toast.success('Đã xóa Wi-Fi.');
      setDeleting(null);
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err));
    }
  };

  return (
    <>
      <Modal
        open={!!branch && !deleting}
        onClose={onClose}
        size="lg"
        title={`Wi-Fi nội bộ — ${branch?.name ?? ''}`}
        description="Nhân viên chỉ được chấm công khi điện thoại bắt đúng một trong các BSSID (địa chỉ MAC router) đang bật dưới đây."
      >
        <VStack gap={4}>
          {canEdit && (
            <Card variant="muted" padding={4}>
              <form onSubmit={add} noValidate>
                <VStack gap={3}>
                  <FormError message={error} />
                  <HStack gap={3} wrap="wrap" vAlign="end">
                    <div className="min-w-48 flex-1">
                      <TextInput label="Tên mạng (SSID)" isRequired placeholder="VD: FlexiWork_Staff" value={ssid} onChange={setSsid} width="100%" />
                    </div>
                    <div className="min-w-48 flex-1">
                      <TextInput label="BSSID" isRequired placeholder="AA:BB:CC:DD:EE:FF" value={bssid} onChange={setBssid} width="100%" />
                    </div>
                    <Button label="Thêm" type="submit" variant="primary" icon={<Icon icon={Plus} />} isLoading={saving} />
                  </HStack>
                  <Text type="supporting">BSSID dạng AA:BB:CC:DD:EE:FF hoặc aa-bb-cc-dd-ee-ff.</Text>
                </VStack>
              </form>
            </Card>
          )}

          {loading ? (
            <HStack hAlign="center" padding={8}>
              <Spinner />
            </HStack>
          ) : wifis.length === 0 ? (
            <EmptyState title="Chưa khai báo Wi-Fi" description="Khi chưa có BSSID, lớp kiểm tra Wi-Fi sẽ không xác minh được nhân viên tại chi nhánh này." />
          ) : (
            <Card padding={0}>
              {wifis.map((w, index) => (
                <HStack key={w.id} gap={3} vAlign="center" padding={3} className={index > 0 ? 'border-t border-border' : undefined}>
                  <Icon icon={w.isActive ? Wifi : WifiOff} color={w.isActive ? 'success' : 'secondary'} />
                  <VStack gap={0.5} className="min-w-0 flex-1">
                    <Text weight="medium" maxLines={1}>
                      {w.ssid}
                    </Text>
                    <span className="font-mono text-xs text-secondary">{w.bssid}</span>
                  </VStack>
                  <Badge tone={w.isActive ? 'green' : 'gray'}>{w.isActive ? 'Đang dùng' : 'Tạm tắt'}</Badge>
                  {canEdit && (
                    <>
                      <Button label={w.isActive ? 'Tắt' : 'Bật'} size="sm" onClick={() => toggle(w)} />
                      <IconButton variant="ghost" size="sm" label="Xóa" tooltip="Xóa" icon={<Icon icon={Trash2} color="error" />} onClick={() => setDeleting(w)} />
                    </>
                  )}
                </HStack>
              ))}
            </Card>
          )}
        </VStack>
      </Modal>
      <ConfirmDialog
        open={!!deleting}
        title="Xóa Wi-Fi"
        message={
          <>
            Xóa Wi-Fi <b>{deleting?.ssid}</b> ({deleting?.bssid})? Nhân viên sẽ không chấm công được qua mạng này nữa.
          </>
        }
        confirmText="Xóa"
        onConfirm={remove}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}
