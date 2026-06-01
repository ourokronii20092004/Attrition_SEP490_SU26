"use client";

import { useState } from "react";
import { accountApi } from "@/lib/api/account";
import { Button } from "@/components/ui/button";
import { SettingsCard } from "./SettingsCard";

export function DangerSection({ logout }: { logout: () => void }) {
  const [confirming, setConfirming] = useState(false);
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState("");

  const handleRequest = async () => {
    setSending(true);
    setError("");
    try {
      const res = await accountApi.requestDeletion();
      if (res.success) setSent(true);
      else setError(res.error || "Failed to start account deletion. Please try again.");
    } catch {
      setError("Failed to start account deletion. Please try again.");
    } finally {
      setSending(false);
    }
  };

  return (
    <SettingsCard title="Danger Zone" danger>
      {sent ? (
        <div className="space-y-2">
          <p className="text-sm font-medium text-fg">Check your email</p>
          <p className="text-sm text-fg-muted">
            We sent a confirmation link to your email address. Click it to deactivate your account.
            You'll have <strong>90 days</strong> to change your mind — just sign back in to restore everything.
          </p>
        </div>
      ) : !confirming ? (
        <Button variant="danger" onClick={() => setConfirming(true)}>Delete Account</Button>
      ) : (
        <div className="space-y-3">
          <p className="text-sm text-fg-muted">
            We'll email you a confirmation link. After you confirm, your account is deactivated and
            permanently deleted after <strong>90 days</strong>. You can restore it any time within
            those 90 days by signing back in.
          </p>
          <div className="flex gap-2">
            <Button variant="danger" onClick={handleRequest} loading={sending}>Email me a confirmation link</Button>
            <Button variant="secondary" onClick={() => setConfirming(false)}>Cancel</Button>
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      )}
    </SettingsCard>
  );
}
