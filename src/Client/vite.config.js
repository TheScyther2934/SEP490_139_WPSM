import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Dev: Vite on 5173 proxies /api to WPSM API (5128) so the refresh cookie stays same-origin.
// changeOrigin stays false: the API compares Origin with Host before rotating the refresh token.
// Build: output goes to src/API/wwwroot and the API serves it.
export default defineConfig({
  // No "@" alias: imports are relative so the editor resolves them without extra config.
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { "/api": { target: "http://localhost:5128", changeOrigin: false } },
  },
  build: { outDir: "../API/wwwroot", emptyOutDir: true },
});
