"use client";

import { useEffect } from "react";
import { getAccessToken, getRefreshToken } from "@/lib/api";

/**
 * Collection Relay — bridges auth from Web → Collection.
 *
 * When a user logs in on the Web domain, this page reads the JWT tokens
 * from localStorage and redirects to the Collection domain's /auth/relay
 * with the tokens as URL params. The Collection relay page then stores
 * them in its own localStorage.
 *
 * Flow:
 *   1. Collection "Sign In" → Web /login?redirect=collection-relay
 *   2. User logs in on Web
 *   3. Web redirects to /collection-relay (this page)
 *   4. This page reads tokens → redirects to collection.hault.io.vn/auth/relay?token=...&refreshToken=...
 *   5. Collection relay stores tokens → redirects to /
 */
export default function CollectionRelayPage() {
  useEffect(() => {
    const collectionUrl =
      process.env.NEXT_PUBLIC_COLLECTION_URL || "https://collection.hault.io.vn";

    const token = getAccessToken();
    const refreshToken = getRefreshToken();

    if (token && refreshToken) {
      // Relay tokens + theme settings to Collection domain
      const params = new URLSearchParams({ token, refreshToken });
      // Sync theme settings
      const themeMode = localStorage.getItem("attrition-theme-mode");
      const accent = localStorage.getItem("attrition-accent");
      if (themeMode) params.set("themeMode", themeMode);
      if (accent) params.set("accent", accent);
      window.location.href = `${collectionUrl}/auth/relay?${params.toString()}`;
    } else {
      // Not logged in — send to collection and clear any old state
      window.location.href = `${collectionUrl}/auth/relay?clear=true`;
    }
  }, []);

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "100dvh",
        color: "var(--text-muted)",
        fontSize: "var(--text-sm)",
      }}
    >
      Redirecting to Collection...
    </div>
  );
}
