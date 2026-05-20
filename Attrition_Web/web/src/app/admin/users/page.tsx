'use client';
import { useState, useEffect } from 'react';
import SearchBar from '@/components/SearchBar';
import Pagination from '@/components/Pagination';
import Badge from '@/components/Badge';
import Modal from '@/components/Modal';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';

export default function AdminUsersPage() {
  const { showToast } = useToast();
  const [users, setUsers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [search, setSearch] = useState('');
  const [resetModal, setResetModal] = useState<any>(null);

  const fetchUsers = async (p = page, q = search) => {
    setLoading(true);
    const params = new URLSearchParams({ page: p.toString(), pageSize: '15' });
    if (q) params.append('search', q);

    const res = await api.get(`/api/admin/users?${params}`);
    if (res?.items) {
      setUsers(res.items);
      setTotalPages(Math.ceil((res.totalCount || 0) / 15));
    }
    setLoading(false);
  };

  useEffect(() => { fetchUsers(); }, [page]);

  const handleSearch = (query: string) => {
    setSearch(query);
    setPage(1);
    fetchUsers(1, query);
  };

  const handleRoleChange = async (userId: string, newRole: string) => {
    const res = await api.put(`/api/admin/users/${userId}/role`, { role: newRole });
    if (res.success) {
      showToast('Role updated', 'success');
      fetchUsers();
    } else {
      showToast(res.error || 'Failed to update role', 'error');
    }
  };

  const handleBanToggle = async (userId: string, isBanned: boolean) => {
    const endpoint = isBanned
      ? `/api/admin/users/${userId}/unban`
      : `/api/admin/users/${userId}/ban`;
    const res = await api.post(endpoint);
    if (res.success) {
      showToast(isBanned ? 'User unbanned' : 'User banned', 'success');
      fetchUsers();
    } else {
      showToast(res.error || 'Action failed', 'error');
    }
  };

  const handleResetPassword = async () => {
    if (!resetModal) return;
    const res = await api.post(`/api/admin/users/${resetModal.id}/reset-password`);
    if (res.success) {
      showToast('Password reset successfully', 'success');
    } else {
      showToast(res.error || 'Failed to reset password', 'error');
    }
    setResetModal(null);
  };

  return (
    <div>
      <div className="admin-page-header">
        <h1>👥 User Management</h1>
        <SearchBar placeholder="Search users..." onSearch={handleSearch} />
      </div>

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Email</th>
              <th>Role</th>
              <th>Joined</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={6} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  <div className="spinner" style={{ margin: '0 auto' }} />
                </td>
              </tr>
            ) : users.length === 0 ? (
              <tr>
                <td colSpan={6} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  No users found
                </td>
              </tr>
            ) : (
              users.map((u) => (
                <tr key={u.id}>
                  <td>
                    <strong>{u.username}</strong>
                  </td>
                  <td className="text-muted">{u.email || '—'}</td>
                  <td>
                    <select
                      className="input btn-sm"
                      value={u.role}
                      onChange={(e) => handleRoleChange(u.id, e.target.value)}
                      style={{ width: 'auto', padding: '4px 8px', fontSize: '12px' }}
                    >
                      <option value="User">User</option>
                      <option value="Moderator">Moderator</option>
                      <option value="Admin">Admin</option>
                    </select>
                  </td>
                  <td className="text-muted">{new Date(u.createdAt).toLocaleDateString()}</td>
                  <td>
                    {u.isBanned ? (
                      <Badge variant="danger">Banned</Badge>
                    ) : (
                      <Badge variant="success">Active</Badge>
                    )}
                  </td>
                  <td>
                    <div className="actions">
                      <button
                        className={`btn btn-sm ${u.isBanned ? 'btn-soul' : 'btn-danger'}`}
                        onClick={() => handleBanToggle(u.id, u.isBanned)}
                      >
                        {u.isBanned ? 'Unban' : 'Ban'}
                      </button>
                      <button
                        className="btn btn-sm btn-ghost"
                        onClick={() => setResetModal(u)}
                      >
                        Reset PW
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="mt-lg">
        <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} />
      </div>

      {/* Reset Password Modal */}
      <Modal
        isOpen={!!resetModal}
        onClose={() => setResetModal(null)}
        title="Reset Password"
      >
        <p className="text-muted mb-lg">
          Are you sure you want to reset the password for <strong>{resetModal?.username}</strong>?
          They will receive a temporary password.
        </p>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setResetModal(null)}>Cancel</button>
          <button className="btn btn-danger" onClick={handleResetPassword}>Reset Password</button>
        </div>
      </Modal>
    </div>
  );
}