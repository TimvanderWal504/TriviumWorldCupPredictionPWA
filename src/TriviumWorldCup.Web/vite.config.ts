import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

// When run via Aspire, these are injected automatically.
// Fallback values are used for standalone `npm run dev`.
const apiTarget = process.env['services__api__http__0'] ?? 'http://localhost:5009';
// Aspire injects PORT via .WithHttpEndpoint(env: "PORT"); fall back to 64505 for standalone use.
const devPort = parseInt(process.env['PORT'] ?? '64505');

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      // Register the SW in dev so push notifications can be tested locally.
      devOptions: { enabled: true },
      manifest: {
        name: 'Trivium World Cup 2026',
        short_name: 'TWC 2026',
        theme_color: '#003e78',
        background_color: '#003e78',
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
        importScripts: ['/push-handler.js'],
        runtimeCaching: [
          {
            // Network-first for API routes
            urlPattern: /\/(auth|profile|predictions|fixtures|teams|players|scores|leaderboard)(\/|$)/,
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
    port: devPort,
    // '::'  binds to all IPv6 interfaces; on Windows this dual-stacks over IPv4 too.
    // Required for Aspire's DCP proxy, which connects via [::1].
    host: '::',
    proxy: {
      '/api':         { target: apiTarget, changeOrigin: true },
      '/auth':        { target: apiTarget, changeOrigin: true },
      '/profile':     { target: apiTarget, changeOrigin: true },
      '/predictions': { target: apiTarget, changeOrigin: true },
      '/fixtures':    { target: apiTarget, changeOrigin: true },
      '/teams':       { target: apiTarget, changeOrigin: true },
      '/players':     { target: apiTarget, changeOrigin: true },
      '/scores':      { target: apiTarget, changeOrigin: true },
      '/leaderboard': { target: apiTarget, changeOrigin: true },
      '/knockout':    { target: apiTarget, changeOrigin: true },
      '/admin':       { target: apiTarget, changeOrigin: true },
      '/push':        { target: apiTarget, changeOrigin: true },
      '/e2e':         { target: apiTarget, changeOrigin: true },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: [],
  },
})
