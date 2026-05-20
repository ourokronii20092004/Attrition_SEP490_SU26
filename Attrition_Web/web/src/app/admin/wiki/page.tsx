'use client';
import { useState, useEffect } from 'react';
import Link from 'next/link';
import Modal from '@/components/Modal';
import Pagination from '@/components/Pagination';
import SearchBar from '@/components/SearchBar';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';

export default function AdminWikiPage() {
  const { showToast } = useToast();
  const [articles, setArticles] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<any>(null);

  const fetchArticles = async (p = page, q = search) => {
    setLoading(true);
    const params = new URLSearchParams({ page: p.toString(), pageSize: '15' });
    if (q) params.append('search', q);
    const res = await api.get(`/api/wiki/articles?${params}`);
    if (res?.items) {
      setArticles(res.items);
      setTotalPages(Math.ceil((res.totalCount || 0) / 15));
    }
    setLoading(false);
  };

  useEffect(() => { fetchArticles(); }, [page]);

  const handleSearch = (query: string) => {
    setSearch(query);
    setPage(1);
    fetchArticles(1, query);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const res = await api.delete(`/api/wiki/articles/${deleteTarget.id}`);
    if (res.success) {
      showToast('Article deleted', 'success');
      fetchArticles();
    } else {
      showToast(res.error || 'Failed to delete', 'error');
    }
    setDeleteTarget(null);
  };

  return (
    <div>
      <div className="admin-page-header">
        <h1>📖 Wiki Articles</h1>
        <div style={{ display: 'flex', gap: 'var(--space-md)', alignItems: 'center' }}>
          <SearchBar placeholder="Search articles..." onSearch={handleSearch} />
          <Link href="/admin/wiki/new" className="btn btn-primary btn-sm">
            + New Article
          </Link>
        </div>
      </div>

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Title</th>
              <th>Category</th>
              <th>Author</th>
              <th>Updated</th>
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
            ) : articles.length === 0 ? (
              <tr>
                <td colSpan={6} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  No articles found
                </td>
              </tr>
            ) : (
              articles.map((article) => (
                <tr key={article.id}>
                  <td><strong>{article.title}</strong></td>
                  <td className="text-muted">{article.categoryName || article.categorySlug}</td>
                  <td className="text-muted">{article.authorName || '—'}</td>
                  <td className="text-muted">{new Date(article.updatedAt).toLocaleDateString()}</td>
                  <td>
                    <span className={`badge ${article.status === 'Published' ? 'badge-success' : 'badge-warning'}`}>
                      {article.status}
                    </span>
                  </td>
                  <td>
                    <div className="actions">
                      <Link href={`/admin/wiki/edit/${article.id}`} className="btn btn-sm btn-ghost">
                        Edit
                      </Link>
                      <button
                        className="btn btn-sm btn-danger"
                        onClick={() => setDeleteTarget(article)}
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
        title="Delete Article"
      >
        <p className="text-muted mb-lg">
          Are you sure you want to delete <strong>&quot;{deleteTarget?.title}&quot;</strong>? This action cannot be undone.
        </p>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setDeleteTarget(null)}>Cancel</button>
          <button className="btn btn-danger" onClick={handleDelete}>Delete</button>
        </div>
      </Modal>
    </div>
  );
}
