"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { Eye, Ban, ShieldCheck, Trash2 } from "lucide-react";
import { adminApi } from "@/lib/api/admin";
import { useAuth, useConfirm, useToast } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { IconButton } from "@/components/ui/icon-button";
import { PageLoader } from "@/components/ui/spinner";
import { AdminPageHeader, AdminFilterBar, AdminTable, AdminRow } from "@/components/admin/admin-table";
import { Pagination } from "@/components/ui/pagination";
import { formatDate } from "@/lib/format-date";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import { qk } from "@/lib/query-keys";
import type { UserListItem } from "@/lib/types";

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
  const confirm = useConfirm();
  const { toast } = useToast();
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [sort, setSort] = useState("newest");
  const [status, setStatus] = useState("all");
  const search = useDebouncedValue(searchInput.trim(), 300);

  const { data: users, isPending: loading } = useQuery({
    queryKey: qk.admin.users({ page, search, sort, status }),
    enabled: me?.role === "Admin",
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const res = await adminApi.getUsers({ page, pageSize: 20, search: search || undefined, sort, status });
      return res.success ? res.data : null;
    },
  });

  const totalPages = users ? Math.ceil(users.totalCount / users.pageSize) : 0;

  const toggleBanMutation = useMutation({
    mutationFn: (userId: string) => adminApi.toggleBan(userId),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: qk.admin.users() }); toast("Ban status updated.", "success"); },
    onError: () => toast("Failed to update ban status.", "error"),
  });

  const roleMutation = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: string }) => adminApi.setUserRole(userId, role),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: qk.admin.users() }),
  });

  const deleteMutation = useMutation({
    mutationFn: (userId: string) => adminApi.deleteUser(userId),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: qk.admin.users() }); toast("User deleted.", "success"); },
    onError: () => toast("Failed to delete user.", "error"),
  });

  if (!me || me.role !== "Admin") return null;

  const onToggleBan = async (u: UserListItem) => {
    const ok = await confirm({
      title: u.isBanned ? "Unban user?" : "Ban user?",
      message: u.isBanned ? `Restore access for @${u.username}?` : `Ban @${u.username}? They'll be signed out and blocked from signing in.`,
      confirmLabel: u.isBanned ? "Unban" : "Ban",
      danger: !u.isBanned,
    });
    if (ok) toggleBanMutation.mutate(u.id);
  };

  const onDelete = async (u: UserListItem) => {
    const ok = await confirm({
      title: "Delete user?",
      message: `Permanently delete @${u.username}? This cannot be undone.`,
      confirmLabel: "Delete",
      danger: true,
    });
    if (ok) deleteMutation.mutate(u.id);
  };

  // Status filter is sent to the backend; rows are already filtered server-side.
  const rows = users?.items ?? [];

  return (
    <div>
      <AdminPageHeader title="Users" />
      <AdminFilterBar
        search={searchInput}
        onSearch={(v) => { setSearchInput(v); setPage(1); }}
        searchPlaceholder="Search by username…"
        filters={[
          { value: status, onChange: (v) => { setStatus(v); setPage(1); }, ariaLabel: "Filter by status", options: STATUSES },
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
                <div className="flex items-center justify-end gap-0.5">
                  <IconButton
                    label="View user"
                    size="sm"
                    onClick={(e) => { e.stopPropagation(); router.push(`/admin/users/${u.id}`); }}
                  >
                    <Eye size={15} />
                  </IconButton>
                  {u.id !== me.id && !u.isDeleted && (
                    <>
                      <IconButton
                        label={u.isBanned ? "Unban user" : "Ban user"}
                        size="sm"
                        variant={u.isBanned ? "ghost" : "danger"}
                        onClick={(e) => { e.stopPropagation(); onToggleBan(u); }}
                      >
                        {u.isBanned ? <ShieldCheck size={15} /> : <Ban size={15} />}
                      </IconButton>
                      <IconButton
                        label="Delete user"
                        size="sm"
                        variant="danger"
                        onClick={(e) => { e.stopPropagation(); onDelete(u); }}
                      >
                        <Trash2 size={15} />
                      </IconButton>
                    </>
                  )}
                </div>
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
