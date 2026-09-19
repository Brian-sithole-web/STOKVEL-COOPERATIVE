import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const directoryName = fileURLToPath(new URL('.', import.meta.url))

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(directoryName, 'src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.API_PROXY || 'http://localhost:5187',
        changeOrigin: true,
      },
    },
  },
})
