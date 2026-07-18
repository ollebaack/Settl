import { useState } from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Platform, Pressable, Text, TextInput, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { ApiError } from '@/lib/api';
import { useAuth } from '@/lib/auth';

export default function SignIn() {
  const { signIn } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | undefined>(undefined);
  const [busy, setBusy] = useState(false);

  const canSubmit = email.trim().length > 0 && password.length > 0 && !busy;

  async function submit() {
    if (!canSubmit) return;
    setBusy(true);
    setError(undefined);
    try {
      await signIn(email.trim(), password);
      // On success the auth redirect swaps this screen for the household list.
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Kunde inte logga in');
    } finally {
      setBusy(false);
    }
  }

  return (
    <SafeAreaView className="flex-1 bg-white">
      <KeyboardAvoidingView className="flex-1" behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <View className="flex-1 justify-center gap-8 px-6">
          <View className="gap-1">
            <Text className="text-3xl font-bold text-neutral-900">Settl</Text>
            <Text className="text-base text-neutral-500">Logga in för att fortsätta</Text>
          </View>

          <View className="gap-3">
            <TextInput
              className="rounded-xl border border-neutral-200 bg-neutral-50 px-4 py-3 text-base text-neutral-900"
              placeholder="E-post"
              placeholderTextColor="#a3a3a3"
              autoCapitalize="none"
              autoCorrect={false}
              autoComplete="email"
              keyboardType="email-address"
              value={email}
              onChangeText={setEmail}
              editable={!busy}
            />
            <TextInput
              className="rounded-xl border border-neutral-200 bg-neutral-50 px-4 py-3 text-base text-neutral-900"
              placeholder="Lösenord"
              placeholderTextColor="#a3a3a3"
              secureTextEntry
              autoComplete="current-password"
              value={password}
              onChangeText={setPassword}
              editable={!busy}
              onSubmitEditing={submit}
              returnKeyType="go"
            />
          </View>

          {error ? <Text className="text-sm text-red-600">{error}</Text> : null}

          <Pressable
            accessibilityRole="button"
            disabled={!canSubmit}
            onPress={submit}
            className={`items-center rounded-xl py-4 ${canSubmit ? 'bg-neutral-900' : 'bg-neutral-300'}`}
          >
            {busy ? (
              <ActivityIndicator color="white" />
            ) : (
              <Text className="text-base font-semibold text-white">Logga in</Text>
            )}
          </Pressable>
        </View>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
