import type { ComponentType, ReactNode, SVGProps } from 'react';
import { Card } from '@astryxdesign/core/Card';
import { Center } from '@astryxdesign/core/Center';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { Theme } from '@astryxdesign/core/theme';
import { Fingerprint, MapPin, Wifi } from 'lucide-react';
import { Logo } from '@/components/common/Logo';
import { flexiworkTheme } from '@/theme/flexiworkTheme';

/**
 * Khung trang đăng nhập / quên mật khẩu / đổi mật khẩu tạm:
 * bên trái panel giới thiệu (theme tối lồng nhau, chỉ hiện trên màn hình rộng), bên phải form trong Card.
 */
export function AuthLayout({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="flex min-h-full">
      <Theme theme={flexiworkTheme} mode="dark">
        <section className="hidden w-[44%] flex-col justify-between bg-body p-10 lg:flex">
          <Logo />
          <VStack gap={4}>
            <Heading level={2} type="display-3">
              Chấm công thông minh, quản lý ca linh hoạt.
            </Heading>
            <Text color="secondary" as="p">
              Xác thực chấm công 3 lớp trên chính điện thoại của nhân viên — chống chấm công hộ và giả mạo vị trí.
            </Text>
            <VStack gap={3} className="mt-4">
              <Feature icon={MapPin} text="Định vị GPS Geofencing theo bán kính chi nhánh" />
              <Feature icon={Wifi} text="Đối soát Wi-Fi BSSID nội bộ chống Fake GPS" />
              <Feature icon={Fingerprint} text="Nhận diện khuôn mặt bằng AI chống chấm hộ" />
            </VStack>
          </VStack>
          <Text type="supporting">© {new Date().getFullYear()} FlexiWork — Khóa luận tốt nghiệp, Trường Đại học Thăng Long</Text>
        </section>
      </Theme>

      <Center axis="both" padding={6} className="flex-1 bg-body">
        <VStack gap={6} width="100%" maxWidth={420}>
          <div className="lg:hidden">
            <Logo />
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
      <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-muted">
        <Icon icon={icon} size="sm" color="accent" />
      </span>
      <Text>{text}</Text>
    </HStack>
  );
}
