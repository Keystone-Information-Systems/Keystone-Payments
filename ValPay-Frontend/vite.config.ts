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
    // Hardcoded values - environment variables commented out
    'process.env.VITE_API_BASE_URL': JSON.stringify('http://localhost:5000'),
    'process.env.VITE_ADYEN_CLIENT_KEY': JSON.stringify('test_DVRLC56S3RCH5JWVCEBDLLJYEMGXT57T'),
    'process.env.VITE_ENVIRONMENT': JSON.stringify('test'),
    // Original environment variable code (commented):
    // 'process.env.VITE_API_BASE_URL': JSON.stringify(process.env.VITE_API_BASE_URL || 'http://localhost:5000'),
    // 'process.env.VITE_ADYEN_CLIENT_KEY': JSON.stringify(process.env.VITE_ADYEN_CLIENT_KEY || 'test_KPIEBM4JNFADJMJYJWUOV4ZHP457XA3T'),
    // 'process.env.VITE_ENVIRONMENT': JSON.stringify(process.env.VITE_ENVIRONMENT || 'test'),
  },
  server: {
    port: 3000,
    host: true,
    open: true,
    strictPort: true,
  },
  css: {
    devSourcemap: true,
  },
})
