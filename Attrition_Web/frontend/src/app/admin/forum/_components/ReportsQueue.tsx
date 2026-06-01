"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { forumApi } from "@/lib/api/forum";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { Pager } from "./Pager";

export function ReportsQueue() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("Pending");
  const [searchInput, setSearchInput] = useState("");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data, isPending: loading } = useQuery({
    queryKey: qk.admin.forum.reports(`${statusFilter}-${page}`),
    queryFn: async () => {
      const res = await forumApi.getReports({ status: statusFilter, page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const reports = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const invalidate = () => queryClient.invalidateQueries({ queryKey: qk.admin.forum.reports() });

  const dismissMutation = useMutation({
    mutationFn: async (id: string) => { await forumApi.dismissReport(id); },
    onSuccess: invalidate,
  });
  const removePostMutation = useMutation({
    mutationFn: async ({ postId, reason }: { postId: string; reason: string }) => { await forumApi.removePost(postId, { reason }); },
    onSuccess: invalidate,
  });

  if (loading) return <PageLoader />;

  const filtered = search
    ? reports.filter((r) => r.authorName.toLowerCase().includes(search) || r.reporterName.toLowerCase().includes(search) || r.reason.toLowerCase().includes(search))
    : reports;

  return (
    <div>
      <AdminPageHeader title="Forum Reports" />
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search author, reporter, or reason…"
        filters={[
          {
            value: statusFilter, onChange: (v) => { setStatusFilter(v); setPage(1); }, ariaLabel: "Filter by status",
            options: [{ value: "Pending", label: "Pending" }, { value: "Resolved", label: "Resolved" }, { value: "Dismissed", label: "Dismissed" }],
          },
        ]}
      />

      <AdminTable
        columns={[
          { key: "post", label: "Reported post" },
          { key: "reporter", label: "Reporter" },
          { key: "reason", label: "Reason" },
          { key: "when", label: "When" },
          { key: "actions", label: "Actions", align: "right" },
        ]}
        empty={filtered.length === 0}
      >
        {filtered.map((r) => (
          <AdminRow key={r.id}>
            <td className="px-3 py-2">
              <span className="font-medium text-fg">{r.authorName}</span>
              <p className="mt-0.5 line-clamp-1 max-w-md text-xs text-fg-muted">{r.postContent}</p>
            </td>
            <td className="px-3 py-2 text-fg-muted">{r.reporterName}</td>
            <td className="px-3 py-2"><span className="line-clamp-1 max-w-[12rem] text-fg-muted">{r.reason}</span></td>
            <td className="px-3 py-2 text-fg-subtle">{formatDate(r.createdAt)}</td>
            <td className="px-3 py-2 text-right">
              {statusFilter === "Pending" && (
                <div className="flex justify-end gap-2">
                  <Button size="sm" variant="danger"
                    loading={removePostMutation.isPending && removePostMutation.variables?.postId === r.postId}
                    onClick={() => removePostMutation.mutate({ postId: r.postId, reason: r.reason })}>Remove Post</Button>
                  <Button size="sm" variant="secondary"
                    loading={dismissMutation.isPending && dismissMutation.variables === r.id}
                    onClick={() => dismissMutation.mutate(r.id)}>Dismiss</Button>
                </div>
              )}
            </td>
          </AdminRow>
        ))}
      </AdminTable>
      <Pager page={page} totalPages={totalPages} onPage={setPage} />
    </div>
  );
}
