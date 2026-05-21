"use client";

import { useEffect, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";

function RelayHandler() {
  const router = useRouter();
  const searchParams = useSearchParams();

  useEffect(() => {
    const clear = searchParams.get("clear");
    const token = searchParams.get("token");
    const refreshToken = searchParams.get("refreshToken");

    if (clear === "true") {
      localStorage.removeItem("attrition-token");
      localStorage.removeItem("attrition-refresh");
      localStorage.removeItem("attrition-theme-mode");
      localStorage.removeItem("attrition-accent");
      // clear cookies if any
      const domain = window.location.hostname.includes("hault.io.vn") ? "domain=.hault.io.vn;" : "";
      document.cookie = `attrition-token=; ${domain} path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
      document.cookie = `attrition-refresh=; ${domain} path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
      document.cookie = `attrition-theme-mode=; ${domain} path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
      document.cookie = `attrition-accent=; ${domain} path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
      document.cookie = `attrition-auth-state=logged-out; ${domain} path=/; max-age=86400; SameSite=Lax`;
      
      window.location.href = "/";
      return;
    }

    if (token && refreshToken) {
      // Store tokens from the Web app (keys must match api.ts)
      localStorage.setItem("attrition-token", token);
      localStorage.setItem("attrition-refresh", refreshToken);

      // Sync theme settings from Web app
      const themeMode = searchParams.get("themeMode");
      const accent = searchParams.get("accent");
      if (themeMode) localStorage.setItem("attrition-theme-mode", themeMode);
      if (accent) localStorage.setItem("attrition-accent", accent);

      window.location.href = "/";
    } else {
      // No tokens — redirect to web login
      const webUrl = process.env.NEXT_PUBLIC_WEB_URL || "http://localhost:3000";
      window.location.href = `${webUrl}/login?redirect=collection-relay`;
    }
  }, [router, searchParams]);

  return (
    <div style={{
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      minHeight: "100dvh",
      color: "var(--text-muted)",
      fontSize: "var(--text-sm)",
    }}>
      Authenticating...
    </div>
  );
}

export default function AuthRelayPage() {
  return (
    <Suspense>
      <RelayHandler />
    </Suspense>
  );
}
