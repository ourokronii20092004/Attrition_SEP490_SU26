import { defineConfig } from "vitest/config";
import { resolve } from "node:path";

export default defineConfig({
  resolve: {
    alias: { "@": resolve(__dirname, "src") },
  },
  test: {
    // Tier-1 targets are pure logic (formatters, error parsing, the API client with mocked fetch),
    // so the default node environment is enough — no jsdom needed yet.
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
