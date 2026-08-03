"use client";

import { useState } from "react";
import { accountApi } from "@/lib/api/account";
import { useToast } from "@/lib/providers";
import { Toggle } from "@/components/ui/toggle";
import { useAutoSave } from "@/lib/hooks/use-auto-save";
import type { UserDto } from "@/lib/types";
import { SettingsCard } from "./SettingsCard";
import { SaveStatus } from "./SaveStatus";

/**
 * Profile visibility controls. Saves as soon as a switch is flipped.
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

  const { save, state } = useAutoSave<{ showBio?: boolean; showActivity?: boolean }>({
    onSave: async (patch) => {
      const res = await accountApi.updateProfile(patch);
      if (res.success && res.data) setUser(res.data);
      else throw new Error(res.error ?? "save failed");
    },
    onError: (message) => toast(message, "error"),
  });

  return (
    <SettingsCard title="Privacy">
      <div className="space-y-4">
        <div className="space-y-3 rounded-lg border border-border bg-surface-2/40 p-4">
          <Toggle
            checked={showBio}
            onChange={(next) => {
              setShowBio(next);
              save({ showBio: next }, () => setShowBio(!next));
            }}
            label="Show my profile"
            description="When off, visitors are told you've hidden your profile. You and admins can still see it."
          />
          <Toggle
            checked={showActivity}
            onChange={(next) => {
              setShowActivity(next);
              save({ showActivity: next }, () => setShowActivity(!next));
            }}
            label="Show my activity"
            description="Your threads, replies, and wiki contributions on your profile page. They stay visible in the forum and wiki themselves."
          />
        </div>
        {!showBio && (
          <p className="rounded-lg border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-fg-muted">
            Your profile is hidden. Posts and replies you have already made stay visible in the
            forum and wiki — this only hides your profile page.
          </p>
        )}
        <SaveStatus state={state} />
      </div>
    </SettingsCard>
  );
}
