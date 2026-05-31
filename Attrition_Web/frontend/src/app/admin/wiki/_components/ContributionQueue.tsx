"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { wikiApi } from "@/lib/api/wiki";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";

export function ContributionQueue() {
  const queryClient = useQueryClient();

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

  if (loading) return <PageLoader />;
  const pending = items.filter((c) => c.status === "Pending");
  if (!pending.length) return <p className="py-8 text-center text-fg-muted">No pending contributions.</p>;

  return (
    <div className="space-y-4">
      {pending.map((c) => (
        <div key={c.id} className="card p-4">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="font-medium text-fg">{c.articleTitle}</p>
              <p className="mt-1 text-sm text-fg-muted">by {c.contributorName} · {formatDate(c.submittedAt)}</p>
              <p className="mt-1 text-sm text-fg-subtle">{c.changeNote ?? "No note"}</p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button size="sm" onClick={() => review(c.id, "Approved")}>Approve</Button>
              <Button size="sm" variant="danger" onClick={() => review(c.id, "Rejected")}>Reject</Button>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
