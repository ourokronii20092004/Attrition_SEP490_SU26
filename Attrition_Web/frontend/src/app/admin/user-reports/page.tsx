"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ExternalLink } from "lucide-react";
import { userReportsApi } from "@/lib/api/user-reports";
import { useAuth, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { formatDate } from "@/lib/format-date";

export default function AdminUserReportsPage() {
  const { user: me } = useAuth();
  const { toast } = useToast();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState("Pending");
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const search = useDebouncedValue(searchInput.trim().toLowerCase(), 200);

  const { data, isPending } = useQuery({
    queryKey: ["admin", "user-reports", status, page] as const,
    enabled: me?.role === "Admin",
    queryFn: async () => {
      const res = await userReportsApi.adminList({ status, page, pageSize: 20 });
      return res.success ? res.data : null;
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin", "user-reports"] });

  const resolveMutation = useMutation({
    mutationFn: (id: string) => userReportsApi.adminResolve(id),
    onSuccess: () => { invalidate(); toast("Report resolved.", "success"); },
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
            value: status, onChange: (v) => { setStatus(v); setPage(1); }, ariaLabel: "Filter by status",
            options: [{ value: "Pending", label: "Pending" }, { value: "Resolved", label: "Resolved" }, { value: "Dismissed", label: "Dismissed" }],
          },
        ]}
      />

      {isPending ? (
        <PageLoader />
      ) : (
        <AdminTable
          columns={[
            { key: "reported", label: "Reported user" },
            { key: "reason", label: "Reason" },
            { key: "reporter", label: "Reporter" },
            { key: "when", label: "When" },
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
              <td className="px-3 py-2 text-fg-subtle">{formatDate(r.createdAt)}</td>
              <td className="px-3 py-2 text-right">
                {status === "Pending" && (
                  <div className="flex justify-end gap-2">
                    <Button size="sm" onClick={(e) => { e.stopPropagation(); resolveMutation.mutate(r.id); }} loading={resolveMutation.isPending}>Resolve</Button>
                    <Button size="sm" variant="secondary" onClick={(e) => { e.stopPropagation(); dismissMutation.mutate(r.id); }} loading={dismissMutation.isPending}>Dismiss</Button>
                  </div>
                )}
              </td>
            </AdminRow>
          ))}
        </AdminTable>
      )}

      {totalPages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-2">
          <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Prev</button>
          <span className="text-sm text-fg-muted">{page} / {totalPages}</span>
          <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Next</button>
        </div>
      )}
    </div>
  );
}
