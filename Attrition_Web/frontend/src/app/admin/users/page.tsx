"use client";

import { useEffect, useState } from "react";
import { adminApi } from "@/lib/api/admin";
import { useAuth } from "@/lib/providers";
import { Button } from "@/components/ui/button";
import { PageLoader } from "@/components/ui/spinner";
import { formatDate } from "@/lib/format-date";
import type { UserListItem, PaginatedResponse } from "@/lib/types";

export default function AdminUsersPage() {
  const { user: me } = useAuth();
  const [users, setUsers] = useState<PaginatedResponse<UserListItem> | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const totalPages = users ? Math.ceil(users.totalCount / users.pageSize) : 0;

  const fetchUsers = () => {
    setLoading(true);
    adminApi
      .getUsers({ page, pageSize: 20 })
      .then((res) => {
        if (res.success) setUsers(res.data);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (me?.role !== "Admin") return;
    fetchUsers();
  }, [me, page]);

  const handleToggleBan = async (userId: string) => {
    await adminApi.toggleBan(userId);
    fetchUsers();
  };

  const handleRoleChange = async (userId: string, role: string) => {
    await adminApi.setUserRole(userId, role);
    fetchUsers();
  };

  if (!me || me.role !== "Admin") return null;
  if (loading && !users) return <PageLoader />;

  return (
    <div className="mx-auto max-w-6xl">
      <h1 className="font-display text-3xl font-bold text-fg">User Management</h1>

      <div className="mt-6 overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border text-left text-fg-muted">
              <th className="pb-2 pr-4">Username</th>
              <th className="pb-2 pr-4">Role</th>
              <th className="pb-2 pr-4">Status</th>
              <th className="pb-2 pr-4">Joined</th>
              <th className="pb-2">Actions</th>
            </tr>
          </thead>
          <tbody>
            {users?.items.map((u) => (
              <tr key={u.id} className="border-b border-border/50">
                <td className="py-3 pr-4 text-fg">{u.username}</td>
                <td className="py-3 pr-4">
                  <select
                    value={u.role}
                    onChange={(e) => handleRoleChange(u.id, e.target.value)}
                    className="rounded border border-border bg-surface-2 px-2 py-1 text-xs text-fg"
                    disabled={u.id === me.id}
                  >
                    <option value="User">User</option>
                    <option value="Admin">Admin</option>
                  </select>
                </td>
                <td className="py-3 pr-4">
                  {u.isBanned ? (
                    <span className="text-xs text-danger">Banned</span>
                  ) : (
                    <span className="text-xs text-success">Active</span>
                  )}
                </td>
                <td className="py-3 pr-4 text-fg-muted">{formatDate(u.joinedAt)}</td>
                <td className="py-3">
                  {u.id !== me.id && (
                    <Button
                      variant={u.isBanned ? "secondary" : "danger"}
                      size="sm"
                      onClick={() => handleToggleBan(u.id)}
                    >
                      {u.isBanned ? "Unban" : "Ban"}
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="mt-6 flex items-center justify-center gap-2">
          <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Prev</button>
          <span className="text-sm text-fg-muted">{page} / {totalPages}</span>
          <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="rounded-md border border-border px-3 py-1 text-sm text-fg-muted disabled:opacity-50">Next</button>
        </div>
      )}
    </div>
  );
}
