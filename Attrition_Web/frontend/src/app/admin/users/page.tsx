"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { adminApi } from "@/lib/api/admin";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { formatDate } from "@/lib/format-date";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { qk } from "@/lib/query-keys";

const SORTS = [
  { value: "newest", label: "Newest" },
  { value: "oldest", label: "Oldest" },
  { value: "username", label: "Name (A–Z)" },
];
const STATUSES = [
  { value: "all", label: "All statuses" },
  { value: "active", label: "Active" },
  { value: "banned", label: "Banned" },
  { value: "deleted", label: "Deleted" },
];

export default function AdminUsersPage() {
  const { user: me } = useAuth();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [sort, setSort] = useState("newest");
  const [status, setStatus] = useState("all");
  const search = useDebouncedValue(searchInput.trim(), 300);

  const { data: users, isPending: loading } = useQuery({
    queryKey: qk.admin.users({ page, search, sort }),
    enabled: me?.role === "Admin",
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const res = await adminApi.getUsers({ page, pageSize: 20, search: search || undefined, sort });
      return res.success ? res.data : null;
    },
  });

  const totalPages = users ? Math.ceil(users.totalCount / users.pageSize) : 0;

  const toggleBanMutation = useMutation({
    mutationFn: (userId: string) => adminApi.toggleBan(userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: qk.admin.users() }),
  });

  const roleMutation = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: string }) => adminApi.setUserRole(userId, role),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: qk.admin.users() }),
  });

  if (!me || me.role !== "Admin") return null;

  // Status filter is applied client-side over the current page (backend filters by search+sort).
  const rows = (users?.items ?? []).filter((u) => {
    if (status === "active") return !u.isBanned && !u.isDeleted;
    if (status === "banned") return u.isBanned;
    if (status === "deleted") return u.isDeleted;
    return true;
  });

  return (
    <div>
      <AdminPageHeader title="Users" />
      <AdminFilterBar
        search={searchInput}
        onSearch={(v) => { setSearchInput(v); setPage(1); }}
        searchPlaceholder="Search by username…"
        filters={[
          { value: status, onChange: setStatus, ariaLabel: "Filter by status", options: STATUSES },
          { value: sort, onChange: (v) => { setSort(v); setPage(1); }, ariaLabel: "Sort", options: SORTS },
        ]}
      />

      {loading && !users ? (
        <PageLoader />
      ) : (
        <AdminTable
          columns={[
            { key: "username", label: "Username" },
            { key: "role", label: "Role" },
            { key: "status", label: "Status" },
            { key: "joined", label: "Joined" },
            { key: "actions", label: "Actions", align: "right" },
          ]}
          empty={rows.length === 0}
        >
          {rows.map((u) => (
            <AdminRow key={u.id} onClick={() => router.push(`/admin/users/${u.id}`)}>
              <td className="px-3 py-2 font-medium text-fg">{u.username}</td>
              <td className="px-3 py-2">
                <select
                  value={u.role}
                  onClick={(e) => e.stopPropagation()}
                  onChange={(e) => { e.stopPropagation(); roleMutation.mutate({ userId: u.id, role: e.target.value }); }}
                  className="rounded border border-border bg-surface-2 px-1.5 py-0.5 text-xs text-fg disabled:opacity-50"
                  disabled={u.id === me.id || u.isDeleted}
                >
                  <option value="User">User</option>
                  <option value="Admin">Admin</option>
                </select>
              </td>
              <td className="px-3 py-2">
                {u.isDeleted ? (
                  <span className="text-xs text-fg-subtle">Deleted</span>
                ) : u.isBanned ? (
                  <span className="text-xs font-medium text-danger">Banned</span>
                ) : (
                  <span className="text-xs text-success">Active</span>
                )}
              </td>
              <td className="px-3 py-2 text-fg-muted">{formatDate(u.joinedAt)}</td>
              <td className="px-3 py-2 text-right">
                {u.id !== me.id && !u.isDeleted && (
                  <Button
                    variant={u.isBanned ? "secondary" : "danger"}
                    size="sm"
                    onClick={(e) => { e.stopPropagation(); toggleBanMutation.mutate(u.id); }}
                  >
                    {u.isBanned ? "Unban" : "Ban"}
                  </Button>
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
