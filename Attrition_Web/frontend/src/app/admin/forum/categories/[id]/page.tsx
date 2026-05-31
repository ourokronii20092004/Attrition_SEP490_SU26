"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { useAuth, useConfirm, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { Pager } from "../../_components/Pager";

export default function AdminCategoryThreadsPage() {
  const { user } = useAuth();
  const params = useParams<{ id: string }>();
  const categoryId = Number(params.id);
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const { toast } = useToast();
  const [page, setPage] = useState(1);

  const { data, isPending: loading } = useQuery({
    queryKey: qk.forum.threads({ categoryId, page }),
    enabled: user?.role === "Admin" && Number.isFinite(categoryId),
    queryFn: async () => {
      const res = await forumApi.getThreads({ categoryId, page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const threads = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.forum.threads() });

  const pinMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.pinThread(id); },
    onSuccess: () => { toast("Thread updated.", "success"); invalidate(); },
    onError: () => toast("Could not update the thread.", "error"),
  });
  const lockMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.lockThread(id); },
    onSuccess: () => { toast("Thread updated.", "success"); invalidate(); },
    onError: () => toast("Could not update the thread.", "error"),
  });
  const removeMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.deleteThread(id); },
    onSuccess: () => { toast("Thread deleted.", "success"); invalidate(); },
    onError: () => toast("Could not delete the thread.", "error"),
  });

  const pin = (id: string) => pinMutation.mutate(id);
  const lock = (id: string) => lockMutation.mutate(id);
  const remove = async (id: string) => {
    if (!(await confirm({ message: "Delete this thread and all its posts?", danger: true, confirmLabel: "Delete" }))) return;
    removeMutation.mutate(id);
  };

  if (!user || user.role !== "Admin") return null;

  return (
    <div className="mx-auto max-w-5xl">
      <Link href="/admin/forum" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Forum Management
      </Link>
      <h1 className="mt-4 font-display text-3xl font-bold text-fg">Category Threads</h1>

      {loading ? (
        <PageLoader />
      ) : !threads.length ? (
        <p className="py-8 text-center text-fg-muted">No threads in this category.</p>
      ) : (
        <div className="mt-6 space-y-2">
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
      )}
    </div>
  );
}
