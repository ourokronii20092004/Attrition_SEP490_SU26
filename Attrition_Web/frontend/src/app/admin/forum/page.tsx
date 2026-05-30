"use client";

import { useEffect, useState } from "react";
import { forumApi } from "@/lib/api/forum";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import type { AdminPostReportDto, AdminForumThreadDto, ForumCategoryDto } from "@/lib/types";

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
  const [reports, setReports] = useState<AdminPostReportDto[]>([]);
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    forumApi.getReports().then((r) => { if (r.success) setReports(r.data); }).finally(() => setLoading(false));
  };
  useEffect(load, []);

  const dismiss = async (id: string) => { await forumApi.dismissReport(id); load(); };
  const removePost = async (postId: string, reason: string) => { await forumApi.removePost(postId, { reason }); load(); };

  if (loading) return <PageLoader />;
  const pending = reports.filter((r) => r.status === "Pending");
  if (!pending.length) return <p className="py-8 text-center text-fg-muted">No pending reports.</p>;

  return (
    <div className="space-y-4">
      {pending.map((r) => (
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
    </div>
  );
}

function ThreadsAdmin() {
  const [threads, setThreads] = useState<AdminForumThreadDto[]>([]);
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    forumApi.getAdminThreads().then((r) => { if (r.success) setThreads(r.data); }).finally(() => setLoading(false));
  };
  useEffect(load, []);

  const pin = async (id: string) => { await forumApi.pinThread(id); load(); };
  const lock = async (id: string) => { await forumApi.lockThread(id); load(); };
  const remove = async (id: string) => { if (!confirm("Delete this thread and all its posts?")) return; await forumApi.deleteThread(id); load(); };

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
    </div>
  );
}

function CategoriesAdmin() {
  const [categories, setCategories] = useState<ForumCategoryDto[]>([]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    forumApi.getCategories().then((r) => { if (r.success) setCategories(r.data); }).finally(() => setLoading(false));
  };
  useEffect(load, []);

  const create = async () => {
    if (!name.trim()) return;
    await forumApi.createCategory({ name, description });
    setName(""); setDescription(""); load();
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
