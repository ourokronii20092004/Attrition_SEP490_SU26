"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ChevronDown, ChevronRight, GitCompare, Eye } from "lucide-react";
import { wikiApi } from "@/lib/api/wiki";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { LineDiff } from "@/components/line-diff";
import { MarkdownContent } from "@/components/post-content";
import { AdminPageHeader, AdminFilterBar } from "@/components/admin/admin-table";
import { Pagination } from "@/components/ui/pagination";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { useUrlPagination } from "@/lib/hooks/use-url-pagination";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { LIVE_NORMAL, liveWhenFocused } from "@/lib/live";

const STATUS_OPTIONS = [
  { value: "Pending", label: "Pending" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" },
  { value: "all", label: "All statuses" },
];

export function ContributionQueue() {
  const queryClient = useQueryClient();
  const [openId, setOpenId] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<"diff" | "preview">("diff");
  const [statusFilter, setStatusFilter] = useState("Pending");
  const [searchInput, setSearchInput] = useState("");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: items = [], isPending: loading } = useQuery({
    queryKey: qk.admin.wiki.contributions(),
    // Contributions arrive while moderators work through the queue.
    refetchInterval: liveWhenFocused(LIVE_NORMAL),
    queryFn: async () => {
      const res = await wikiApi.getContributions();
      return res.success ? res.data : [];
    },
  });

  const reviewMutation = useMutation({
    mutationFn: async ({ id, status }: { id: string; status: "Approved" | "Rejected" }) => {
      await wikiApi.reviewContribution(id, { status });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.admin.wiki.contributions() });
    },
  });

  const review = (id: string, status: "Approved" | "Rejected") => {
    reviewMutation.mutate({ id, status });
  };

  const filtered = items.filter((c) => {
    if (statusFilter !== "all" && c.status !== statusFilter) return false;
    if (search && !c.articleTitle.toLowerCase().includes(search) && !(c.contributorName ?? "").toLowerCase().includes(search)) return false;
    return true;
  });
  const { page, setPage, totalPages, paged } = useUrlPagination(filtered, 10);

  return (
    <div>
      <AdminPageHeader title="Contribution Queue" />
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search by article or contributor…"
        filters={[{ value: statusFilter, onChange: setStatusFilter, ariaLabel: "Filter by status", options: STATUS_OPTIONS }]}
      />

      {loading ? (
        <PageLoader />
      ) : filtered.length === 0 ? (
        <p className="py-8 text-center text-fg-muted">No contributions match.</p>
      ) : (
        <div className="mt-4 space-y-4">
          {paged.map((c) => {
            const open = openId === c.id;
            return (
              <div key={c.id} className="card p-4">
                <div className="flex items-start justify-between gap-4">
                  <div className="min-w-0">
                    <p className="font-medium text-fg">{c.articleTitle}</p>
                    <p className="mt-1 text-sm text-fg-muted">
                      by {c.contributorName} · {formatDate(c.submittedAt)}
                      {c.status !== "Pending" && (
                        <span className={`ml-2 rounded px-1.5 py-0.5 text-xs font-medium ${c.status === "Approved" ? "bg-success/10 text-success" : "bg-danger/10 text-danger"}`}>{c.status}</span>
                      )}
                    </p>
                    <p className="mt-1 text-sm text-fg-subtle">{c.changeNote ?? "No note"}</p>
                  </div>
                  {c.status === "Pending" && (
                    <div className="flex shrink-0 gap-2">
                      <Button size="sm" onClick={() => review(c.id, "Approved")} loading={reviewMutation.isPending}>Approve</Button>
                      <Button size="sm" variant="danger" onClick={() => review(c.id, "Rejected")} loading={reviewMutation.isPending}>Reject</Button>
                    </div>
                  )}
                </div>

                <button
                  onClick={() => { setOpenId(open ? null : c.id); setViewMode("diff"); }}
                  className="mt-3 inline-flex items-center gap-1 text-sm text-accent transition-colors hover:underline"
                >
                  {open ? <ChevronDown size={15} /> : <ChevronRight size={15} />}
                  {open ? "Hide changes" : "Review changes"}
                </button>

                {open && (
                  <div className="mt-3">
                    <div className="mb-3 inline-flex rounded-lg border border-border p-0.5">
                      <button
                        onClick={() => setViewMode("diff")}
                        className={`inline-flex items-center gap-1.5 rounded-md px-3 py-1 text-xs font-medium transition-colors ${viewMode === "diff" ? "bg-accent text-accent-fg" : "text-fg-muted hover:text-fg"}`}
                      >
                        <GitCompare size={13} /> Diff
                      </button>
                      <button
                        onClick={() => setViewMode("preview")}
                        className={`inline-flex items-center gap-1.5 rounded-md px-3 py-1 text-xs font-medium transition-colors ${viewMode === "preview" ? "bg-accent text-accent-fg" : "text-fg-muted hover:text-fg"}`}
                      >
                        <Eye size={13} /> Rendered preview
                      </button>
                    </div>
                    {viewMode === "diff" ? (
                      <LineDiff oldText={c.currentContent} newText={c.suggestedContent} />
                    ) : (
                      <div className="rounded-lg border border-border bg-surface-2 p-4">
                        <MarkdownContent content={c.suggestedContent} />
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })}
          <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />
        </div>
      )}
    </div>
  );
}
