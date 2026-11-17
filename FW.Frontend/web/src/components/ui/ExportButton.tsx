import { useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Icon } from '@astryxdesign/core/Icon';
import { FileSpreadsheet } from 'lucide-react';
import { downloadFile, getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';

/** Nút tải tệp Excel từ API (gửi kèm JWT): hiện trạng thái đang xuất, báo lỗi bằng toast. */
export function ExportButton({
  url,
  params,
  label = 'Xuất Excel',
  isDisabled,
}: {
  url: string;
  params?: object;
  label?: string;
  isDisabled?: boolean;
}) {
  const [busy, setBusy] = useState(false);

  const run = async () => {
    setBusy(true);
    try {
      const name = await downloadFile(url, params);
      toast.success(`Đã tải xuống ${name}`);
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return <Button label={label} variant="secondary" icon={<Icon icon={FileSpreadsheet} />} isLoading={busy} isDisabled={isDisabled} onClick={run} />;
}
