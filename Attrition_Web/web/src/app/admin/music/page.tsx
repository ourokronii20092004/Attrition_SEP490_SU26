'use client';
import { useState, useEffect } from 'react';
import Link from 'next/link';
import Modal from '@/components/Modal';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';

export default function AdminMusicPage() {
  const { showToast } = useToast();
  const [albums, setAlbums] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [editModal, setEditModal] = useState<any>(null);
  const [deleteTarget, setDeleteTarget] = useState<any>(null);
  const [formTitle, setFormTitle] = useState('');
  const [formArtist, setFormArtist] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchAlbums = async () => {
    setLoading(true);
    const res = await api.get('/api/music/albums');
    const data = Array.isArray(res) ? res : (res.items || res.data || []);
    setAlbums(data);
    setLoading(false);
  };

  useEffect(() => { fetchAlbums(); }, []);

  const openCreateModal = () => {
    setFormTitle('');
    setFormArtist('');
    setFormDescription('');
    setCoverFile(null);
    setEditModal({ isNew: true });
  };

  const openEditModal = (album: any) => {
    setFormTitle(album.title);
    setFormArtist(album.artist || '');
    setFormDescription(album.description || '');
    setCoverFile(null);
    setEditModal(album);
  };

  const handleSave = async () => {
    if (!formTitle.trim()) {
      showToast('Title is required', 'error');
      return;
    }

    setIsSubmitting(true);
    const payload = { title: formTitle, artist: formArtist, description: formDescription };

    let res;
    if (editModal?.isNew) {
      res = await api.post('/api/music/albums', payload);
    } else {
      res = await api.put(`/api/music/albums/${editModal.id}`, payload);
    }

    if (res.success) {
      // Upload cover if provided
      if (coverFile && (res.data?.id || editModal.id)) {
        const albumId = res.data?.id || editModal.id;
        await api.upload(`/api/music/albums/${albumId}/cover`, coverFile, 'cover');
      }
      showToast(editModal?.isNew ? 'Album created' : 'Album updated', 'success');
      fetchAlbums();
      setEditModal(null);
    } else {
      showToast(res.error || 'Failed to save', 'error');
    }
    setIsSubmitting(false);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const res = await api.delete(`/api/music/albums/${deleteTarget.id}`);
    if (res.success) {
      showToast('Album deleted', 'success');
      fetchAlbums();
    } else {
      showToast(res.error || 'Failed to delete', 'error');
    }
    setDeleteTarget(null);
  };

  return (
    <div>
      <div className="admin-page-header">
        <h1>🎵 Music Albums</h1>
        <button className="btn btn-primary btn-sm" onClick={openCreateModal}>
          + New Album
        </button>
      </div>

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Title</th>
              <th>Artist</th>
              <th>Tracks</th>
              <th>Created</th>
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
            ) : albums.length === 0 ? (
              <tr>
                <td colSpan={5} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  No albums yet
                </td>
              </tr>
            ) : (
              albums.map((album) => (
                <tr key={album.id}>
                  <td><strong>{album.title}</strong></td>
                  <td className="text-muted">{album.artist || '—'}</td>
                  <td>{album.trackCount || 0}</td>
                  <td className="text-muted">{new Date(album.createdAt).toLocaleDateString()}</td>
                  <td>
                    <div className="actions">
                      <button className="btn btn-sm btn-ghost" onClick={() => openEditModal(album)}>
                        Edit
                      </button>
                      <Link href={`/admin/music/tracks?album=${album.id}`} className="btn btn-sm btn-soul">
                        Tracks
                      </Link>
                      <button className="btn btn-sm btn-danger" onClick={() => setDeleteTarget(album)}>
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
        title={editModal?.isNew ? 'Create Album' : 'Edit Album'}
      >
        <div className="auth-form">
          <div className="input-group">
            <label>Title</label>
            <input className="input" value={formTitle} onChange={(e) => setFormTitle(e.target.value)} placeholder="Album title" />
          </div>
          <div className="input-group">
            <label>Artist</label>
            <input className="input" value={formArtist} onChange={(e) => setFormArtist(e.target.value)} placeholder="Artist name" />
          </div>
          <div className="input-group">
            <label>Description</label>
            <textarea className="input" value={formDescription} onChange={(e) => setFormDescription(e.target.value)} placeholder="Album description" rows={3} />
          </div>
          <div className="input-group">
            <label>Cover Image</label>
            <input type="file" accept="image/*" className="input" onChange={(e) => setCoverFile(e.target.files?.[0] || null)} />
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
        title="Delete Album"
      >
        <p className="text-muted mb-lg">
          Are you sure you want to delete <strong>&quot;{deleteTarget?.title}&quot;</strong>? All tracks in this album will also be deleted.
        </p>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setDeleteTarget(null)}>Cancel</button>
          <button className="btn btn-danger" onClick={handleDelete}>Delete</button>
        </div>
      </Modal>
    </div>
  );
}
