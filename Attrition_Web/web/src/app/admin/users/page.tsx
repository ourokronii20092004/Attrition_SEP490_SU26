"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, cn, debounce } from "@/lib/utils";
import { Trash2 } from "lucide-react";
import styles from "../admin.module.css";

interface UserRow {
  id: string;
  username: string;
  email: string | null;
  role: string;
  authProvider: string;
  isBanned: boolean;
  joinedAt: string;
}

export default function AdminUsersPage() {
  const toast = useToast();
  const { user: currentUser } = useAuth();
  const [users, setUsers] = useState<UserRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 20;

  const fetchUsers = useCallback(
    async (searchTerm: string, pageNum: number) => {
      setLoading(true);
      try {
        const res = await api.get<UserRow[]>(
          "/admin/users",
          { search: searchTerm, page: pageNum, pageSize }
        );
        if (res.success) {
          const list = Array.isArray(res.data) ? res.data : [];
          setUsers(list);
          setTotalCount(res.totalCount ?? list.length);
        }
      } catch {
        toast.error("Failed to load users");
      } finally {
        setLoading(false);
      }
    },
    [toast]
  );

  useEffect(() => {
    fetchUsers(search, page);
  }, [page, fetchUsers, search]);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const debouncedSearch = useCallback(
    debounce((term: string) => {
      setPage(1);
      setSearch(term);
    }, 300),
    []
  );

  const handleRoleChange = async (userId: string, newRole: string) => {
    try {
      await api.put(`/admin/users/${userId}/role`, { role: newRole });
      setUsers((prev) =>
        prev.map((u) => (u.id === userId ? { ...u, role: newRole } : u))
      );
      toast.success("Role updated");
    } catch {
      toast.error("Failed to update role");
    }
  };

  // Ban is a TOGGLE — single endpoint that flips isBanned
  const handleBanToggle = async (userId: string, currentlyBanned: boolean) => {
    try {
      await api.post(`/admin/users/${userId}/ban`);
      setUsers((prev) =>
        prev.map((u) =>
          u.id === userId ? { ...u, isBanned: !currentlyBanned } : u
        )
      );
      toast.success(currentlyBanned ? "User unbanned" : "User banned");
    } catch {
      toast.error("Failed to update ban status");
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  // Check if a user can be banned (not self, not admin)
  const canBan = (user: UserRow) => {
    if (user.id === currentUser?.id) return false; // can't ban self
    if (user.role === "Admin") return false; // can't ban admins
    return true;
  };

  const canDelete = (user: UserRow) => user.id !== currentUser?.id;

  const handleDelete = async (userId: string, username: string) => {
    if (!confirm(`Permanently delete user "${username}"? This will remove all their posts, threads, and contributions. This cannot be undone.`)) return;
    try {
      await api.delete(`/admin/users/${userId}`);
      setUsers((prev) => prev.filter((u) => u.id !== userId));
      setTotalCount((c) => c - 1);
      toast.success(`User "${username}" deleted`);
    } catch {
      toast.error("Failed to delete user");
    }
  };

  return (
    <div>
      <h1 style={{ marginBottom: "var(--space-6)" }}>Users</h1>

      <div className={styles.adminTableWrapper}>
        <div className={styles.adminTableHeader}>
          <input
            type="text"
            className="input"
            placeholder="Search users..."
            onChange={(e) => debouncedSearch(e.target.value)}
            style={{ maxWidth: 300 }}
          />
          <span className="text-sm text-muted">{totalCount} users</span>
        </div>

        <table className="table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Email</th>
              <th>Role</th>
              <th>Provider</th>
              <th>Status</th>
              <th>Joined</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <tr key={i}>
                  {Array.from({ length: 7 }).map((_, j) => (
                    <td key={j}><div className="skeleton skeleton-text" /></td>
                  ))}
                </tr>
              ))
            ) : users.length > 0 ? (
              users.map((user) => (
                <tr key={user.id}>
                  <td><strong>{user.username}</strong></td>
                  <td>{user.email || "—"}</td>
                  <td>
                    <select
                      value={user.role}
                      onChange={(e) => handleRoleChange(user.id, e.target.value)}
                      className="input"
                      disabled={user.id === currentUser?.id}
                      style={{ height: 32, fontSize: "var(--text-xs)", padding: "0 var(--space-2)", width: 100 }}
                    >
                      <option value="User">User</option>
                      <option value="Admin">Admin</option>
                    </select>
                  </td>
                  <td>
                    <span className="badge badge-default">{user.authProvider || "local"}</span>
                  </td>
                  <td>
                    {user.isBanned ? (
                      <span className="badge badge-danger badge-dot">Banned</span>
                    ) : (
                      <span className="badge badge-success badge-dot">Active</span>
                    )}
                  </td>
                  <td>{formatDate(user.joinedAt)}</td>
                  <td>
                    {canBan(user) ? (
                      <button
                        className={cn("btn btn-sm", user.isBanned ? "btn-secondary" : "btn-danger")}
                        onClick={() => handleBanToggle(user.id, user.isBanned)}
                        style={{ fontSize: "var(--text-xs)" }}
                      >
                        {user.isBanned ? "Unban" : "Ban"}
                      </button>
                    ) : (
                      <span style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>—</span>
                    )}
                    {canDelete(user) && (
                      <button
                        className="btn btn-sm"
                        onClick={() => handleDelete(user.id, user.username)}
                        style={{ fontSize: "var(--text-xs)", color: "var(--danger)", background: "transparent", border: "1px solid var(--border)", marginLeft: 4 }}
                        title="Delete user"
                      >
                        <Trash2 size={13} />
                      </button>
                    )}
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={7} style={{ textAlign: "center", padding: "var(--space-8)" }}>
                  No users found
                </td>
              </tr>
            )}
          </tbody>
        </table>

        {totalPages > 1 && (
          <div className={styles.adminTableFooter}>
            <span>Page {page} of {totalPages}</span>
            <div className="pagination">
              <button className="pagination-btn" disabled={page === 1} onClick={() => setPage(page - 1)}>←</button>
              <button className="pagination-btn" disabled={page === totalPages} onClick={() => setPage(page + 1)}>→</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
