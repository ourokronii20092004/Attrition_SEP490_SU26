"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { forumApi } from "@/lib/api/forum";
import { Button } from "@/components/ui/button";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { useUrlPage } from "@/lib/hooks/use-url-pagination";
import { Pager } from "./Pager";

// Posts removed by moderators vanish from the public thread view, and until this page existed there
// was no surface to review or restore them — the restore endpoint was wired but unreachable.
export function PostsModeration() {
  const queryClient = useQueryClient();
  const [page, setPage] = useUrlPage();
  const [searchInput, setSearchInput] = useState("");
  const search = useDebouncedValue(searchInput.trim(), 300);

  const { data, isPending: loading } = useQuery({
    queryKey: [...qk.admin.forum.posts(page), search],
    queryFn: async () => {
      const res = await forumApi.getAdminPosts({ removedOnly: true, search: search || undefined, page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const posts = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.forum.posts() });

  const restoreMutation = useMutation({ mutationFn: (id: string) => forumApi.restorePost(id), onSuccess: invalidate });

  return (
    <div>
      <AdminPageHeader title="Removed Posts" />
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search removed post content…"
      />
      <AdminTable
        columns={[
          { key: "post", label: "Post" },
          { key: "author", label: "Author" },
          { key: "removed", label: "Removed" },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        loading={loading}
        empty={posts.length === 0}
        emptyLabel={search ? "No removed posts match this search." : "No removed posts."}
        emptyHint={search ? "Try a different search." : "Posts removed from threads or the reports queue land here."}
      >
        {posts.map((p) => (
          <AdminRow key={p.id}>
            <td className="max-w-[28rem] px-3 py-2">
              <p className="truncate text-sm text-fg">{p.content.trim() || "(empty post)"}</p>
              <p className="truncate text-xs text-fg-subtle">Thread {p.threadId.slice(0, 8)}…</p>
            </td>
            <td className="px-3 py-2 text-fg-muted">{p.authorName ?? "Unknown"}</td>
            <td className="px-3 py-2 text-fg-muted">
              <p className="text-sm">{p.removedByName ?? "Unknown"} · {p.removedAt ? formatDate(p.removedAt) : "—"}</p>
              {p.removedReason && <p className="truncate text-xs italic text-fg-subtle">{p.removedReason}</p>}
            </td>
            <td className="px-3 py-2 text-right">
              <Button size="sm" variant="secondary" onClick={() => restoreMutation.mutate(p.id)}>Restore</Button>
            </td>
          </AdminRow>
        ))}
      </AdminTable>
      <Pager page={page} totalPages={totalPages} onPage={setPage} />
    </div>
  );
}
