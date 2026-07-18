import { Stack, useLocalSearchParams } from 'expo-router';
import { ActivityIndicator, RefreshControl, ScrollView, Text, View } from 'react-native';

import { formatDate, formatMoney } from '@/lib/format';
import { balanceColorClass, netHeroLabel, relationLabel } from '@/lib/labels';
import type { Entry, HouseholdSummary } from '@/lib/types';
import { useApi } from '@/lib/use-api';

export default function HouseholdLedger() {
  const { id, name, currency } = useLocalSearchParams<{ id: string; name?: string; currency?: string }>();
  const cur = currency ?? 'SEK';
  const summary = useApi<HouseholdSummary>(`/households/${id}/summary`);
  const entries = useApi<Entry[]>(`/households/${id}/entries`);

  const loading = summary.loading || entries.loading;
  const error = summary.error ?? entries.error;
  const reload = () => {
    summary.reload();
    entries.reload();
  };

  return (
    <>
      <Stack.Screen options={{ title: name ?? 'Hushåll' }} />
      <ScrollView
        className="flex-1 bg-white"
        refreshControl={<RefreshControl refreshing={loading} onRefresh={reload} />}
      >
        <View className="gap-6 px-6 py-4">
          {error ? <Text className="text-sm text-red-600">{error}</Text> : null}

          {/* Net balance up top (product brief's two-level display) */}
          {summary.data ? (
            <View className="gap-3">
              <View className="rounded-2xl bg-neutral-900 px-5 py-5">
                <Text className="text-sm text-neutral-300">{netHeroLabel(summary.data.netLabel)}</Text>
                <Text className="mt-1 text-3xl font-bold text-white">
                  {formatMoney(Math.abs(Number(summary.data.overallNetMinor)), cur)}
                </Text>
              </View>
              {summary.data.people.map((p) => (
                <View
                  key={p.memberId}
                  className="flex-row items-center justify-between rounded-xl border border-neutral-200 px-4 py-3"
                >
                  <View>
                    <Text className="text-base text-neutral-800">{p.name}</Text>
                    <Text className="mt-0.5 text-xs text-neutral-500">{relationLabel(p.relation)}</Text>
                  </View>
                  <Text className={`text-base font-medium ${balanceColorClass(p.relation)}`}>
                    {formatMoney(Math.abs(Number(p.netMinor)), cur)}
                  </Text>
                </View>
              ))}
            </View>
          ) : loading ? (
            <View className="items-center py-10">
              <ActivityIndicator />
            </View>
          ) : null}

          {/* Itemized history underneath */}
          {entries.data ? (
            <View className="gap-2">
              <Text className="text-sm font-semibold uppercase tracking-wide text-neutral-400">Historik</Text>
              {entries.data.length === 0 ? (
                <Text className="py-6 text-center text-neutral-400">Inga poster än</Text>
              ) : (
                entries.data.map((e) => (
                  <View
                    key={e.id}
                    className="flex-row items-center justify-between border-b border-neutral-100 py-3"
                  >
                    <View className="flex-1 pr-3">
                      <Text className="text-base text-neutral-900">{e.title}</Text>
                      <Text className="mt-0.5 text-xs text-neutral-400">
                        {formatDate(e.date)}
                        {e.settled ? ' · Reglerad' : ''}
                      </Text>
                    </View>
                    <Text className="text-base font-medium text-neutral-800">
                      {formatMoney(e.amountMinor, cur)}
                    </Text>
                  </View>
                ))
              )}
            </View>
          ) : null}
        </View>
      </ScrollView>
    </>
  );
}
