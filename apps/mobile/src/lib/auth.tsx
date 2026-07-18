import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import { requestToken, setUnauthorizedHandler } from './api';
import { clearTokens, loadTokens, saveTokens } from './tokens';

type Status = 'loading' | 'signedIn' | 'signedOut';

type AuthValue = {
  status: Status;
  signIn: (email: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<Status>('loading');

  // Bootstrap from the keychain once (persistent session — the 14-day-plus refresh means a
  // returning user skips the login screen).
  useEffect(() => {
    let cancelled = false;
    loadTokens().then((tokens) => {
      if (!cancelled) setStatus(tokens ? 'signedIn' : 'signedOut');
    });
    return () => {
      cancelled = true;
    };
  }, []);

  // A refresh failure deep in api.ts flips us to signed-out.
  useEffect(() => {
    setUnauthorizedHandler(() => setStatus('signedOut'));
    return () => setUnauthorizedHandler(null);
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const tokens = await requestToken(email, password);
    await saveTokens(tokens);
    setStatus('signedIn');
  }, []);

  const signOut = useCallback(async () => {
    await clearTokens();
    setStatus('signedOut');
  }, []);

  const value = useMemo<AuthValue>(() => ({ status, signIn, signOut }), [status, signIn, signOut]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider');
  return ctx;
}
