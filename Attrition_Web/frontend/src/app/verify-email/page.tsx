"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useMutation } from "@tanstack/react-query";
import Link from "next/link";
import { CheckCircle2, XCircle, Mail } from "lucide-react";
import { authApi } from "@/lib/api/auth";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Spinner, PageLoader } from "@/components/ui/spinner";

export default function VerifyEmailPage() {
  return (
    <Suspense fallback={<PageLoader />}>
      <VerifyEmailContent />
    </Suspense>
  );
}

function AuthCard({ children }: { children: React.ReactNode }) {
  return (
    <div className="mx-auto flex min-h-[80vh] max-w-md flex-col justify-center px-4 py-12">
      <div className="glass rounded-2xl p-6 text-center shadow-[var(--shadow-lg)] sm:p-8 motion-safe:animate-rise-in">
        {children}
      </div>
    </div>
  );
}

function VerifyEmailContent() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token");
  const { user } = useAuth();
  const [status, setStatus] = useState<"verifying" | "success" | "error" | "idle">(token ? "verifying" : "idle");

  const verifyMutation = useMutation({
    mutationFn: async (t: string) => authApi.verifyEmail({ token: t }),
    onSuccess: () => setStatus("success"),
    onError: () => setStatus("error"),
  });

  const resendMutation = useMutation({
    mutationFn: async () => { await authApi.resendVerification(); },
  });

  useEffect(() => {
    if (!token) return;
    verifyMutation.mutate(token);
    // Fire the verify exactly once for this token.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  const handleResend = () => {
    resendMutation.mutate();
  };
  const resending = resendMutation.isPending;

  if (status === "verifying") {
    return (
      <AuthCard>
        <Spinner className="mx-auto h-8 w-8" />
        <p className="mt-4 text-fg-muted">Verifying your email...</p>
      </AuthCard>
    );
  }

  if (status === "success") {
    return (
      <AuthCard>
        <CheckCircle2 size={44} className="mx-auto text-success" />
        <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Email Verified</h1>
        <p className="mt-3 text-fg-muted">Your email has been verified successfully.</p>
        <Link href="/" className="mt-6 inline-block font-medium text-accent transition-opacity hover:opacity-80">Go to home</Link>
      </AuthCard>
    );
  }

  if (status === "error") {
    return (
      <AuthCard>
        <XCircle size={44} className="mx-auto text-danger" />
        <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Verification Failed</h1>
        <p className="mt-3 text-fg-muted">The link may have expired or is invalid.</p>
        {user && (
          <Button onClick={handleResend} loading={resending} className="mx-auto mt-6">
            Resend Verification Email
          </Button>
        )}
      </AuthCard>
    );
  }

  return (
    <AuthCard>
      <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-accent-soft text-accent">
        <Mail size={26} />
      </span>
      <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Verify Your Email</h1>
      <p className="mt-3 text-fg-muted">Check your inbox for a verification link.</p>
      {user && (
        <Button onClick={handleResend} loading={resending} className="mx-auto mt-6">
          Resend Verification Email
        </Button>
      )}
    </AuthCard>
  );
}
