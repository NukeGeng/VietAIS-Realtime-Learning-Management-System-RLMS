import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_DEV_API_TARGET || 'https://localhost:7041'

  return {
    plugins: [vue()],
    resolve: { alias: { '@': '/src' } },
    server: {
      host: true,
      port: 5173,
      proxy: {
        '/api': { target: apiTarget, changeOrigin: true, secure: false },
        '/hubs': { target: apiTarget, changeOrigin: true, secure: false, ws: true }
      }
    },
    build: { sourcemap: false }
  }
})
