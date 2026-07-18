import { useCallback, useEffect, useState } from 'react';

import { ApiError, apiFetch } from './api';

type ApiResult<T> = {
  data: T | undefined;
  error: string | undefined;
  loading: boolean;
  reload: () => void;
};

// Minimal fetch-on-mount hook for the thin slice's read screens. Deliberately not TanStack
// Query yet (the web uses it) — kept dependency-light until caching/refetch actually earns it.
export function useApi<T>(path: string): ApiResult<T> {
  const [data, setData] = useState<T | undefined>(undefined);
  const [error, setError] = useState<string | undefined>(undefined);
  const [loading, setLoading] = useState(true);
  const [nonce, setNonce] = useState(0);

  const reload = useCallback(() => setNonce((n) => n + 1), []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(undefined);
    apiFetch<T>(path)
      .then((result) => {
        if (!cancelled) setData(result);
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof ApiError ? e.message : 'Något gick fel');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [path, nonce]);

  return { data, error, loading, reload };
}
