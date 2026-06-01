"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useConfirm, useToast } from "@/lib/providers";
import { forumApi } from "@/lib/api/forum";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { Pager } from "./Pager";

export function ThreadsAdmin() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [showNew, setShowNew] = useState(false);
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

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

  const pinMutation = useMutation({ mutationFn: (id: string) => forumApi.pinThread(id), onSuccess: invalidate });
  const lockMutation = useMutation({ mutationFn: (id: string) => forumApi.lockThread(id), onSuccess: invalidate });
  const removeMutation = useMutation({ mutationFn: (id: string) => forumApi.deleteThread(id), onSuccess: invalidate });

  const remove = async (id: string) => {
    if (!(await confirm({ message: "Delete this thread and all its posts?", danger: true, confirmLabel: "Delete" }))) return;
    removeMutation.mutate(id);
  };

  if (loading) return <PageLoader />;

  const filtered = threads.filter((t) => {
    if (statusFilter === "pinned" && !t.isPinned) return false;
    if (statusFilter === "locked" && !t.isLocked) return false;
    if (search && !t.title.toLowerCase().includes(search) && !(t.authorName ?? "").toLowerCase().includes(search)) return false;
    return true;
  });

  return (
    <div>
      <AdminPageHeader title="Forum Threads" addLabel="New Thread" onAdd={() => setShowNew(true)} />
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search threads or author…"
        filters={[
          {
            value: statusFilter, onChange: setStatusFilter, ariaLabel: "Filter by status",
            options: [{ value: "all", label: "All threads" }, { value: "pinned", label: "Pinned" }, { value: "locked", label: "Locked" }],
          },
        ]}
      />

      <Modal open={showNew} onClose={() => setShowNew(false)} title="New Thread" size="lg">
        <NewThreadForm onDone={() => { setShowNew(false); invalidate(); }} onCancel={() => setShowNew(false)} />
      </Modal>

      <AdminTable
        columns={[
          { key: "title", label: "Thread" },
          { key: "author", label: "Author" },
          { key: "replies", label: "Replies" },
          { key: "activity", label: "Last reply" },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        empty={filtered.length === 0}
      >
        {filtered.map((t) => (
          <AdminRow key={t.id} onClick={() => router.push(`/admin/forum/threads/${t.id}`)}>
            <td className="px-3 py-2">
              {t.isPinned && <span className="mr-1 text-xs font-medium text-accent">[Pinned]</span>}
              {t.isLocked && <span className="mr-1 text-xs font-medium text-warning">[Locked]</span>}
              <span className="font-medium text-fg">{t.title}</span>
            </td>
            <td className="px-3 py-2 text-fg-muted">{t.authorName ?? "Unknown"}</td>
            <td className="px-3 py-2 tabular-nums text-fg-muted">{t.replyCount}</td>
            <td className="px-3 py-2 text-fg-muted">{formatDate(t.lastReplyAt)}</td>
            <td className="px-3 py-2 text-right">
              <div className="flex justify-end gap-2">
                <Button size="sm" variant="secondary" onClick={(e) => { e.stopPropagation(); pinMutation.mutate(t.id); }}>{t.isPinned ? "Unpin" : "Pin"}</Button>
                <Button size="sm" variant="secondary" onClick={(e) => { e.stopPropagation(); lockMutation.mutate(t.id); }}>{t.isLocked ? "Unlock" : "Lock"}</Button>
                <Button size="sm" variant="danger" onClick={(e) => { e.stopPropagation(); remove(t.id); }}>Delete</Button>
              </div>
            </td>
          </AdminRow>
        ))}
      </AdminTable>
      <Pager page={page} totalPages={totalPages} onPage={setPage} />
    </div>
  );
}

// Lets an admin start a thread from the dashboard (PROB-6 — admins post here, not on the user site).
function NewThreadForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const { toast } = useToast();
  const [categoryId, setCategoryId] = useState<number | "">("");
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");

  const { data: categories = [] } = useQuery({
    queryKey: qk.forum.categories(),
    queryFn: async () => {
      const res = await forumApi.getCategories();
      return res.success ? res.data ?? [] : [];
    },
  });

  const createMutation = useMutation({
    mutationFn: async () => forumApi.createThread({ categoryId: Number(categoryId), title: title.trim(), content: content.trim() }),
    onSuccess: (res) => {
      if (res.success) { toast("Thread created.", "success"); onDone(); }
      else toast(res.error || "Failed to create thread.", "error");
    },
    onError: () => toast("Failed to create thread.", "error"),
  });

  const valid = categoryId !== "" && title.trim().length >= 3 && content.trim().length >= 10;

  return (
    <div className="space-y-3">
      <Select label="Category" value={categoryId} onChange={(e) => setCategoryId(e.target.value === "" ? "" : Number(e.target.value))}>
        <option value="">Select a category…</option>
        {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
      </Select>
      <Input label="Title" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Thread title" />
      <div className="space-y-1.5">
        <label className="block text-xs font-medium uppercase tracking-wider text-fg-muted">Content (Markdown)</label>
        <textarea
          value={content}
          onChange={(e) => setContent(e.target.value)}
          rows={8}
          placeholder="Write the opening post in Markdown…"
          className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
        />
      </div>
      <div className="flex gap-2">
        <Button onClick={() => createMutation.mutate()} loading={createMutation.isPending} disabled={!valid}>Create Thread</Button>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}
