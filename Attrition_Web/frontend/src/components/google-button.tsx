"use client";

import { useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/components/ui/button";
import { LoadingScreen } from "@/components/ui/loading-screen";
import { API_BASE, GOOGLE_CLIENT_ID } from "@/lib/config";

const UNITY_CLIENT_KEY = "attrition_unity_client";

function googleErrorMessage(code: string): string {
  switch (code) {
    case "google_denied":
      return "Google sign-in was cancelled.";
    case "google_unconfigured":
      return "Google sign-in isn't available right now.";
    case "google_unavailable":
      return "Google sign-in is temporarily unavailable. Please try again shortly.";
    case "google_state":
      return "Your Google sign-in session expired. Please try again.";
    case "google_failed":
      return "We couldn't complete Google sign-in. Please try again.";
    default:
      return "Google sign-in failed. Please try again.";
  }
}

/**
 * "Continue with Google" — shared by login and register. Uses a full-page redirect into the
 * server-side OAuth code flow (GET /api/auth/google/start) instead of a popup / One-Tap prompt, so
 * browsers that block third-party sign-in popups (notably Edge) can't break login. The server does
 * the Google round-trip, sets the session cookies, then redirects back here (or to the game host).
 */
export function GoogleButton({ label = "Continue with Google" }: { label?: string }) {
  const searchParams = useSearchParams();
  const [error, setError] = useState("");
  const [retryIn, setRetryIn] = useState(0); // rate-limit countdown (seconds)
  const [redirecting, setRedirecting] = useState(false);

  // Game mở web với ?client=unity. Lưu cờ này NGAY khi trang load để giữ ý định "về game" kể cả khi
  // query bị rớt trước lúc người dùng bấm nút.
  useEffect(() => {
    if (typeof window === "undefined") return;
    if (new URLSearchParams(window.location.search).get("client") === "unity") {
      sessionStorage.setItem(UNITY_CLIENT_KEY, "1");
    }
  }, []);

  // Surface a failure bounced back from the server-side OAuth callback (?auth_error=...).
  // Rate limits get a live countdown instead of a static message.
  useEffect(() => {
    const code = searchParams.get("auth_error");
    if (!code) return;
    if (code === "rate_limited") {
      const secs = parseInt(searchParams.get("retry") ?? "", 10);
      setRetryIn(Number.isFinite(secs) && secs > 0 ? secs : 60);
    } else {
      setError(googleErrorMessage(code));
    }
  }, [searchParams]);

  // Tick the rate-limit countdown down to zero.
  useEffect(() => {
    if (retryIn <= 0) return;
    const id = setInterval(() => setRetryIn((s) => Math.max(0, s - 1)), 1000);
    return () => clearInterval(id);
  }, [retryIn]);

  // If the user leaves for Google and hits the browser Back button, the page is often restored from
  // the back-forward cache with React state frozen — leaving the redirect overlay stuck forever.
  // `pageshow` fires on that restore (persisted=true) and on normal loads; clear the overlay so the
  // button works again.
  useEffect(() => {
    const onPageShow = () => setRedirecting(false);
    window.addEventListener("pageshow", onPageShow);
    return () => window.removeEventListener("pageshow", onPageShow);
  }, []);

  const isUnityClient = () => {
    if (searchParams.get("client") === "unity") return true;
    if (typeof window === "undefined") return false;
    if (new URLSearchParams(window.location.search).get("client") === "unity") return true;
    return sessionStorage.getItem(UNITY_CLIENT_KEY) === "1";
  };

  const handleGoogle = () => {
    if (!GOOGLE_CLIENT_ID || typeof window === "undefined" || retryIn > 0) return;
    setError("");
    // Network parity with the email/password forms: if we're offline the redirect would just fail
    // (and leave the overlay stuck), so bail out early with a clear message instead.
    if (typeof navigator !== "undefined" && navigator.onLine === false) {
      setError("You appear to be offline. Check your connection and try again.");
      return;
    }
    // Show the loader BEFORE leaving for Google — the account chooser is Google's own page, so this
    // is the only moment we can give in-app feedback. The brief delay lets the overlay paint first.
    setRedirecting(true);
    const url = `${API_BASE}/api/auth/google/start${isUnityClient() ? "?client=unity" : ""}`;
    window.setTimeout(() => { window.location.href = url; }, 150);
  };

  return (
    <div>
      {redirecting && <LoadingScreen fullscreen />}
      <div className="my-6 flex items-center gap-3">
        <div className="h-px flex-1 bg-border" />
        <span className="text-xs uppercase tracking-[0.2em] text-fg-subtle">or</span>
        <div className="h-px flex-1 bg-border" />
      </div>
      {error && <p className="mb-3 text-sm text-danger" role="alert">{error}</p>}
      {retryIn > 0 && (
        <p className="mb-3 text-sm text-warning" role="alert">
          Too many attempts. Try again in {retryIn}s.
        </p>
      )}
      <Button
        variant="secondary"
        className="w-full"
        onClick={handleGoogle}
        loading={redirecting}
        disabled={!GOOGLE_CLIENT_ID || retryIn > 0}
      >
        <GoogleGlyph /> {label}
      </Button>
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
