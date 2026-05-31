"use client";

import { useState } from "react";
import { accountApi } from "@/lib/api/account";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { UserDto } from "@/lib/types";
import { SettingsCard } from "./SettingsCard";

export function EmailSection({ user, refreshUser }: { user: UserDto; refreshUser: () => Promise<void> }) {
  const [email, setEmail] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState("");

  const save = async () => {
    setSaving(true);
    setMsg("");
    try {
      await accountApi.updateEmail({ newEmail: email, currentPassword });
      setMsg("Verification email sent to new address");
      await refreshUser();
    } catch {
      setMsg("Failed to update email");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Email">
      <p className="-mt-2 text-sm text-fg-muted">
        Current: {user.email ?? "none"}{" "}
        {user.email && (user.isEmailVerified
          ? <span className="text-success">(verified)</span>
          : <span className="text-warning">(unverified)</span>)}
      </p>
      <div className="mt-4 space-y-3">
        <Input label="New Email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        <Input label="Current Password" type="password" autoComplete="current-password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} />
        {msg && <p className="text-sm text-fg-muted">{msg}</p>}
        <Button onClick={save} loading={saving} disabled={!email || !currentPassword}>Update Email</Button>
      </div>
    </SettingsCard>
  );
}
