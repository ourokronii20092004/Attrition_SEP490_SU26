"use client";

import { useState } from "react";
import { authApi } from "@/lib/api/auth";
import { accountApi } from "@/lib/api/account";
import { parseApiError } from "@/lib/api/parse-error";
import { useAuth, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SettingsCard } from "./SettingsCard";

export function PasswordSection() {
  const { user, refreshUser } = useAuth();
  const { toast } = useToast();
  const [current, setCurrent] = useState("");
  const [newPw, setNewPw] = useState("");
  const [confirm, setConfirm] = useState("");
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState("");

  // Google-only accounts have no password yet: offer "set" (no current password) instead of "change".
  const hasPassword = user?.hasPassword ?? true;

  const change = async () => {
    if (newPw !== confirm) { setMsg("The new passwords don't match."); toast("The new passwords don't match.", "error"); return; }
    setSaving(true);
    setMsg("");
    try {
      await authApi.changePassword({ currentPassword: current, newPassword: newPw });
      setMsg("Password updated");
      setCurrent("");
      setNewPw("");
      setConfirm("");
      toast("Password updated. A confirmation email has been sent.", "success");
    } catch (err) {
      const m = parseApiError(err, "Failed to update password");
      setMsg(m);
      toast(m, "error");
    }
    setSaving(false);
  };

  const set = async () => {
    if (newPw !== confirm) { setMsg("The passwords don't match."); return; }
    setSaving(true);
    setMsg("");
    try {
      await accountApi.setPassword({ newPassword: newPw });
      setNewPw("");
      setConfirm("");
      toast("Password set.", "success");
      await refreshUser(); // hasPassword flips true → this section switches to change mode
    } catch (err) {
      const m = parseApiError(err, "Failed to set password");
      setMsg(m);
      toast(m, "error");
    }
    setSaving(false);
  };

  if (!hasPassword) {
    return (
      <SettingsCard title="Password">
        <p className="mb-3 text-sm text-fg-muted">
          You signed in with Google and don&apos;t have a password yet. Set one to also sign in with your username.
        </p>
        <div className="space-y-3">
          <Input label="New Password" type="password" autoComplete="new-password" value={newPw} onChange={(e) => setNewPw(e.target.value)} />
          <Input label="Confirm Password" type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
          {msg && <p className="text-sm text-fg-muted">{msg}</p>}
          <Button onClick={set} loading={saving} disabled={!newPw || !confirm}>Set Password</Button>
        </div>
      </SettingsCard>
    );
  }

  return (
    <SettingsCard title="Password">
      <div className="space-y-3">
        <Input label="Current Password" type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} />
        <Input label="New Password" type="password" autoComplete="new-password" value={newPw} onChange={(e) => setNewPw(e.target.value)} />
        <Input label="Repeat New Password" type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
        {msg && <p className="text-sm text-fg-muted">{msg}</p>}
        <p className="text-xs text-fg-subtle">For your security, we&apos;ll email a confirmation to your address after the change.</p>
        <Button onClick={change} loading={saving} disabled={!current || !newPw || !confirm}>Change Password</Button>
      </div>
    </SettingsCard>
  );
}
