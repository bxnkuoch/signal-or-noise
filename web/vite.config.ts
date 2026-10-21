import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development, forward API routes to the .NET API so the page and API share one origin (no CORS needed).
const api = 'http://localhost:5199'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/digest': api,
      '/stories': api,
    },
  },
})
