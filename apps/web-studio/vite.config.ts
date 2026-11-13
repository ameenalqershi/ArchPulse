// SPDX-License-Identifier: Apache-2.0
import { defineConfig } from 'vitest/config';
import { loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', 'ARCHPULSE_');
  const port = Number(env.ARCHPULSE_LOCAL_GRAPH_PORT ?? 5217);
  if (!Number.isInteger(port) || port < 1 || port > 65535)
    throw new Error('ARCHPULSE_LOCAL_GRAPH_PORT must be a valid local port.');
  return {
    plugins: [react()],
    server: {
      host: '127.0.0.1',
      port: 5174,
      strictPort: true,
      proxy: { '/api': { target: `http://127.0.0.1:${port}`, changeOrigin: false } }
    },
    test: {
      environment: 'node',
      include: ['src/**/*.test.ts']
    }
  };
});
