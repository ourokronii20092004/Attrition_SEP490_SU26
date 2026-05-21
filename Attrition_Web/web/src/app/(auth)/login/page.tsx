"use client";

import React, { useState, FormEvent, Suspense, useEffect, useCallback } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { cn } from "@/lib/utils";
import styles from "./auth.module.css";

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID || "";

export default function LoginPage() {
  return (
    <Suspense>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { login, googleLogin, isAuthenticated, isLoading: authLoading } = useAuth();
  const toast = useToast();

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const redirectParam = searchParams.get("redirect") || "/";
  // Ensure redirect starts with exactly one / (prevent //admin → https://admin)
  const redirect = "/" + redirectParam.replace(/^\/+/, "");

  // If already logged in, skip the form and redirect immediately
  useEffect(() => {
    if (!authLoading && isAuthenticated) {
      router.replace(redirect);
    }
  }, [authLoading, isAuthenticated, redirect, router]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");

    if (!username.trim() || !password) {
      setError("Please fill in all fields");
      return;
    }

    setLoading(true);
    try {
      await login(username.trim(), password);
      toast.success("Welcome back!");
      router.push(redirect);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleLogin = useCallback(async (credentialResponse: { credential: string }) => {
    setError("");
    setLoading(true);
    try {
      await googleLogin(credentialResponse.credential, window.location.origin);
      toast.success("Welcome back!");
      router.push(redirect);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Google login failed");
    } finally {
      setLoading(false);
    }
  }, [googleLogin, toast, router, redirect]);

  useEffect(() => {
    if (!GOOGLE_CLIENT_ID) return;

    const script = document.createElement("script");
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.defer = true;
    script.onload = () => {
      /* eslint-disable @typescript-eslint/no-explicit-any */
      (window as any).google?.accounts?.id?.initialize({
        client_id: GOOGLE_CLIENT_ID,
        callback: handleGoogleLogin,
      });
      (window as any).google?.accounts?.id?.renderButton(
        document.getElementById("google-signin-btn"),
        {
          type: "standard",
          theme: "outline",
          size: "large",
          width: "100%",
          text: "signin_with",
          shape: "rectangular",
          logo_alignment: "center",
        }
      );
      /* eslint-enable @typescript-eslint/no-explicit-any */
    };
    document.head.appendChild(script);

    return () => {
      document.head.removeChild(script);
    };
  }, [handleGoogleLogin]);

  return (
    <div className={styles.authPage}>
      <div className={styles.authCard}>
        <div className={styles.authHeader}>
          <Link href="/" className={styles.authLogo}>
            ATTRITION
          </Link>
          <h1 className={styles.authTitle}>Welcome back</h1>
          <p className={styles.authSubtitle}>
            Sign in to your account to continue
          </p>
        </div>

        <form onSubmit={handleSubmit} className={styles.authForm}>
          {error && (
            <div className={styles.authError} role="alert">
              {error}
            </div>
          )}

          <div className="input-group">
            <label htmlFor="username" className="input-label">
              Username
            </label>
            <input
              id="username"
              type="text"
              className="input"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="Enter your username"
              autoComplete="username"
              autoFocus
              disabled={loading}
            />
          </div>

          <div className="input-group">
            <label htmlFor="password" className="input-label">
              Password
            </label>
            <div className={styles.passwordWrapper}>
              <input
                id="password"
                type={showPassword ? "text" : "password"}
                className="input"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter your password"
                autoComplete="current-password"
                disabled={loading}
              />
              <button
                type="button"
                className={styles.passwordToggle}
                onClick={() => setShowPassword(!showPassword)}
                tabIndex={-1}
                aria-label={showPassword ? "Hide password" : "Show password"}
              >
                {showPassword ? "◉" : "◎"}
              </button>
            </div>
          </div>

          <button
            type="submit"
            className={cn("btn btn-primary btn-md", styles.submitBtn, loading && "btn-loading")}
            disabled={loading}
          >
            Sign in
          </button>

          {GOOGLE_CLIENT_ID && (
            <>
              <div className={styles.divider}>or</div>
              <div id="google-signin-btn" className={styles.googleBtnContainer} />
            </>
          )}
        </form>

        <div className={styles.authFooter}>
          <p>
            Don&apos;t have an account?{" "}
            <Link href="/register">Create one</Link>
          </p>
        </div>
      </div>
    </div>
  );
}
