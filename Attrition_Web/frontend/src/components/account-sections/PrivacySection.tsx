"use client";

import { useState } from "react";
import { accountApi } from "@/lib/api/account";
import { useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Toggle } from "@/components/ui/toggle";
import type { UserDto } from "@/lib/types";
import { SettingsCard } from "./SettingsCard";

/**
 * Profile visibility controls.
 *
 * "Show profile" is the stronger of the two: with it off, the profile page is withheld from
 * everyone but the owner and admins, and visitors are told the user has hidden their profile.
 * "Show activity" only withholds the threads/replies/contributions feed, leaving the rest of
 * the profile intact.
 */
export function PrivacySection({ user, setUser }: { user: UserDto; setUser: (u: UserDto) => void }) {
  const { toast } = useToast();
  const [showBio, setShowBio] = useState(user.showBio ?? true);
  const [showActivity, setShowActivity] = useState(user.showActivity ?? true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const save = async () => {
    setSaving(true);
    setError("");
    try {
      const res = await accountApi.updateProfile({ showBio, showActivity });
      if (res.success && res.data) setUser(res.data);
      toast("Privacy settings saved.", "success");
    } catch {
      setError("Failed to save settings. Please try again.");
      toast("Failed to save settings. Please try again.", "error");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Privacy">
      <div className="space-y-4">
        <div className="space-y-3 rounded-lg border border-border bg-surface-2/40 p-4">
          <Toggle checked={showBio} onChange={setShowBio} label="Show my profile"
            description="When off, visitors are told you've hidden your profile. You and admins can still see it." />
          <Toggle checked={showActivity} onChange={setShowActivity} label="Show my activity"
            description="Your threads, replies, and wiki contributions on your profile page. They stay visible in the forum and wiki themselves." />
        </div>
        {!showBio && (
          <p className="rounded-lg border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-fg-muted">
            Your profile is hidden. Posts and replies you have already made stay visible in the
            forum and wiki — this only hides your profile page.
          </p>
        )}
        <Button onClick={save} loading={saving}>Save</Button>
        {error && <p className="text-sm text-danger">{error}</p>}
      </div>
    </SettingsCard>
  );
}
