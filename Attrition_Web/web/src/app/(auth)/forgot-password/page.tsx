"use client";

import React, { useState, FormEvent, Suspense } from "react";
import Link from "next/link";
import { useToast } from "@/contexts/ToastContext";
import { cn } from "@/lib/utils";
import { api } from "@/lib/api";
import styles from "../login/auth.module.css";

export default function ForgotPasswordPage() {
  return (
    <Suspense>
      <ForgotPasswordForm />
    </Suspense>
  );
}

function ForgotPasswordForm() {
  const toast = useToast();

  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");

    if (!email.trim()) {
      setError("Please enter your email address");
      return;
    }

    setLoading(true);
    try {
      await api.post("/auth/forgot-password", { email: email.trim() });
      toast.success("Reset link sent! Check your email.");
      setSubmitted(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to send reset link");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={styles.authPage}>
      <div className={styles.authCard}>
        <div className={styles.authHeader}>
          <Link href="/" className={styles.authLogo}>
            ATTRITION
          </Link>
          <h1 className={styles.authTitle}>Forgot Password</h1>
          <p className={styles.authSubtitle}>
            Enter your email to receive a reset link
          </p>
        </div>

        {submitted ? (
          <div className={styles.authForm}>
            <div style={{ textAlign: "center", padding: "var(--space-4) 0" }}>
              <p style={{ fontSize: "var(--text-sm)", color: "var(--text-tertiary)" }}>
                Check your email for reset instructions
              </p>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className={styles.authForm}>
            {error && (
              <div className={styles.authError} role="alert">
                {error}
              </div>
            )}

            <div className="input-group">
              <label htmlFor="email" className="input-label">
                Email
              </label>
              <input
                id="email"
                type="email"
                className="input"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Enter your email address"
                autoComplete="email"
                autoFocus
                disabled={loading}
              />
            </div>

            <button
              type="submit"
              className={cn("btn btn-primary btn-md", styles.submitBtn, loading && "btn-loading")}
              disabled={loading}
            >
              Send Reset Link
            </button>
          </form>
        )}

        <div className={styles.authFooter}>
          <p>
            <Link href="/login">Back to Sign In</Link>
          </p>
        </div>
      </div>
    </div>
  );
}
