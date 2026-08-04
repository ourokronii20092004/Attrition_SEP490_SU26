"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ExternalLink, ShieldAlert } from "lucide-react";
import { userReportsApi } from "@/lib/api/user-reports";
import { useAuth, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { Pagination } from "@/components/ui/pagination";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatDate } from "@/lib/format-date";
import type { AdminUserReportDto } from "@/lib/types";
import { LIVE_NORMAL, liveWhenFocused } from "@/lib/live";
import { useUrlPage } from "@/lib/hooks/use-url-pagination";

function AdminUserReportsList() {
  const { user: me } = useAuth();
  const { toast } = useToast();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState("Pending");
  const [page, setPage] = useUrlPage();
  const [searchInput, setSearchInput] = useState("");
  const [resolving, setResolving] = useState<AdminUserReportDto | null>(null);
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data, isPending } = useQuery({
    queryKey: ["admin", "user-reports", status, page] as const,
    // New user reports should appear in the queue without a refresh.
    refetchInterval: liveWhenFocused(LIVE_NORMAL),
    enabled: me?.role === "Admin",
    queryFn: async () => {
      const res = await userReportsApi.adminList({ status, page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin", "user-reports"] });

  const resolveMutation = useMutation({
    mutationFn: ({ id, banUser, note }: { id: string; banUser: boolean; note: string }) =>
      userReportsApi.adminResolve(id, { banUser, note }),
    onSuccess: (_d, vars) => { invalidate(); setResolving(null); toast(vars.banUser ? "Report resolved and user banned." : "Report resolved.", "success"); },
    onError: () => toast("Action failed.", "error"),
  });
  const dismissMutation = useMutation({
    mutationFn: (id: string) => userReportsApi.adminDismiss(id),
    onSuccess: () => { invalidate(); toast("Report dismissed.", "success"); },
    onError: () => toast("Action failed.", "error"),
  });

  if (!me || me.role !== "Admin") return null;
  const reports = data?.items ?? [];
  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 0;

  const filtered = search
    ? reports.filter((r) => r.reportedUserName.toLowerCase().includes(search) || r.reporterName.toLowerCase().includes(search) || r.reason.toLowerCase().includes(search))
    : reports;

  return (
    <div>
      <AdminPageHeader title="User Reports" />
      <AdminFilterBar
        search={searchInput}
        onSearch={setSearchInput}
        searchPlaceholder="Search reported user, reporter, or reason…"
        filters={[
          {
            value: status, onChange: (v) => setStatus(v), ariaLabel: "Filter by status",
            options: [{ value: "Pending", label: "Pending" }, { value: "Resolved", label: "Resolved" }, { value: "Dismissed", label: "Dismissed" }],
          },
        ]}
      />

      <ResolveModal
        key={resolving?.id ?? "none"}
        report={resolving}
        onClose={() => setResolving(null)}
        loading={resolveMutation.isPending}
        onConfirm={(banUser, note) => resolving && resolveMutation.mutate({ id: resolving.id, banUser, note })}
      />

      {isPending ? (
        <PageLoader />
      ) : (
        <AdminTable
          columns={[
            { key: "reported", label: "Reported user" },
            { key: "reason", label: "Reason" },
            { key: "reporter", label: "Reporter" },
            { key: "outcome", label: status === "Pending" ? "When" : "Outcome" },
            { key: "actions", label: "Actions", align: "right" },
          ]}
          empty={filtered.length === 0}
        >
          {filtered.map((r) => (
            <AdminRow key={r.id} onClick={() => router.push(`/admin/users/${r.reportedUserId}`)}>
              <td className="px-3 py-2">
                <span className="font-medium text-fg">{r.reportedUserName}</span>
                <Link href={`/u/${encodeURIComponent(r.reportedUserName)}`} target="_blank" onClick={(e) => e.stopPropagation()}
                  className="ml-2 inline-flex items-center gap-0.5 text-xs text-fg-subtle hover:text-accent">
                  profile <ExternalLink size={11} />
                </Link>
              </td>
              <td className="px-3 py-2"><span className="line-clamp-1 max-w-sm text-fg-muted">{r.reason}</span></td>
              <td className="px-3 py-2 text-fg-muted">{r.reporterName}</td>
              <td className="px-3 py-2 text-fg-subtle">
                {status === "Pending" ? (
                  formatDate(r.createdAt)
                ) : (
                  <div className="text-xs">
                    {r.actionTaken === "Banned" && <span className="mr-1.5 rounded bg-danger/10 px-1.5 py-0.5 font-medium text-danger">Banned</span>}
                    {r.resolvedByName ? `by ${r.resolvedByName}` : "—"}
                    {r.moderatorNote && <p className="mt-0.5 text-fg-muted">{r.moderatorNote}</p>}
                  </div>
                )}
              </td>
              <td className="px-3 py-2 text-right">
                {status === "Pending" && (
                  <div className="flex justify-end gap-2">
                    <Button size="sm" onClick={(e) => { e.stopPropagation(); setResolving(r); }}>Resolve…</Button>
                    <Button size="sm" variant="secondary" onClick={(e) => { e.stopPropagation(); dismissMutation.mutate(r.id); }} loading={dismissMutation.isPending}>Dismiss</Button>
                  </div>
                )}
              </td>
            </AdminRow>
          ))}
        </AdminTable>
      )}

      {totalPages > 1 && (
        <Pagination page={page} totalPages={totalPages} onChange={setPage} compact />
      )}
    </div>
  );
}

function ResolveModal({ report, onClose, onConfirm, loading }: {
  report: AdminUserReportDto | null;
  onClose: () => void;
  onConfirm: (banUser: boolean, note: string) => void;
  loading: boolean;
}) {
  const [ban, setBan] = useState(false);
  const [note, setNote] = useState("");

  return (
    <Modal open={report != null} onClose={onClose} title="Resolve report">
      {report && (
        <div className="space-y-4">
          <div className="rounded-lg bg-surface-2 p-3 text-sm">
            <p className="text-fg"><span className="text-fg-muted">Reported:</span> {report.reportedUserName}</p>
            <p className="mt-1 text-fg"><span className="text-fg-muted">Reason:</span> {report.reason}</p>
            <p className="mt-1 text-fg-subtle">by {report.reporterName} · {formatDate(report.createdAt)}</p>
          </div>

          <label className="flex cursor-pointer items-start gap-2.5 rounded-lg border border-border p-3 transition-colors hover:border-danger/50">
            <input type="checkbox" checked={ban} onChange={(e) => setBan(e.target.checked)} className="mt-0.5 rounded border-border" />
            <span>
              <span className="flex items-center gap-1.5 text-sm font-medium text-fg">
                <ShieldAlert size={15} className="text-danger" /> Ban {report.reportedUserName}
              </span>
              <span className="mt-0.5 block text-xs text-fg-muted">They&apos;ll be signed out and blocked from signing in. You can unban later from the user page.</span>
            </span>
          </label>

          <div>
            <label className="text-xs uppercase tracking-wider text-fg-subtle">Moderator note (optional)</label>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              rows={2}
              placeholder="What action did you take and why?"
              className="mt-1 w-full resize-y rounded-md border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none focus:border-accent"
            />
          </div>

          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={onClose}>Cancel</Button>
            <Button variant={ban ? "danger" : "primary"} loading={loading} onClick={() => onConfirm(ban, note)}>
              {ban ? "Resolve & ban" : "Resolve"}
            </Button>
          </div>
        </div>
      )}
    </Modal>
  );
}

export default function AdminUserReportsPage() {
  return (
    <Suspense fallback={null}>
      <AdminUserReportsList />
    </Suspense>
  );
}
