import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// https://vite.dev/config/
export default defineConfig({
    plugins: [react(), tailwindcss()],
    server: {
        proxy: {
            // Proxy API calls to the app service
            "/api": {
                target: process.env.API_HTTPS || process.env.API_HTTP,
                changeOrigin: true,
            },
        },
    },
});
