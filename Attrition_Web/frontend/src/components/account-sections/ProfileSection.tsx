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
 * Notification preferences. Saves the moment a switch is flipped — a Save button here was easy to
 * miss, and people left thinking a change had stuck when it hadn't.
 */
export function ProfileSection({ user, setUser }: { user: UserDto; setUser: (u: UserDto) => void }) {
  const { toast } = useToast();
  const [notifyOnReply, setNotifyOnReply] = useState(user.notifyOnReply ?? true);
  const [notifyOnMention, setNotifyOnMention] = useState(user.notifyOnMention ?? true);

  const { save, state } = useAutoSave<{ notifyOnReply?: boolean; notifyOnMention?: boolean }>({
    onSave: async (patch) => {
      const res = await accountApi.updateProfile(patch);
      if (res.success && res.data) setUser(res.data);
      else throw new Error(res.error ?? "save failed");
    },
    onError: (message) => toast(message, "error"),
  });

  return (
    <SettingsCard title="Notifications">
      <div className="space-y-4">
        {/* Bio, avatar, and cover live on your profile page — not duplicated here. */}
        <div className="space-y-3 rounded-lg border border-border bg-surface-2/40 p-4">
          <Toggle
            checked={notifyOnReply}
            onChange={(next) => {
              setNotifyOnReply(next);
              save({ notifyOnReply: next }, () => setNotifyOnReply(!next));
            }}
            label="Notify on replies"
            description="Email me when someone replies to my threads."
          />
          <Toggle
            checked={notifyOnMention}
            onChange={(next) => {
              setNotifyOnMention(next);
              save({ notifyOnMention: next }, () => setNotifyOnMention(!next));
            }}
            label="Notify on mentions"
            description="Email me when someone @mentions me."
          />
        </div>
        <SaveStatus state={state} />
      </div>
    </SettingsCard>
  );
}
