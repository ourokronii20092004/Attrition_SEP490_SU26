'use client';
import { useState, useEffect } from 'react';
import Modal from '@/components/Modal';
import Badge from '@/components/Badge';
import Pagination from '@/components/Pagination';
import SearchBar from '@/components/SearchBar';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';

export default function AdminForumPage() {
  const { showToast } = useToast();
  const [threads, setThreads] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<any>(null);

  const fetchThreads = async (p = page, q = search) => {
    setLoading(true);
    const params = new URLSearchParams({ page: p.toString(), pageSize: '15' });
    if (q) params.append('search', q);
    const res = await api.get(`/api/forum/threads?${params}`);
    if (res?.items) {
      setThreads(res.items);
      setTotalPages(Math.ceil((res.totalCount || 0) / 15));
    }
    setLoading(false);
  };

  useEffect(() => { fetchThreads(); }, [page]);

  const handleSearch = (query: string) => {
    setSearch(query);
    setPage(1);
    fetchThreads(1, query);
  };

  const handleTogglePin = async (thread: any) => {
    const res = await api.put(`/api/forum/threads/${thread.id}`, { isPinned: !thread.isPinned });
    if (res.success) {
      showToast(thread.isPinned ? 'Unpinned' : 'Pinned', 'success');
      fetchThreads();
    } else {
      showToast(res.error || 'Failed', 'error');
    }
  };

  const handleToggleLock = async (thread: any) => {
    const res = await api.put(`/api/forum/threads/${thread.id}`, { isLocked: !thread.isLocked });
    if (res.success) {
      showToast(thread.isLocked ? 'Unlocked' : 'Locked', 'success');
      fetchThreads();
    } else {
      showToast(res.error || 'Failed', 'error');
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const res = await api.delete(`/api/forum/threads/${deleteTarget.id}`);
    if (res.success) {
      showToast('Thread deleted', 'success');
      fetchThreads();
    } else {
      showToast(res.error || 'Failed', 'error');
    }
    setDeleteTarget(null);
  };

  return (
    <div>
      <div className="admin-page-header">
        <h1>💬 Forum Threads</h1>
        <SearchBar placeholder="Search threads..." onSearch={handleSearch} />
      </div>

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Title</th>
              <th>Category</th>
              <th>Author</th>
              <th>Replies</th>
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
            ) : threads.length === 0 ? (
              <tr>
                <td colSpan={6} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  No threads found
                </td>
              </tr>
            ) : (
              threads.map((thread) => (
                <tr key={thread.id}>
                  <td><strong>{thread.title}</strong></td>
                  <td className="text-muted">{thread.categoryName || thread.categorySlug}</td>
                  <td className="text-muted">{thread.authorName}</td>
                  <td>{thread.replyCount || 0}</td>
                  <td>
                    <div style={{ display: 'flex', gap: 'var(--space-xs)' }}>
                      {thread.isPinned && <Badge variant="pinned">📌</Badge>}
                      {thread.isLocked && <Badge variant="locked">🔒</Badge>}
                      {!thread.isPinned && !thread.isLocked && <span className="text-ghost">—</span>}
                    </div>
                  </td>
                  <td>
                    <div className="actions">
                      <button
                        className={`btn btn-sm ${thread.isPinned ? 'btn-ghost' : 'btn-gold'}`}
                        onClick={() => handleTogglePin(thread)}
                      >
                        {thread.isPinned ? 'Unpin' : 'Pin'}
                      </button>
                      <button
                        className={`btn btn-sm ${thread.isLocked ? 'btn-ghost' : 'btn-secondary'}`}
                        onClick={() => handleToggleLock(thread)}
                      >
                        {thread.isLocked ? 'Unlock' : 'Lock'}
                      </button>
                      <button
                        className="btn btn-sm btn-danger"
                        onClick={() => setDeleteTarget(thread)}
                      >
                        Delete
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

      <Modal
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title="Delete Thread"
      >
        <p className="text-muted mb-lg">
          Are you sure you want to delete <strong>&quot;{deleteTarget?.title}&quot;</strong>? This will also delete all replies.
        </p>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setDeleteTarget(null)}>Cancel</button>
          <button className="btn btn-danger" onClick={handleDelete}>Delete</button>
        </div>
      </Modal>
    </div>
  );
}
