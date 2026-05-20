'use client';
import { useState, useEffect } from 'react';
import Link from 'next/link';
import Modal from '@/components/Modal';
import Badge from '@/components/Badge';
import Pagination from '@/components/Pagination';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';

export default function AdminForumPostsPage() {
  const { showToast } = useToast();
  const [posts, setPosts] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [removeModal, setRemoveModal] = useState<any>(null);
  const [removeReason, setRemoveReason] = useState('');

  const fetchPosts = async (p = page) => {
    setLoading(true);
    const res = await api.get(`/api/admin/forum/posts?page=${p}&pageSize=15`);
    if (res?.items) {
      setPosts(res.items);
      setTotalPages(Math.ceil((res.totalCount || 0) / 15));
    } else if (Array.isArray(res)) {
      setPosts(res);
    }
    setLoading(false);
  };

  useEffect(() => { fetchPosts(); }, [page]);

  const handleRemove = async () => {
    if (!removeModal) return;
    const res = await api.put(`/api/admin/forum/posts/${removeModal.id}/remove`, {
      reason: removeReason || 'Violation of community guidelines',
    });
    if (res.success) {
      showToast('Post removed', 'success');
      fetchPosts();
    } else {
      showToast(res.error || 'Failed to remove', 'error');
    }
    setRemoveModal(null);
    setRemoveReason('');
  };

  const handleRestore = async (postId: string) => {
    const res = await api.put(`/api/admin/forum/posts/${postId}/restore`);
    if (res.success) {
      showToast('Post restored', 'success');
      fetchPosts();
    } else {
      showToast(res.error || 'Failed to restore', 'error');
    }
  };

  const truncate = (text: string, max = 80) => {
    if (!text) return '—';
    return text.length > max ? text.substring(0, max) + '...' : text;
  };

  return (
    <div>
      <div className="admin-page-header">
        <h1>📝 Forum Posts</h1>
      </div>

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Content</th>
              <th>Author</th>
              <th>Thread</th>
              <th>Date</th>
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
            ) : posts.length === 0 ? (
              <tr>
                <td colSpan={6} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  No posts found
                </td>
              </tr>
            ) : (
              posts.map((post) => (
                <tr key={post.id}>
                  <td>
                    {post.isRemoved ? (
                      <span className="text-muted" style={{ fontStyle: 'italic' }}>
                        [Removed by moderator]
                      </span>
                    ) : (
                      truncate(post.content)
                    )}
                  </td>
                  <td className="text-muted">{post.authorName}</td>
                  <td>
                    <Link
                      href={`/forum/${post.categorySlug || 'general'}/${post.threadId}`}
                      className="text-ember"
                      style={{ fontSize: '13px' }}
                    >
                      View Thread
                    </Link>
                  </td>
                  <td className="text-muted">{new Date(post.createdAt).toLocaleDateString()}</td>
                  <td>
                    {post.isRemoved ? (
                      <Badge variant="danger">Removed</Badge>
                    ) : (
                      <Badge variant="success">Visible</Badge>
                    )}
                  </td>
                  <td>
                    <div className="actions">
                      {post.isRemoved ? (
                        <button
                          className="btn btn-sm btn-soul"
                          onClick={() => handleRestore(post.id)}
                        >
                          Restore
                        </button>
                      ) : (
                        <button
                          className="btn btn-sm btn-danger"
                          onClick={() => setRemoveModal(post)}
                        >
                          Remove
                        </button>
                      )}
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

      {/* Remove Modal */}
      <Modal
        isOpen={!!removeModal}
        onClose={() => { setRemoveModal(null); setRemoveReason(''); }}
        title="Remove Post"
      >
        <p className="text-muted mb-md">
          Provide a reason for removing this post by <strong>{removeModal?.authorName}</strong>:
        </p>
        <div className="input-group mb-lg">
          <textarea
            className="input"
            value={removeReason}
            onChange={(e) => setRemoveReason(e.target.value)}
            placeholder="Reason for removal..."
            rows={3}
          />
        </div>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => { setRemoveModal(null); setRemoveReason(''); }}>Cancel</button>
          <button className="btn btn-danger" onClick={handleRemove}>Remove Post</button>
        </div>
      </Modal>
    </div>
  );
}
