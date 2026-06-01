"use client";

import { Suspense, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useMutation } from "@tanstack/react-query";
import Link from "next/link";
import { CheckCircle2, XCircle, ShieldAlert } from "lucide-react";
import { accountApi } from "@/lib/api/account";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Spinner, PageLoader } from "@/components/ui/spinner";

export default function ConfirmDeletionPage() {
  return (
    <Suspense fallback={<PageLoader />}>
      <ConfirmDeletionContent />
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

function ConfirmDeletionContent() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token");
  const { user, loading, logout } = useAuth();
  const [status, setStatus] = useState<"idle" | "confirming" | "success" | "error">("idle");

  const confirmMutation = useMutation({
    mutationFn: async (t: string) => accountApi.confirmDeletion(t),
    onSuccess: (res) => setStatus(res.success ? "success" : "error"),
    onError: () => setStatus("error"),
  });

  // Require the user to click — confirming a destructive action on page load would be too easy to
  // trigger by accident (link prefetch, email scanners). Auth is needed since the API uses the
  // current user; redirect to login (preserving the token) if signed out.
  const confirm = () => {
    if (!token) return;
    setStatus("confirming");
    confirmMutation.mutate(token);
  };

  if (loading) return <PageLoader />;

  if (!token) {
    return (
      <AuthCard>
        <XCircle size={44} className="mx-auto text-danger" />
        <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Invalid Link</h1>
        <p className="mt-3 text-fg-muted">This deletion link is missing its token.</p>
        <Link href="/settings" className="mt-6 inline-block font-medium text-accent transition-opacity hover:opacity-80">Back to settings</Link>
      </AuthCard>
    );
  }

  if (!user) {
    return (
      <AuthCard>
        <ShieldAlert size={44} className="mx-auto text-warning" />
        <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Sign in to continue</h1>
        <p className="mt-3 text-fg-muted">Please sign in to confirm your account deletion.</p>
        <Link href={`/login?redirect=${encodeURIComponent(`/settings/confirm-deletion?token=${token}`)}`}
          className="mt-6 inline-block font-medium text-accent transition-opacity hover:opacity-80">
          Sign in
        </Link>
      </AuthCard>
    );
  }

  if (status === "confirming") {
    return (
      <AuthCard>
        <Spinner className="mx-auto h-8 w-8" />
        <p className="mt-4 text-fg-muted">Processing your request...</p>
      </AuthCard>
    );
  }

  if (status === "success") {
    return (
      <AuthCard>
        <CheckCircle2 size={44} className="mx-auto text-success" />
        <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Account Deactivated</h1>
        <p className="mt-3 text-fg-muted">
          Your account has been deactivated and will be permanently deleted in 90 days. Changed your
          mind? Just sign back in any time within 90 days to restore it.
        </p>
        <Button onClick={() => logout()} className="mx-auto mt-6">Sign out</Button>
      </AuthCard>
    );
  }

  if (status === "error") {
    return (
      <AuthCard>
        <XCircle size={44} className="mx-auto text-danger" />
        <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Link Invalid or Expired</h1>
        <p className="mt-3 text-fg-muted">This confirmation link is no longer valid. You can start the deletion process again from settings.</p>
        <Link href="/settings" className="mt-6 inline-block font-medium text-accent transition-opacity hover:opacity-80">Back to settings</Link>
      </AuthCard>
    );
  }

  return (
    <AuthCard>
      <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-danger/10 text-danger">
        <ShieldAlert size={26} />
      </span>
      <h1 className="mt-4 font-display text-3xl font-bold tracking-tight text-fg">Confirm Account Deletion</h1>
      <p className="mt-3 text-fg-muted">
        This deactivates your account, signing as <strong>@{user.username}</strong> out everywhere.
        Your account is permanently deleted after 90 days — sign back in within that window to restore it.
      </p>
      <div className="mt-6 flex justify-center gap-2">
        <Button variant="danger" onClick={confirm}>Confirm deletion</Button>
        <Link href="/settings"><Button variant="secondary">Cancel</Button></Link>
      </div>
    </AuthCard>
  );
}
