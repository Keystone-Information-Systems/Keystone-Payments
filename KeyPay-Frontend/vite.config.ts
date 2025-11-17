import { defineConfig } from 'vite'
import { fileURLToPath, URL } from 'url'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom'],
          adyen: ['@adyen/adyen-web'],
          mui: ['@mui/material', '@mui/icons-material'],
        },
      },
    },
  },
  define: {
    // Use relative API base so ngrok + Vite proxy work on Free
    'process.env.VITE_API_BASE_URL': JSON.stringify('https://t90bvzrzc3.execute-api.us-east-1.amazonaws.com/dev/api'),
    'process.env.VITE_ENVIRONMENT': JSON.stringify('test'),
    // Original environment variable code (commented):
    // 'process.env.VITE_API_BASE_URL': JSON.stringify(process.env.VITE_API_BASE_URL || 'http://localhost:5000'),
    // 'process.env.VITE_ENVIRONMENT': JSON.stringify(process.env.VITE_ENVIRONMENT || 'test'),
  },
  server: {
    port: 3000,
    host: true,
    open: true,
    strictPort: true,

    // ✅ Allow your ngrok host (Option A)
    allowedHosts: ['alfredo-frumentaceous-maniacally.ngrok-free.dev'],

    // ✅ Dev proxy: /api -> .NET backend on :5000
    // proxy: {
    //   '/api': {
    //     target: 'http://localhost:5000',
    //     changeOrigin: true,
    //     // optional: if your API expects /api prefix remove it by uncommenting:
    //     rewrite: path => path.replace(/^\/api/, ''),
    //   },
    // },

    // ✅ HMR over ngrok so hot-reload works behind https
    hmr: {
      host: 'alfredo-frumentaceous-maniacally.ngrok-free.dev',
      protocol: 'wss',
      port: 443,
    },
  },
  css: {
    devSourcemap: true,
  },
})
