import { defineConfig, loadEnv } from 'vite'
import { fileURLToPath, URL } from 'node:url'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    // Serves the API on the dev server's own origin, as nginx does in production: Pandora Desktop
    // pointed at this server calls `<server>/api/...` with its device key.
    proxy: {
      '/api': { target: loadEnv(mode, process.cwd(), '').VITE_API_URL || 'https://localhost:61182', secure: false },
    },
  },
}))
