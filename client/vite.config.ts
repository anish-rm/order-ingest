import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// /api/* is proxied to the ASP.NET Core API in dev, so the client needs no
// CORS setup and no hardcoded API origin.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
