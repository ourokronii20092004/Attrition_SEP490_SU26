import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",

  // Proxy API calls to the backend
  async rewrites() {
    const apiUrl = process.env.API_URL || "http://localhost:5000";
    return [
      {
        source: "/api/:path*",
        destination: `${apiUrl}/api/:path*`,
      },
      {
        source: "/uploads/:path*",
        destination: `${apiUrl}/uploads/:path*`,
      },
      {
        source: "/hubs/:path*",
        destination: `${apiUrl}/hubs/:path*`,
      },
    ];
  },

  // Image optimization for avatars and wiki images
  images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: "lh3.googleusercontent.com",
      },
      {
        protocol: "https",
        hostname: "attrition.hault.io.vn",
      },
    ],
  },

  // Strict mode for better development experience
  reactStrictMode: true,
};

export default nextConfig;
