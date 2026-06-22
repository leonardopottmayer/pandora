import { defineConfig } from 'vitest/config'
import { fileURLToPath, URL } from 'node:url'
import react from '@vitejs/plugin-react'

// Vitest config kept separate from vite.config.ts so the test-only `env`
// (deterministic API base for MSW) and jsdom setup don't leak into the build.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    // v8 coverage instrumentation slows multi-interaction component tests well
    // past the 5s default; give them headroom so `test:cov` isn't flaky.
    testTimeout: 15000,
    // apiClient reads VITE_API_URL; pin it so MSW handlers match a known origin.
    env: { VITE_API_URL: 'http://localhost' },
  },
})
