import { Link } from 'expo-router';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useAuth } from '@/lib/auth';
import { formatMoney } from '@/lib/format';
import { balanceColorClass, netSubLabel } from '@/lib/labels';
import type { HouseholdListItem } from '@/lib/types';
import { useApi } from '@/lib/use-api';

export default function Households() {
  const { signOut } = useAuth();
  const { data, error, loading, reload } = useApi<HouseholdListItem[]>('/households');

  return (
    <SafeAreaView className="flex-1 bg-white" edges={['top']}>
      <View className="flex-row items-center justify-between px-6 py-2">
        <Text className="text-2xl font-bold text-neutral-900">Hushåll</Text>
        <Pressable accessibilityRole="button" hitSlop={8} onPress={signOut}>
          <Text className="text-sm font-medium text-neutral-500">Logga ut</Text>
        </Pressable>
      </View>

      <ScrollView refreshControl={<RefreshControl refreshing={loading} onRefresh={reload} />}>
        <View className="gap-3 px-6 pb-8 pt-2">
          {error ? (
            <Text className="text-sm text-red-600">{error}</Text>
          ) : !data && loading ? (
            <View className="items-center py-16">
              <ActivityIndicator />
            </View>
          ) : data && data.length === 0 ? (
            <Text className="py-16 text-center text-neutral-400">Inga hushåll än</Text>
          ) : (
            data?.map((h) => (
              <Link
                key={h.id}
                href={{
                  pathname: '/household/[id]',
                  params: { id: h.id, name: h.name, currency: h.currency },
                }}
                asChild
              >
                <Pressable className="rounded-2xl border border-neutral-200 bg-white px-4 py-4 active:bg-neutral-50">
                  <Text className="text-lg font-semibold text-neutral-900">{h.name}</Text>
                  <Text className="mt-0.5 text-sm text-neutral-500">{h.memberNames.join(', ')}</Text>
                  <View className="mt-2 flex-row items-baseline gap-2">
                    <Text className={`text-base font-semibold ${balanceColorClass(h.netLabel)}`}>
                      {formatMoney(Math.abs(Number(h.netMinor)), h.currency)}
                    </Text>
                    <Text className="text-sm text-neutral-500">{netSubLabel(h.netLabel)}</Text>
                  </View>
                </Pressable>
              </Link>
            ))
          )}
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
