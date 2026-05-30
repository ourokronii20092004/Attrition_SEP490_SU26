"use client";

import { useEffect, useState } from "react";
import { wikiApi } from "@/lib/api/wiki";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import type { WikiContributionDto } from "@/lib/types";

export default function AdminWikiPage() {
  const { user } = useAuth();
  const [contributions, setContributions] = useState<WikiContributionDto[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchContributions = () => {
    setLoading(true);
    wikiApi
      .getContributions()
      .then((res) => {
        if (res.success) setContributions(res.data);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (user?.role !== "Admin") return;
    fetchContributions();
  }, [user]);

  const handleReview = async (id: string, status: "Approved" | "Rejected") => {
    await wikiApi.reviewContribution(id, { status });
    fetchContributions();
  };

  if (!user || user.role !== "Admin") return null;
  if (loading) return <PageLoader />;

  const pending = contributions.filter((c) => c.status === "Pending");

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="font-display text-3xl font-bold text-fg">Wiki Management</h1>
      <p className="mt-2 text-fg-muted">{pending.length} pending contributions</p>

      <div className="mt-6 space-y-4">
        {pending.map((c) => (
          <div key={c.id} className="card p-4">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="font-medium text-fg">{c.articleTitle}</p>
                <p className="mt-1 text-sm text-fg-muted">by {c.contributorName} &middot; {new Date(c.submittedAt).toLocaleDateString()}</p>
                <p className="mt-1 text-sm text-fg-subtle">{c.changeNote ?? "No note"}</p>
              </div>
              <div className="flex shrink-0 gap-2">
                <Button size="sm" onClick={() => handleReview(c.id, "Approved")}>Approve</Button>
                <Button size="sm" variant="danger" onClick={() => handleReview(c.id, "Rejected")}>Reject</Button>
              </div>
            </div>
          </div>
        ))}
        {pending.length === 0 && <p className="py-8 text-center text-fg-muted">No pending contributions.</p>}
      </div>
    </div>
  );
}
