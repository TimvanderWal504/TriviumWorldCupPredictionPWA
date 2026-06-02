import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      manifest: {
        name: 'Trivium World Cup 2026',
        short_name: 'TWC 2026',
        theme_color: '#1e293b',
        background_color: '#0f172a',
        display: 'standalone',
        start_url: '/',
        icons: [
          {
            src: '/icons/icon-192.png',
            sizes: '192x192',
            type: 'image/png',
          },
          {
            src: '/icons/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
          },
        ],
      },
      workbox: {
        runtimeCaching: [
          {
            // Network-first for API routes
            urlPattern: /\/(auth|profile|predictions|fixtures|teams|players)(\/|$)/,
            handler: 'NetworkFirst',
            options: {
              cacheName: 'api-cache',
              networkTimeoutSeconds: 10,
              expiration: {
                maxEntries: 50,
                maxAgeSeconds: 5 * 60, // 5 minutes
              },
            },
          },
          {
            // Cache-first for static assets (JS, CSS, fonts, images)
            urlPattern: /\.(?:js|css|woff2?|png|svg|ico)$/,
            handler: 'CacheFirst',
            options: {
              cacheName: 'static-assets',
              expiration: {
                maxEntries: 100,
                maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
              },
            },
          },
        ],
      },
    }),
  ],
  server: {
    proxy: {
      // Proxy /api/* to the .NET API during local development
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
      // Proxy /auth/* and /profile to the .NET API
      '/auth': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
      '/profile': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
      '/predictions': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
      '/fixtures': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
      '/teams': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
      '/players': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: [],
  },
})
