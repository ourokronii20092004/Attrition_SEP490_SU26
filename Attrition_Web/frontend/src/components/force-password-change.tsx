"use client";

import { useState } from "react";
import { KeyRound } from "lucide-react";
import { authApi } from "@/lib/api/auth";
import { parseApiError } from "@/lib/api/parse-error";
import { useAuth, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

/**
 * Blocking gate for accounts an admin has reset (mustChangePassword). It renders a full-screen
 * overlay over every route until the user sets a new password — the change clears the flag
 * server-side, refreshUser() then drops the overlay. Mounted once at the app root so it covers
 * both the public site and the admin dashboard.
 */
export function ForcePasswordChange() {
  const { user, refreshUser, logout } = useAuth();
  const { toast } = useToast();
  const [current, setCurrent] = useState("");
  const [newPw, setNewPw] = useState("");
  const [confirm, setConfirm] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  if (!user?.mustChangePassword) return null;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    if (newPw !== confirm) {
      setError("The new passwords don't match.");
      return;
    }
    setSaving(true);
    try {
      await authApi.changePassword({ currentPassword: current, newPassword: newPw });
      toast("Password updated.", "success");
      await refreshUser(); // clears mustChangePassword -> this overlay unmounts
    } catch (err) {
      setError(parseApiError(err, "Couldn't update your password. Check your current password and try again."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center bg-bg/90 p-4 backdrop-blur">
      <div className="w-full max-w-md rounded-card border border-border bg-surface p-6 shadow-[var(--shadow-lg)]">
        <div className="mb-3 flex items-center gap-2 text-accent">
          <KeyRound size={20} />
          <h1 className="font-display text-xl font-bold text-fg">Set a new password</h1>
        </div>
        <p className="mb-5 text-sm text-fg-muted">
          An administrator reset your password. For your security, choose a new one before continuing.
        </p>
        <form onSubmit={submit} className="space-y-3">
          <Input label="Current (temporary) password" type="password" autoComplete="current-password"
            value={current} onChange={(e) => setCurrent(e.target.value)} />
          <Input label="New password" type="password" autoComplete="new-password"
            value={newPw} onChange={(e) => setNewPw(e.target.value)} />
          <Input label="Confirm new password" type="password" autoComplete="new-password"
            value={confirm} onChange={(e) => setConfirm(e.target.value)} />
          {error && <p className="text-sm text-danger">{error}</p>}
          <div className="flex items-center justify-between gap-2 pt-1">
            <button type="button" onClick={logout} className="text-sm text-fg-muted transition-colors hover:text-fg">
              Sign out
            </button>
            <Button type="submit" loading={saving} disabled={!current || !newPw || !confirm}>Update password</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
