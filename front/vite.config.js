import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  base: '/expenses/',
  plugins: [vue()],
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      '/api': 'http://localhost:5023',
      '/expenses/api': {
        target: 'http://localhost:5023',
        rewrite: (path) => path.replace(/^\/expenses\/api/, '/api'),
      },
    },
  },
})
