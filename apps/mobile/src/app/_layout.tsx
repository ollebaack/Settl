import '../global.css';

import { Stack, useRouter, useSegments } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { AuthProvider, useAuth } from '@/lib/auth';

// Keeps the visible route in sync with auth state: signed-out users are pushed to /sign-in,
// signed-in users never sit on it. The thin slice has one gate; richer nav comes later.
function useAuthRedirect() {
  const { status } = useAuth();
  const segments = useSegments();
  const router = useRouter();

  useEffect(() => {
    if (status === 'loading') return;
    const onSignIn = segments[0] === 'sign-in';
    if (status === 'signedOut' && !onSignIn) router.replace('/sign-in');
    else if (status === 'signedIn' && onSignIn) router.replace('/');
  }, [status, segments, router]);
}

function RootNavigator() {
  const { status } = useAuth();
  useAuthRedirect();

  if (status === 'loading') {
    return (
      <View className="flex-1 items-center justify-center bg-white">
        <ActivityIndicator />
      </View>
    );
  }

  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="sign-in" />
      <Stack.Screen name="household/[id]" options={{ headerShown: true, headerBackTitle: 'Tillbaka' }} />
    </Stack>
  );
}

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <AuthProvider>
        <RootNavigator />
        <StatusBar style="auto" />
      </AuthProvider>
    </SafeAreaProvider>
  );
}
