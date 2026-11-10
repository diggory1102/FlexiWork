import type { UserStatus } from '@/types/user';

export const statusTone = (status: UserStatus) => (status === 'active' ? 'green' : status === 'suspended' ? 'red' : 'gray') as 'green' | 'red' | 'gray';
