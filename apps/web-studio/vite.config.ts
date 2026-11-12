// SPDX-License-Identifier: Apache-2.0
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5174,
    strictPort: true,
    proxy: { '/api': { target: 'http://127.0.0.1:5217', changeOrigin: false } }
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts']
  }
});
