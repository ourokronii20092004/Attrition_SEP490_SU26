"use client";

import { useState } from "react";
import { Mail, X } from "lucide-react";
import { useAuth } from "@/lib/providers";
import { authApi } from "@/lib/api/auth";

/**
 * Site-wide nudge for logged-in users who haven't verified their email. They can still
 * browse, but posting/contributing is blocked server-side until verified (soft gate).
 * Hidden for admins, verified users, and users with no email on file.
 */
export function VerifyEmailBanner() {
  const { user } = useAuth();
  const [dismissed, setDismissed] = useState(false);
  const [resending, setResending] = useState(false);
  const [sent, setSent] = useState(false);

  if (!user || user.isEmailVerified || user.role === "Admin" || !user.email || dismissed) return null;

  const resend = async () => {
    setResending(true);
    try { await authApi.resendVerification(); setSent(true); } catch {} finally { setResending(false); }
  };

  return (
    <div className="border-b border-warning/30 bg-warning/10">
      <div className="mx-auto flex max-w-7xl items-center gap-3 px-5 py-2.5 text-sm sm:px-8">
        <Mail size={16} className="shrink-0 text-warning" />
        <p className="min-w-0 flex-1 text-fg">
          {sent
            ? "Verification email sent — check your inbox."
            : "Verify your email to post and contribute. Browsing stays open."}
        </p>
        {!sent && (
          <button
            onClick={resend}
            disabled={resending}
            className="shrink-0 rounded-md bg-warning/20 px-3 py-1 text-xs font-medium text-warning transition-colors hover:bg-warning/30 disabled:opacity-50"
          >
            {resending ? "Sending…" : "Resend"}
          </button>
        )}
        <button onClick={() => setDismissed(true)} className="shrink-0 text-fg-subtle transition-colors hover:text-fg" aria-label="Dismiss">
          <X size={16} />
        </button>
      </div>
    </div>
  );
}
