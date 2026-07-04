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
  const [notifyOnReply, setNotifyOnReply] = useState(user.notifyOnReply ?? true);
  const [notifyOnMention, setNotifyOnMention] = useState(user.notifyOnMention ?? true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const save = async () => {
    setSaving(true);
    setError("");
    try {
      const res = await accountApi.updateProfile({ notifyOnReply, notifyOnMention });
      if (res.success && res.data) setUser(res.data);
      toast("Notification settings saved.", "success");
    } catch {
      setError("Failed to save settings. Please try again.");
      toast("Failed to save settings. Please try again.", "error");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Notifications">
      <div className="space-y-4">
        {/* Bio, avatar, and cover now live on your profile page — not duplicated here. */}
        <div className="space-y-3 rounded-lg border border-border bg-surface-2/40 p-4">
          <Toggle checked={notifyOnReply} onChange={setNotifyOnReply} label="Notify on replies" description="Email me when someone replies to my threads." />
          <Toggle checked={notifyOnMention} onChange={setNotifyOnMention} label="Notify on mentions" description="Email me when someone @mentions me." />
        </div>
        <Button onClick={save} loading={saving}>Save</Button>
        {error && <p className="text-sm text-danger">{error}</p>}
      </div>
    </SettingsCard>
  );
}
