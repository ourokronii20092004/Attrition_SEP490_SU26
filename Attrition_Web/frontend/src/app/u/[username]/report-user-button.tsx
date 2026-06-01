"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Flag } from "lucide-react";
import { userReportsApi } from "@/lib/api/user-reports";
import { useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";

/** QOLF-9: lets a signed-in user report another user. Admins review before any action. */
export function ReportUserButton({ userId, username }: { userId: string; username: string }) {
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");

  const reportMutation = useMutation({
    mutationFn: async () => userReportsApi.report(userId, reason.trim()),
    onSuccess: (res) => {
      if (res.success) {
        toast("Report submitted. Thank you.", "success");
        setOpen(false);
        setReason("");
      } else {
        toast(res.error || "Failed to submit report.", "error");
      }
    },
    onError: () => toast("Failed to submit report.", "error"),
  });

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="ml-auto inline-flex shrink-0 items-center gap-1.5 self-start rounded-md border border-border px-2.5 py-1.5 text-xs text-fg-muted transition-colors hover:border-warning/50 hover:text-warning"
      >
        <Flag size={14} /> Report
      </button>

      <Modal open={open} onClose={() => setOpen(false)} title={`Report @${username}`} size="sm" dirty={!!reason.trim()}>
        <p className="text-sm text-fg-muted">
          Tell us what's wrong. An admin will review this report before any action is taken.
        </p>
        <textarea
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          rows={4}
          autoFocus
          placeholder="Describe the issue (harassment, spam, impersonation…)"
          className="mt-3 w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
        />
        <div className="mt-4 flex justify-end gap-2">
          <Button variant="secondary" onClick={() => setOpen(false)}>Cancel</Button>
          <Button variant="danger" disabled={!reason.trim()} loading={reportMutation.isPending}
            onClick={() => reportMutation.mutate()}>
            Submit report
          </Button>
        </div>
      </Modal>
    </>
  );
}
