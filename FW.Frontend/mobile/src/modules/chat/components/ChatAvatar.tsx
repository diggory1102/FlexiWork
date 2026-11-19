import { StyleSheet, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import { colors } from '@/theme';
import type { Conversation } from '@/types/chat';

/** Ảnh phòng chat: người kia (chat 1-1) hoặc biểu tượng nhóm. */
export function ChatAvatar({ conversation, size = 46 }: { conversation: Pick<Conversation, 'type' | 'title' | 'avatarUrl'>; size?: number }) {
  if (conversation.type === 'direct') return <Avatar name={conversation.title} url={conversation.avatarUrl} size={size} />;
  return (
    <View style={[styles.group, { width: size, height: size, borderRadius: size / 2 }]}>
      <Ionicons name="people" size={size * 0.46} color={colors.primary} />
    </View>
  );
}

const styles = StyleSheet.create({
  group: { backgroundColor: colors.primarySoft, alignItems: 'center', justifyContent: 'center' },
});
