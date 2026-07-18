// API origin. Set EXPO_PUBLIC_API_URL for real devices and EAS builds — `localhost` won't
// resolve from a phone, so use the dev machine's LAN IP or a tunnel there. The fallback is the
// fixed dev API port (ADR-0008's `pnpm dev:api` :5000), which the iOS simulator / web can reach
// on the same host. Trailing slash trimmed so `${API_URL}${path}` never doubles up.
export const API_URL = (process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000').replace(/\/+$/, '');
