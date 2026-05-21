"use client";

import React, { useState, FormEvent, useEffect, useCallback } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { cn } from "@/lib/utils";
import styles from "../login/auth.module.css";

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID || "";

export default function RegisterPage() {
  const router = useRouter();
  const { register, googleLogin } = useAuth();
  const toast = useToast();

  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Field-level validation
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const validateUsername = (value: string) => {
    if (value.length < 3) return "At least 3 characters";
    if (value.length > 20) return "Maximum 20 characters";
    if (!/^[a-zA-Z0-9_]+$/.test(value)) return "Letters, numbers, and underscores only";
    return "";
  };

  const validatePassword = (value: string) => {
    if (value.length < 6) return "At least 6 characters";
    if (!/[A-Z]/.test(value)) return "Include an uppercase letter";
    if (!/[0-9]/.test(value)) return "Include a number";
    return "";
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");

    // Validate all fields
    const errors: Record<string, string> = {};
    const usernameErr = validateUsername(username);
    if (usernameErr) errors.username = usernameErr;
    const passwordErr = validatePassword(password);
    if (passwordErr) errors.password = passwordErr;
    if (password !== confirmPassword) errors.confirmPassword = "Passwords don't match";
    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = "Invalid email format";

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }

    setFieldErrors({});
    setLoading(true);
    try {
      await register(username.trim(), password, email.trim() || undefined);
      toast.success("Account created! Welcome to Attrition.");
      router.push("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Registration failed");
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleRegister = useCallback(async (credentialResponse: { credential: string }) => {
    setError("");
    setLoading(true);
    try {
      await googleLogin(credentialResponse.credential, window.location.origin);
      toast.success("Account created! Welcome to Attrition.");
      router.push("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Google sign-up failed");
    } finally {
      setLoading(false);
    }
  }, [googleLogin, toast, router]);

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
        callback: handleGoogleRegister,
      });
      (window as any).google?.accounts?.id?.renderButton(
        document.getElementById("google-signup-btn"),
        {
          type: "standard",
          theme: "outline",
          size: "large",
          width: "100%",
          text: "signup_with",
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
  }, [handleGoogleRegister]);

  return (
    <div className={styles.authPage}>
      <div className={styles.authCard}>
        <div className={styles.authHeader}>
          <Link href="/" className={styles.authLogo}>
            ATTRITION
          </Link>
          <h1 className={styles.authTitle}>Create account</h1>
          <p className={styles.authSubtitle}>
            Join the community and start your journey
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
              Username <span style={{ color: "var(--danger)" }}>*</span>
            </label>
            <input
              id="username"
              type="text"
              className={cn("input", fieldErrors.username && "input-error")}
              value={username}
              onChange={(e) => {
                setUsername(e.target.value);
                if (fieldErrors.username) {
                  setFieldErrors((prev) => ({ ...prev, username: "" }));
                }
              }}
              onBlur={() => {
                const err = validateUsername(username);
                if (err) setFieldErrors((prev) => ({ ...prev, username: err }));
              }}
              placeholder="Choose a username"
              autoComplete="username"
              autoFocus
              disabled={loading}
            />
            {fieldErrors.username && (
              <span className="input-error-text">{fieldErrors.username}</span>
            )}
          </div>

          <div className="input-group">
            <label htmlFor="email" className="input-label">
              Email <span style={{ color: "var(--text-muted)" }}>(optional)</span>
            </label>
            <input
              id="email"
              type="email"
              className={cn("input", fieldErrors.email && "input-error")}
              value={email}
              onChange={(e) => {
                setEmail(e.target.value);
                if (fieldErrors.email) {
                  setFieldErrors((prev) => ({ ...prev, email: "" }));
                }
              }}
              placeholder="your@email.com"
              autoComplete="email"
              disabled={loading}
            />
            {fieldErrors.email && (
              <span className="input-error-text">{fieldErrors.email}</span>
            )}
          </div>

          <div className="input-group">
            <label htmlFor="password" className="input-label">
              Password <span style={{ color: "var(--danger)" }}>*</span>
            </label>
            <div className={styles.passwordWrapper}>
              <input
                id="password"
                type={showPassword ? "text" : "password"}
                className={cn("input", fieldErrors.password && "input-error")}
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value);
                  if (fieldErrors.password) {
                    setFieldErrors((prev) => ({ ...prev, password: "" }));
                  }
                }}
                onBlur={() => {
                  const err = validatePassword(password);
                  if (err) setFieldErrors((prev) => ({ ...prev, password: err }));
                }}
                placeholder="Create a password"
                autoComplete="new-password"
                disabled={loading}
              />
              <button
                type="button"
                className={styles.passwordToggle}
                onClick={() => setShowPassword(!showPassword)}
                tabIndex={-1}
              >
                {showPassword ? "◉" : "◎"}
              </button>
            </div>
            {fieldErrors.password && (
              <span className="input-error-text">{fieldErrors.password}</span>
            )}
            {password && !fieldErrors.password && (
              <PasswordStrength password={password} />
            )}
          </div>

          <div className="input-group">
            <label htmlFor="confirmPassword" className="input-label">
              Confirm Password <span style={{ color: "var(--danger)" }}>*</span>
            </label>
            <input
              id="confirmPassword"
              type={showPassword ? "text" : "password"}
              className={cn("input", fieldErrors.confirmPassword && "input-error")}
              value={confirmPassword}
              onChange={(e) => {
                setConfirmPassword(e.target.value);
                if (fieldErrors.confirmPassword) {
                  setFieldErrors((prev) => ({ ...prev, confirmPassword: "" }));
                }
              }}
              onBlur={() => {
                if (confirmPassword && confirmPassword !== password) {
                  setFieldErrors((prev) => ({ ...prev, confirmPassword: "Passwords don't match" }));
                }
              }}
              placeholder="Confirm your password"
              autoComplete="new-password"
              disabled={loading}
            />
            {fieldErrors.confirmPassword && (
              <span className="input-error-text">{fieldErrors.confirmPassword}</span>
            )}
          </div>

          <button
            type="submit"
            className={cn("btn btn-primary btn-md", styles.submitBtn, loading && "btn-loading")}
            disabled={loading}
          >
            Create account
          </button>

          {GOOGLE_CLIENT_ID && (
            <>
              <div className={styles.divider}>or</div>
              <div id="google-signup-btn" className={styles.googleBtnContainer} />
            </>
          )}
        </form>

        <div className={styles.authFooter}>
          <p>
            Already have an account?{" "}
            <Link href="/login">Sign in</Link>
          </p>
        </div>
      </div>
    </div>
  );
}

// ─── Password Strength Indicator ───────────────────────────

function PasswordStrength({ password }: { password: string }) {
  const getStrength = (pw: string): { level: number; label: string; color: string } => {
    let score = 0;
    if (pw.length >= 6) score++;
    if (pw.length >= 10) score++;
    if (/[A-Z]/.test(pw)) score++;
    if (/[0-9]/.test(pw)) score++;
    if (/[^a-zA-Z0-9]/.test(pw)) score++;

    if (score <= 1) return { level: 1, label: "Weak", color: "var(--danger)" };
    if (score <= 2) return { level: 2, label: "Fair", color: "var(--warning)" };
    if (score <= 3) return { level: 3, label: "Good", color: "var(--info)" };
    return { level: 4, label: "Strong", color: "var(--success)" };
  };

  const { level, label, color } = getStrength(password);

  return (
    <div className={styles.strengthWrapper}>
      <div className={styles.strengthBar}>
        {[1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className={styles.strengthSegment}
            style={{
              background: i <= level ? color : "var(--border)",
            }}
          />
        ))}
      </div>
      <span className={styles.strengthLabel} style={{ color }}>
        {label}
      </span>
    </div>
  );
}
