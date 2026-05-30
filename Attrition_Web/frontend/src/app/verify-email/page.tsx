"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
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

function VerifyEmailContent() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token");
  const { user } = useAuth();
  const [status, setStatus] = useState<"verifying" | "success" | "error" | "idle">(token ? "verifying" : "idle");
  const [resending, setResending] = useState(false);

  useEffect(() => {
    if (!token) return;
    authApi
      .verifyEmail({ token })
      .then(() => setStatus("success"))
      .catch(() => setStatus("error"));
  }, [token]);

  const handleResend = async () => {
    setResending(true);
    try {
      await authApi.resendVerification();
    } catch {}
    setResending(false);
  };

  if (status === "verifying") {
    return (
      <div className="mx-auto flex min-h-[70vh] max-w-md flex-col items-center justify-center px-4 py-12">
        <Spinner className="h-8 w-8" />
        <p className="mt-4 text-fg-muted">Verifying your email...</p>
      </div>
    );
  }

  if (status === "success") {
    return (
      <div className="mx-auto flex min-h-[70vh] max-w-md flex-col justify-center px-4 py-12 text-center">
        <h1 className="font-display text-3xl font-bold text-fg">Email Verified</h1>
        <p className="mt-4 text-fg-muted">Your email has been verified successfully.</p>
        <Link href="/" className="mt-6 text-accent hover:underline">Go to home</Link>
      </div>
    );
  }

  if (status === "error") {
    return (
      <div className="mx-auto flex min-h-[70vh] max-w-md flex-col justify-center px-4 py-12 text-center">
        <h1 className="font-display text-3xl font-bold text-fg">Verification Failed</h1>
        <p className="mt-4 text-fg-muted">The link may have expired or is invalid.</p>
        {user && (
          <Button onClick={handleResend} loading={resending} className="mx-auto mt-6">
            Resend Verification Email
          </Button>
        )}
      </div>
    );
  }

  return (
    <div className="mx-auto flex min-h-[70vh] max-w-md flex-col justify-center px-4 py-12 text-center">
      <h1 className="font-display text-3xl font-bold text-fg">Verify Your Email</h1>
      <p className="mt-4 text-fg-muted">
        Check your inbox for a verification link.
      </p>
      {user && (
        <Button onClick={handleResend} loading={resending} className="mx-auto mt-6">
          Resend Verification Email
        </Button>
      )}
    </div>
  );
}
