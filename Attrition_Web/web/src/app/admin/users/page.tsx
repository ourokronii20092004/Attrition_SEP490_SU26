'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import Button from '@/components/Button';
import Pagination from '@/components/Pagination';
import Badge from '@/components/Badge';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter } from 'next/navigation';
import { useToast } from '@/contexts/ToastContext';

export default function AdminUsers() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();

  const [users, setUsers] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [isFetching, setIsFetching] = useState(true);

  useEffect(() => {
    if (!loading && user?.role !== 'Admin') {
      router.push('/');
    }
  }, [user, loading, router]);

  const fetchUsers = async () => {
    setIsFetching(true);
    const res = await api.get(`/api/admin/users?page=${page}`);
    if (res.items) {
      setUsers(res.items);
      setTotal(res.totalCount);
    }
    setIsFetching(false);
  };

  useEffect(() => {
    if (user?.role === 'Admin') fetchUsers();
  }, [page, user]);

  const handleRoleChange = async (userId: string, newRole: string) => {
    const res = await api.put(`/api/admin/users/${userId}/role`, newRole);
    if (res.success) {
      showToast('User role updated', 'success');
      fetchUsers();
    } else {
      showToast(res.error || 'Failed to update role', 'error');
    }
  };

  const handleToggleBan = async (userId: string) => {
    const res = await api.post(`/api/admin/users/${userId}/ban`);
    if (res.success) {
      showToast('User ban status toggled', 'success');
      fetchUsers();
    } else {
      showToast(res.error || 'Failed to toggle ban', 'error');
    }
  };

  const handleResetPassword = async (userId: string) => {
    const newPassword = prompt('Enter new password for this user (must meet complexity requirements):');
    if (!newPassword) return;

    const res = await api.put(`/api/admin/users/${userId}/reset-password`, newPassword);
    if (res.success) {
      showToast('Password reset successfully', 'success');
    } else {
      showToast(res.error || 'Failed to reset password', 'error');
    }
  };

  if (loading || user?.role !== 'Admin') return null;

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Admin', href: '/admin' },
        { label: 'Manage Users' }
      ]} />

      <GlassCard style={{ overflowX: 'auto' }}>
        <h1 style={{ marginBottom: 'var(--space-xl)' }}>Manage Users</h1>

        {isFetching ? (
          <div>Loading...</div>
        ) : (
          <>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', minWidth: '800px' }}>
              <thead>
                <tr style={{ borderBottom: '1px solid var(--border)' }}>
                  <th style={{ padding: 'var(--space-sm)' }}>Username</th>
                  <th style={{ padding: 'var(--space-sm)' }}>Role</th>
                  <th style={{ padding: 'var(--space-sm)' }}>Status</th>
                  <th style={{ padding: 'var(--space-sm)' }}>Joined</th>
                  <th style={{ padding: 'var(--space-sm)', textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {users.map(u => (
                  <tr key={u.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: 'var(--space-sm)' }}>{u.username}</td>
                    <td style={{ padding: 'var(--space-sm)' }}>
                      <select 
                        value={u.role} 
                        onChange={(e) => handleRoleChange(u.id, e.target.value)}
                        className="input"
                        style={{ padding: '4px 8px', width: 'auto' }}
                      >
                        <option value="User">User</option>
                        <option value="Admin">Admin</option>
                      </select>
                    </td>
                    <td style={{ padding: 'var(--space-sm)' }}>
                      <Badge variant={u.isBanned ? 'danger' : 'success'}>
                        {u.isBanned ? 'Banned' : 'Active'}
                      </Badge>
                    </td>
                    <td style={{ padding: 'var(--space-sm)' }}>{new Date(u.joinedAt).toLocaleDateString()}</td>
                    <td style={{ padding: 'var(--space-sm)', textAlign: 'right' }}>
                      <div style={{ display: 'flex', gap: 'var(--space-sm)', justifyContent: 'flex-end' }}>
                        <Button size="sm" variant={u.isBanned ? "primary" : "danger"} onClick={() => handleToggleBan(u.id)}>
                          {u.isBanned ? 'Unban' : 'Ban'}
                        </Button>
                        <Button size="sm" variant="secondary" onClick={() => handleResetPassword(u.id)}>
                          Reset Password
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            
            <Pagination currentPage={page} totalPages={Math.ceil(total / 20)} onPageChange={setPage} />
          </>
        )}
      </GlassCard>
    </div>
  );
}