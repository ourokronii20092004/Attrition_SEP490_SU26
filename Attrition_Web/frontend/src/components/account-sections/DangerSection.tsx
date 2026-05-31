"use client";

import { useState } from "react";
import { accountApi } from "@/lib/api/account";
import { Button } from "@/components/ui/button";
import { SettingsCard } from "./SettingsCard";

export function DangerSection({ logout }: { logout: () => void }) {
  const [confirming, setConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");

  const handleDelete = async () => {
    setDeleting(true);
    setError("");
    try {
      await accountApi.deleteAccount();
      logout();
    } catch {
      setError("Failed to delete account. Please try again.");
      setDeleting(false);
    }
  };

  return (
    <SettingsCard title="Danger Zone" danger>
      {!confirming ? (
        <Button variant="danger" onClick={() => setConfirming(true)}>Delete Account</Button>
      ) : (
        <div className="space-y-3">
          <p className="text-sm text-fg-muted">This action is irreversible. All your data will be permanently removed. Are you sure?</p>
          <div className="flex gap-2">
            <Button variant="danger" onClick={handleDelete} loading={deleting}>Yes, Delete</Button>
            <Button variant="secondary" onClick={() => setConfirming(false)}>Cancel</Button>
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      )}
    </SettingsCard>
  );
}
