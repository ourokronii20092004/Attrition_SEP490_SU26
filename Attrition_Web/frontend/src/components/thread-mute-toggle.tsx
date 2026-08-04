"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Bell, BellOff } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { useToast } from "@/lib/providers";
import { qk } from "@/lib/query-keys";

/**
 * Follow/mute switch for a thread — the forum kind and a wiki article's comment thread alike.
 *
 * Muting is what stops the reply notifications, including the ones you'd otherwise receive for
 * owning the thread or for a reply to your own comment. Renders nothing when signed out, since
 * there are no notifications to silence.
 *
 * The label states the current state ("Following" / "Muted") and the title says what clicking
 * does, so the button never leaves you guessing which way it will flip.
 */
export function ThreadMuteToggle({ threadId, isMuted, disabled, invalidateKey }: {
  threadId: string;
  isMuted: boolean;
  disabled?: boolean;
  /**
   * Query key holding the thread this button reflects. Defaults to the forum thread key; the wiki
   * caches its comment thread under its own key, and invalidating the wrong one would leave the
   * button showing stale state until the next refetch.
   */
  invalidateKey?: readonly unknown[];
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (muted: boolean) => forumApi.setThreadMuted(threadId, muted),
    onSuccess: (_res, muted) => {
      // Refetch the thread so the button reflects what the server stored, not what we assumed.
      queryClient.invalidateQueries({ queryKey: invalidateKey ?? qk.forum.thread(threadId) });
      toast(
        muted
          ? "Muted. You won't get notifications from this thread."
          : "Following. You'll get notifications for new replies.",
        "success",
      );
    },
    onError: () => toast("Couldn't change that. Please try again.", "error"),
  });

  const next = !isMuted;

  return (
    <button
      type="button"
      onClick={() => mutation.mutate(next)}
      disabled={disabled || mutation.isPending}
      aria-pressed={isMuted}
      title={isMuted ? "Turn notifications back on for this thread" : "Stop notifications from this thread"}
      className={`inline-flex shrink-0 items-center gap-1.5 rounded-md border px-2.5 py-1.5 text-xs font-medium transition-colors disabled:opacity-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
        isMuted
          ? "border-border text-fg-subtle hover:border-accent/50 hover:text-fg"
          : "border-accent/40 bg-accent-soft/40 text-accent hover:bg-accent-soft"
      }`}
    >
      {isMuted ? <BellOff size={14} aria-hidden /> : <Bell size={14} aria-hidden />}
      {isMuted ? "Muted" : "Following"}
    </button>
  );
}
