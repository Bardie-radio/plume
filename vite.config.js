import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import tailwindcss from "@tailwindcss/vite";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  build: {
    outDir: "wwwroot/dist",
    emptyOutDir: true,
    rollupOptions: {
      input: {
        main: resolve(root, "assets/main.js"),
        "credentials-reauth": resolve(root, "assets/credentials-reauth.js"),
      },
      output: {
        entryFileNames: "assets/[name].js",
        chunkFileNames: "assets/[name].js",
        assetFileNames: "assets/[name][extname]",
      },
    },
  },
});
