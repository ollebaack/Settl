import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'

// Build id baked into the bundle and emitted as version.json, so the running app
// can detect a newer deploy and offer a refresh — the no-service-worker update
// path (ADR-0027). CI may pin it (e.g. the git sha) via BUILD_ID.
const buildId = process.env.BUILD_ID ?? Date.now().toString(36)

// Emits /version.json alongside the build for the in-app update watcher to poll.
function versionManifest() {
  return {
    name: 'settl-version-manifest',
    apply: 'build' as const,
    generateBundle() {
      // @ts-expect-error — `this` is Rollup's plugin context at emit time.
      this.emitFile({
        type: 'asset',
        fileName: 'version.json',
        source: JSON.stringify({ version: buildId }),
      })
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(buildId),
  },
  plugins: [
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react(),
    tailwindcss(),
    versionManifest(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
})
