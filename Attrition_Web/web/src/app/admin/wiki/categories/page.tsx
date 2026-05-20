'use client';
import { useState, useEffect } from 'react';
import Modal from '@/components/Modal';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';

export default function AdminWikiCategoriesPage() {
  const { showToast } = useToast();
  const [categories, setCategories] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [editModal, setEditModal] = useState<any>(null);
  const [deleteTarget, setDeleteTarget] = useState<any>(null);
  const [formName, setFormName] = useState('');
  const [formSlug, setFormSlug] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchCategories = async () => {
    setLoading(true);
    const res = await api.get('/api/wiki/categories');
    const cats = Array.isArray(res) ? res : (res.data || []);
    setCategories(cats);
    setLoading(false);
  };

  useEffect(() => { fetchCategories(); }, []);

  const openCreateModal = () => {
    setFormName('');
    setFormSlug('');
    setFormDescription('');
    setEditModal({ isNew: true });
  };

  const openEditModal = (cat: any) => {
    setFormName(cat.name);
    setFormSlug(cat.slug);
    setFormDescription(cat.description || '');
    setEditModal(cat);
  };

  const handleSave = async () => {
    if (!formName.trim() || !formSlug.trim()) {
      showToast('Name and slug are required', 'error');
      return;
    }

    setIsSubmitting(true);
    const payload = { name: formName, slug: formSlug, description: formDescription };

    let res;
    if (editModal?.isNew) {
      res = await api.post('/api/wiki/categories', payload);
    } else {
      res = await api.put(`/api/wiki/categories/${editModal.id}`, payload);
    }

    setIsSubmitting(false);

    if (res.success) {
      showToast(editModal?.isNew ? 'Category created' : 'Category updated', 'success');
      fetchCategories();
      setEditModal(null);
    } else {
      showToast(res.error || 'Failed to save', 'error');
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const res = await api.delete(`/api/wiki/categories/${deleteTarget.id}`);
    if (res.success) {
      showToast('Category deleted', 'success');
      fetchCategories();
    } else {
      showToast(res.error || 'Failed to delete', 'error');
    }
    setDeleteTarget(null);
  };

  return (
    <div>
      <div className="admin-page-header">
        <h1>🏷️ Wiki Categories</h1>
        <button className="btn btn-primary btn-sm" onClick={openCreateModal}>
          + New Category
        </button>
      </div>

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Slug</th>
              <th>Description</th>
              <th>Articles</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={5} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  <div className="spinner" style={{ margin: '0 auto' }} />
                </td>
              </tr>
            ) : categories.length === 0 ? (
              <tr>
                <td colSpan={5} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  No categories yet
                </td>
              </tr>
            ) : (
              categories.map((cat) => (
                <tr key={cat.id}>
                  <td><strong>{cat.name}</strong></td>
                  <td className="text-muted">{cat.slug}</td>
                  <td className="text-muted">{cat.description || '—'}</td>
                  <td>{cat.articleCount || 0}</td>
                  <td>
                    <div className="actions">
                      <button className="btn btn-sm btn-ghost" onClick={() => openEditModal(cat)}>
                        Edit
                      </button>
                      <button className="btn btn-sm btn-danger" onClick={() => setDeleteTarget(cat)}>
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

      {/* Create/Edit Modal */}
      <Modal
        isOpen={!!editModal}
        onClose={() => setEditModal(null)}
        title={editModal?.isNew ? 'Create Category' : 'Edit Category'}
      >
        <div className="auth-form">
          <div className="input-group">
            <label>Name</label>
            <input className="input" value={formName} onChange={(e) => setFormName(e.target.value)} placeholder="Category name" />
          </div>
          <div className="input-group">
            <label>Slug</label>
            <input className="input" value={formSlug} onChange={(e) => setFormSlug(e.target.value)} placeholder="category-slug" />
          </div>
          <div className="input-group">
            <label>Description</label>
            <textarea className="input" value={formDescription} onChange={(e) => setFormDescription(e.target.value)} placeholder="Optional description" rows={3} />
          </div>
        </div>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setEditModal(null)}>Cancel</button>
          <button className="btn btn-primary" onClick={handleSave} disabled={isSubmitting}>
            {isSubmitting ? 'Saving...' : 'Save'}
          </button>
        </div>
      </Modal>

      {/* Delete Modal */}
      <Modal
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title="Delete Category"
      >
        <p className="text-muted mb-lg">
          Are you sure you want to delete <strong>&quot;{deleteTarget?.name}&quot;</strong>? All articles in this category may be affected.
        </p>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setDeleteTarget(null)}>Cancel</button>
          <button className="btn btn-danger" onClick={handleDelete}>Delete</button>
        </div>
      </Modal>
    </div>
  );
}
