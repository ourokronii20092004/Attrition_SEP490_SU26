"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ChevronDown, ChevronRight } from "lucide-react";
import { wikiApi } from "@/lib/api/wiki";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { LineDiff } from "@/components/line-diff";
import { AdminPageHeader, AdminFilterBar } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";

export function ContributionQueue() {
  const queryClient = useQueryClient();
  const [openId, setOpenId] = useState<string | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data: items = [], isPending: loading } = useQuery({
    queryKey: qk.admin.wiki.contributions(),
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

  const pending = items.filter((c) => c.status === "Pending");
  const filtered = search
    ? pending.filter((c) => c.articleTitle.toLowerCase().includes(search) || (c.contributorName ?? "").toLowerCase().includes(search))
    : pending;

  return (
    <div>
      <AdminPageHeader title="Contribution Queue" />
      <AdminFilterBar search={searchInput} onSearch={setSearchInput} searchPlaceholder="Search by article or contributor…" />

      {loading ? (
        <PageLoader />
      ) : filtered.length === 0 ? (
        <p className="py-8 text-center text-fg-muted">No pending contributions.</p>
      ) : (
        <div className="mt-4 space-y-4">
          {filtered.map((c) => {
            const open = openId === c.id;
            return (
              <div key={c.id} className="card p-4">
                <div className="flex items-start justify-between gap-4">
                  <div className="min-w-0">
                    <p className="font-medium text-fg">{c.articleTitle}</p>
                    <p className="mt-1 text-sm text-fg-muted">by {c.contributorName} · {formatDate(c.submittedAt)}</p>
                    <p className="mt-1 text-sm text-fg-subtle">{c.changeNote ?? "No note"}</p>
                  </div>
                  <div className="flex shrink-0 gap-2">
                    <Button size="sm" onClick={() => review(c.id, "Approved")} loading={reviewMutation.isPending}>Approve</Button>
                    <Button size="sm" variant="danger" onClick={() => review(c.id, "Rejected")} loading={reviewMutation.isPending}>Reject</Button>
                  </div>
                </div>

                <button
                  onClick={() => setOpenId(open ? null : c.id)}
                  className="mt-3 inline-flex items-center gap-1 text-sm text-accent transition-colors hover:underline"
                >
                  {open ? <ChevronDown size={15} /> : <ChevronRight size={15} />}
                  {open ? "Hide changes" : "Review changes"}
                </button>

                {open && (
                  <div className="mt-3">
                    <LineDiff oldText={c.currentContent} newText={c.suggestedContent} />
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
