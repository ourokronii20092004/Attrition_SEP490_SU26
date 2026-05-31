"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { forumApi } from "@/lib/api/forum";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";

type Tab = "reports" | "threads" | "categories";

export default function AdminForumPage() {
  const [tab, setTab] = useState<Tab>("reports");
  return (
    <div className="mx-auto max-w-6xl">
      <h1 className="font-display text-3xl font-bold text-fg">Forum Management</h1>
      <div className="mt-4 flex gap-1 border-b border-border">
        {(["reports", "threads", "categories"] as Tab[]).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`relative px-4 py-2.5 text-sm font-medium capitalize transition-colors ${tab === t ? "text-accent" : "text-fg-muted hover:text-fg"}`}
          >
            {t}
            {tab === t && <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-accent" />}
          </button>
        ))}
      </div>
      <div className="mt-6">
        {tab === "reports" && <ReportsQueue />}
        {tab === "threads" && <ThreadsAdmin />}
        {tab === "categories" && <CategoriesAdmin />}
      </div>
    </div>
  );
}

function ReportsQueue() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);

  const { data, isPending: loading } = useQuery({
    queryKey: ["admin", "forum", "reports", page],
    queryFn: async () => {
      const res = await forumApi.getReports({ status: "Pending", page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const reports = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin", "forum", "reports"] });

  const dismissMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.dismissReport(id); },
    onSuccess: invalidate,
  });
  const removePostMutation = useMutation({
    mutationFn: async ({ postId, reason }: { postId: string; reason: string }) => { await forumApi.removePost(postId, { reason }); },
    onSuccess: invalidate,
  });

  const dismiss = (id: string) => dismissMutation.mutate(id);
  const removePost = (postId: string, reason: string) => removePostMutation.mutate({ postId, reason });

  if (loading) return <PageLoader />;
  if (!reports.length) return <p className="py-8 text-center text-fg-muted">No pending reports.</p>;

  return (
    <div className="space-y-4">
      {reports.map((r) => (
        <div key={r.id} className="card p-4">
          <div className="flex items-start justify-between gap-4">
            <div className="min-w-0 flex-1">
              <p className="font-medium text-fg">Post by {r.authorName}</p>
              <p className="mt-1 line-clamp-3 whitespace-pre-wrap text-sm text-fg">{r.postContent}</p>
              <p className="mt-2 text-sm text-fg-muted">Reported by {r.reporterName} · {formatDate(r.createdAt)}</p>
              <p className="mt-1 text-sm text-fg-subtle">Reason: {r.reason}</p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button size="sm" variant="danger" onClick={() => removePost(r.postId, r.reason)}>Remove Post</Button>
              <Button size="sm" variant="secondary" onClick={() => dismiss(r.id)}>Dismiss</Button>
            </div>
          </div>
        </div>
      ))}
      <Pager page={page} totalPages={totalPages} onPage={setPage} />
    </div>
  );
}

function ThreadsAdmin() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);

  const { data, isPending: loading } = useQuery({
    queryKey: ["admin", "forum", "threads", page],
    queryFn: async () => {
      const res = await forumApi.getAdminThreads({ page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const threads = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin", "forum", "threads"] });

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
  const remove = (id: string) => { if (!confirm("Delete this thread and all its posts?")) return; removeMutation.mutate(id); };

  if (loading) return <PageLoader />;
  if (!threads.length) return <p className="py-8 text-center text-fg-muted">No threads yet.</p>;

  return (
    <div className="space-y-2">
      {threads.map((t) => (
        <div key={t.id} className="card flex items-center justify-between gap-3 p-4">
          <div className="min-w-0">
            <p className="truncate font-medium text-fg">
              {t.isPinned && <span className="mr-1 text-accent">[Pinned]</span>}
              {t.isLocked && <span className="mr-1 text-warning">[Locked]</span>}
              {t.title}
            </p>
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

/** Minimal prev/next pager shared by the admin moderation lists. */
function Pager({ page, totalPages, onPage }: { page: number; totalPages: number; onPage: (p: number) => void }) {
  if (totalPages <= 1) return null;
  return (
    <div className="flex items-center justify-center gap-3 pt-2">
      <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</Button>
      <span className="text-sm text-fg-muted">Page {page} of {totalPages}</span>
      <Button size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>Next</Button>
    </div>
  );
}

function CategoriesAdmin() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  const { data: categories = [], isPending: loading } = useQuery({
    queryKey: ["forum", "categories"],
    queryFn: async () => {
      const res = await forumApi.getCategories();
      return res.success ? res.data : [];
    },
  });

  const createMutation = useMutation({
    mutationFn: async () => { await forumApi.createCategory({ name, description }); },
    onSuccess: () => {
      setName(""); setDescription("");
      queryClient.invalidateQueries({ queryKey: ["forum", "categories"] });
    },
  });

  const create = () => {
    if (!name.trim()) return;
    createMutation.mutate();
  };

  if (loading) return <PageLoader />;

  return (
    <div>
      <div className="card mb-6 space-y-3 p-4">
        <Input label="New category name" value={name} onChange={(e) => setName(e.target.value)} />
        <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
        <Button onClick={create} disabled={!name.trim()}>Add Category</Button>
      </div>
      <div className="space-y-2">
        {categories.map((c) => (
          <div key={c.id} className="card flex items-center justify-between p-4">
            <div>
              <p className="font-medium text-fg">{c.name}</p>
              <p className="text-xs text-fg-muted">{c.threadCount} threads · {c.slug}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
