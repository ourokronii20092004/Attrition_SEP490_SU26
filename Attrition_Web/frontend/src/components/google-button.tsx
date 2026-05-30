"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { GOOGLE_CLIENT_ID } from "@/lib/config";
import { ApiError } from "@/lib/api/client";

function tryParseError(body: string): string {
  try {
    const json = JSON.parse(body);
    return json.error || json.message || "";
  } catch {
    return body;
  }
}

/**
 * "Continue with Google" — shared by login and register. Owns the GIS script,
 * the divider, the prompt flow, and its own error/loading state.
 */
export function GoogleButton({ label = "Continue with Google" }: { label?: string }) {
  const { loginWithGoogle } = useAuth();
  const router = useRouter();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleGoogle = () => {
    if (!GOOGLE_CLIENT_ID || typeof window === "undefined") return;
    const google = (window as unknown as { google?: any }).google;
    if (!google?.accounts?.id) return;
    google.accounts.id.initialize({
      client_id: GOOGLE_CLIENT_ID,
      callback: async (response: { credential: string }) => {
        setError("");
        setLoading(true);
        try {
          await loginWithGoogle(response.credential);
          router.push("/");
        } catch (e) {
          setError(e instanceof ApiError ? tryParseError(e.body) || "Google sign-in failed" : "Google sign-in failed");
        } finally {
          setLoading(false);
        }
      },
    });
    google.accounts.id.prompt();
  };

  return (
    <div>
      <div className="my-6 flex items-center gap-3">
        <div className="h-px flex-1 bg-border" />
        <span className="text-xs uppercase tracking-[0.2em] text-fg-subtle">or</span>
        <div className="h-px flex-1 bg-border" />
      </div>
      {error && <p className="mb-3 text-sm text-danger" role="alert">{error}</p>}
      <Button variant="secondary" className="w-full" onClick={handleGoogle} loading={loading} disabled={!GOOGLE_CLIENT_ID}>
        <GoogleGlyph /> {label}
      </Button>
      {GOOGLE_CLIENT_ID && <script src="https://accounts.google.com/gsi/client" async defer />}
    </div>
  );
}

function GoogleGlyph() {
  return (
    <svg viewBox="0 0 24 24" className="mr-2 h-4 w-4" aria-hidden>
      <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.27-4.74 3.27-8.1Z" />
      <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84A11 11 0 0 0 12 23Z" />
      <path fill="#FBBC05" d="M5.84 14.1a6.6 6.6 0 0 1 0-4.2V7.06H2.18a11 11 0 0 0 0 9.88l3.66-2.84Z" />
      <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1A11 11 0 0 0 2.18 7.06l3.66 2.84C6.71 7.3 9.14 5.38 12 5.38Z" />
    </svg>
  );
}
