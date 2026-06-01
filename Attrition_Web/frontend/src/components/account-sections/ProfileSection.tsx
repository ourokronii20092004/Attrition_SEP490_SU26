"use client";

import { useState } from "react";
import { accountApi } from "@/lib/api/account";
import { useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Toggle } from "@/components/ui/toggle";
import type { UserDto } from "@/lib/types";
import { SettingsCard } from "./SettingsCard";

export function ProfileSection({ user, setUser }: { user: UserDto; setUser: (u: UserDto) => void }) {
  const { toast } = useToast();
  const [bio, setBio] = useState(user.bio ?? "");
  const [notifyOnReply, setNotifyOnReply] = useState(user.notifyOnReply ?? true);
  const [notifyOnMention, setNotifyOnMention] = useState(user.notifyOnMention ?? true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const save = async () => {
    setSaving(true);
    setError("");
    try {
      const res = await accountApi.updateProfile({ bio, notifyOnReply, notifyOnMention });
      if (res.success && res.data) setUser(res.data);
      toast("Profile saved.", "success");
    } catch {
      setError("Failed to save profile. Please try again.");
      toast("Failed to save profile. Please try again.", "error");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Profile">
      <div className="space-y-4">
        <div className="space-y-1.5">
          <label className="block text-sm font-medium text-fg-muted">Bio</label>
          <textarea
            value={bio}
            onChange={(e) => setBio(e.target.value)}
            rows={3}
            className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors focus:border-accent focus:ring-1 focus:ring-accent"
          />
        </div>
        <div className="space-y-3 rounded-lg border border-border bg-surface-2/40 p-4">
          <Toggle checked={notifyOnReply} onChange={setNotifyOnReply} label="Notify on replies" description="Email me when someone replies to my threads." />
          <Toggle checked={notifyOnMention} onChange={setNotifyOnMention} label="Notify on mentions" description="Email me when someone @mentions me." />
        </div>
        <Button onClick={save} loading={saving}>Save Profile</Button>
        {error && <p className="text-sm text-danger">{error}</p>}
      </div>
    </SettingsCard>
  );
}
