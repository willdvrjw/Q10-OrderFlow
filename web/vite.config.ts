import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Vite genera archivos estáticos; Nginx se ocupa del proxy en producción.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist',
  },
});
