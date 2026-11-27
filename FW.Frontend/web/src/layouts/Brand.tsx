import iconUrl from '@/assets/logo-icon.png';
import wordmarkUrl from '@/assets/logo-wordmark.png';

export const SLOGAN = 'Flex your schedule. Own your work.';

/** Cắt từ ảnh logo rồi tô bằng màu nhấn của theme (mặt nạ), nên tự đổi theo sáng / tối. */
function Masked({ url, width, height }: { url: string; width: number; height: number }) {
  const mask = `url(${url}) center / contain no-repeat`;
  return <span aria-hidden="true" className="block shrink-0 bg-accent" style={{ width, height, mask, WebkitMask: mask }} />;
}

export function BrandIcon({ size = 36 }: { size?: number }) {
  return <Masked url={iconUrl} width={size} height={Math.round(size * 0.877)} />;
}

export function BrandWordmark({ width = 320 }: { width?: number }) {
  return <Masked url={wordmarkUrl} width={width} height={Math.round(width * 0.163)} />;
}
