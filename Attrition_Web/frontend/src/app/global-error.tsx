"use client";

import { useEffect } from "react";

/**
 * Last-resort boundary for errors thrown in the root layout itself (where the normal
 * error.tsx can't render). Must include its own <html>/<body>. Kept dependency-free and
 * inline-styled because the app shell — including the stylesheet — may have failed to mount.
 */
export default function GlobalError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error("Global error:", error);
  }, [error]);

  return (
    <html lang="en">
      <body style={{ background: "#070b09", color: "#e7efe9", fontFamily: "system-ui, sans-serif", margin: 0 }}>
        <div style={{ minHeight: "100vh", display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: "1rem", padding: "2rem", textAlign: "center" }}>
          <h1 style={{ fontSize: "1.5rem", fontWeight: 700 }}>Something went wrong</h1>
          <p style={{ color: "#93a39a", maxWidth: "28rem" }}>
            The application hit an unexpected error. Please try again.
          </p>
          <button
            onClick={reset}
            style={{ background: "#38e8a0", color: "#04130c", border: "none", borderRadius: "0.5rem", padding: "0.6rem 1.2rem", fontWeight: 600, cursor: "pointer" }}
          >
            Try again
          </button>
        </div>
      </body>
    </html>
  );
}
