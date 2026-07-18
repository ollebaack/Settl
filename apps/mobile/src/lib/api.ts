import { API_URL } from './config';
import { clearTokens, loadTokens, saveTokens, type Tokens } from './tokens';
import type { AccessTokenResponse } from './types';

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

// The AuthProvider registers a handler so a dead session (refresh failed) flips the UI to
// signed-out without api.ts importing React. Decouples the fetch layer from navigation.
let onUnauthorized: (() => void) | null = null;
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler;
}

async function parseTokens(res: Response): Promise<Tokens> {
  const body = (await res.json()) as AccessTokenResponse;
  return { accessToken: body.accessToken, refreshToken: body.refreshToken };
}

/** Exchange email/password for a bearer pair (POST /auth/token, ADR-0005). */
export async function requestToken(email: string, password: string): Promise<Tokens> {
  const res = await fetch(`${API_URL}/auth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (res.status === 401) throw new ApiError(401, 'Fel e-post eller lösenord');
  if (!res.ok) throw new ApiError(res.status, 'Kunde inte logga in');
  return parseTokens(res);
}

async function tryRefresh(refreshToken: string): Promise<boolean> {
  const res = await fetch(`${API_URL}/auth/token/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  });
  if (!res.ok) return false;
  await saveTokens(await parseTokens(res));
  return true;
}

/**
 * Authenticated fetch against the API. Attaches the bearer token; on a 401 it refreshes once
 * and retries (ADR-0005). If the refresh also fails, tokens are cleared and the session is
 * reported lost. Non-2xx responses throw an {@link ApiError} carrying the API's ProblemDetails
 * `detail` when present.
 */
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const tokens = await loadTokens();

  const send = (accessToken?: string) =>
    fetch(`${API_URL}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...init.headers,
      },
    });

  let res = await send(tokens?.accessToken);

  if (res.status === 401 && tokens?.refreshToken && (await tryRefresh(tokens.refreshToken))) {
    const refreshed = await loadTokens();
    res = await send(refreshed?.accessToken);
  }

  if (res.status === 401) {
    await clearTokens();
    onUnauthorized?.();
    throw new ApiError(401, 'Sessionen har gått ut');
  }

  if (!res.ok) {
    let detail = `Något gick fel (${res.status})`;
    try {
      const problem = (await res.json()) as { detail?: string };
      if (problem?.detail) detail = problem.detail;
    } catch {
      // non-JSON error body — keep the generic message
    }
    throw new ApiError(res.status, detail);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
