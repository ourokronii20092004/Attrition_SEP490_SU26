import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  reactStrictMode: true,
  images: {
    // Media comes from the API host and arbitrary providers (e.g. Google avatars).
    // Skip the optimizer to avoid per-host allowlist churn in containerized deploys.
    unoptimized: true,
  },
  eslint: { ignoreDuringBuilds: true },
};

export default nextConfig;
