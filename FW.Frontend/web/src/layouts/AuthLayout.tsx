import type { ComponentType, CSSProperties, ReactNode, SVGProps } from 'react';
import { Card } from '@astryxdesign/core/Card';
import { Center } from '@astryxdesign/core/Center';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { Fingerprint, MapPin, Wifi } from 'lucide-react';
import { BrandIcon, BrandWordmark, SLOGAN } from '@/layouts/Brand';
import { brandPanel, surface } from '@/theme/palette';

const bandStyle = {
  background: brandPanel.band,
  color: brandPanel.bandText,
  '--color-text-primary': brandPanel.bandText,
  '--color-text-secondary': brandPanel.bandText,
} as CSSProperties;

/**
 * Khung trang đăng nhập / quên mật khẩu / đổi mật khẩu tạm:
 * bên trái logo trên nền kem + dải giới thiệu màu đỏ (chỉ hiện trên màn hình rộng), bên phải form trong Card.
 */
export function AuthLayout({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="flex min-h-full">
      <section className="hidden w-[44%] flex-col lg:flex">
        <div className="flex flex-1 flex-col items-center justify-center gap-4 p-4" style={{ background: `light-dark(${surface.chrome[0]}, ${surface.chrome[1]})` }}>
          <BrandIcon size={240} />
          <BrandWordmark width={360} />
          <Text color="secondary">SINCE 2026</Text>
        </div>
        <div className="flex flex-col gap-4 p-10" style={bandStyle}>
          <Heading level={2} type="display-3">
            {SLOGAN}
          </Heading>
          <VStack gap={2}>
            <Feature icon={MapPin} text="Định vị GPS Geofencing theo bán kính chi nhánh" />
            <Feature icon={Wifi} text="Đối soát Wi-Fi BSSID nội bộ chống Fake GPS" />
            <Feature icon={Fingerprint} text="Nhận diện khuôn mặt bằng AI chống chấm hộ" />
          </VStack>
          <Text type="supporting">© {new Date().getFullYear()} FlexiWork — Khóa luận tốt nghiệp, Trường Đại học Thăng Long</Text>
        </div>
      </section>
      <Center axis="both" padding={6} className="flex-1 bg-body">
        <VStack gap={6} width="100%" maxWidth={420}>
          <div className="lg:hidden">
            <HStack gap={3} vAlign="center">
              <BrandIcon size={48} />
              <Text weight="bold">FlexiWork</Text>
            </HStack>
          </div>
          <Card padding={8} width="100%">
            <VStack gap={5}>
              <VStack gap={1}>
                <Heading level={1}>{title}</Heading>
                {subtitle && (
                  <Text type="supporting" as="p">
                    {subtitle}
                  </Text>
                )}
              </VStack>
              {children}
            </VStack>
          </Card>
        </VStack>
      </Center>
    </div>
  );
}

function Feature({ icon, text }: { icon: ComponentType<SVGProps<SVGSVGElement>>; text: string }) {
  return (
    <HStack gap={3} vAlign="center">
      <span className="flex h-6 w-6 items-center justify-center">
        <Icon icon={icon} size="sm" color="inherit" />
      </span>
      <Text>{text}</Text>
    </HStack>
  );
}
