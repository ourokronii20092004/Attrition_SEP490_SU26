"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useConfirm } from "@/lib/providers";
import { forumApi } from "@/lib/api/forum";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { Pager } from "./Pager";

export function ThreadsAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [page, setPage] = useState(1);

  const { data, isPending: loading } = useQuery({
    queryKey: qk.admin.forum.threads(page),
    queryFn: async () => {
      const res = await forumApi.getAdminThreads({ page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const threads = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.forum.threads() });

  const pinMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.pinThread(id); },
    onSuccess: invalidate,
  });
  const lockMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.lockThread(id); },
    onSuccess: invalidate,
  });
  const removeMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.deleteThread(id); },
    onSuccess: invalidate,
  });

  const pin = (id: string) => pinMutation.mutate(id);
  const lock = (id: string) => lockMutation.mutate(id);
  const remove = async (id: string) => { if (!(await confirm({ message: "Delete this thread and all its posts?", danger: true, confirmLabel: "Delete" }))) return; removeMutation.mutate(id); };

  if (loading) return <PageLoader />;
  if (!threads.length) return <p className="py-8 text-center text-fg-muted">No threads yet.</p>;

  return (
    <div className="space-y-2">
      {threads.map((t) => (
        <div key={t.id} className="card flex items-center justify-between gap-3 p-4">
          <div className="min-w-0">
            <Link href={`/admin/forum/threads/${t.id}`} className="block truncate font-medium text-fg transition-colors hover:text-accent">
              {t.isPinned && <span className="mr-1 text-accent">[Pinned]</span>}
              {t.isLocked && <span className="mr-1 text-warning">[Locked]</span>}
              {t.title}
            </Link>
            <p className="text-xs text-fg-muted">{t.authorName ?? "Unknown"} · {t.replyCount} replies · {formatDate(t.lastReplyAt)}</p>
          </div>
          <div className="flex shrink-0 gap-2">
            <Button size="sm" variant="secondary" onClick={() => pin(t.id)}>{t.isPinned ? "Unpin" : "Pin"}</Button>
            <Button size="sm" variant="secondary" onClick={() => lock(t.id)}>{t.isLocked ? "Unlock" : "Lock"}</Button>
            <Button size="sm" variant="danger" onClick={() => remove(t.id)}>Delete</Button>
          </div>
        </div>
      ))}
      <Pager page={page} totalPages={totalPages} onPage={setPage} />
    </div>
  );
}
