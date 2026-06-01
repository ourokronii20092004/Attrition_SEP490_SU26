"use client";

import { useState } from "react";
import { authApi } from "@/lib/api/auth";
import { useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SettingsCard } from "./SettingsCard";

export function PasswordSection() {
  const { toast } = useToast();
  const [current, setCurrent] = useState("");
  const [newPw, setNewPw] = useState("");
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState("");

  const save = async () => {
    setSaving(true);
    setMsg("");
    try {
      await authApi.changePassword({ currentPassword: current, newPassword: newPw });
      setMsg("Password updated");
      setCurrent("");
      setNewPw("");
      toast("Password updated.", "success");
    } catch {
      setMsg("Failed to update password");
      toast("Failed to update password.", "error");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Password">
      <div className="space-y-3">
        <Input label="Current Password" type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} />
        <Input label="New Password" type="password" autoComplete="new-password" value={newPw} onChange={(e) => setNewPw(e.target.value)} />
        {msg && <p className="text-sm text-fg-muted">{msg}</p>}
        <Button onClick={save} loading={saving} disabled={!current || !newPw}>Change Password</Button>
      </div>
    </SettingsCard>
  );
}
