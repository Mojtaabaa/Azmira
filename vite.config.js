import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { createApp } from './server/app.js'

function apiPlugin() {
  return {
    name: 'konkoor-api',
    configureServer(server) {
      server.middlewares.use(createApp())
    },
    configurePreviewServer(server) {
      server.middlewares.use(createApp())
    },
  }
}

export default defineConfig({
  plugins: [react(), apiPlugin()],
  server: {
    host: '127.0.0.1',
    port: 5173,
  },
})
