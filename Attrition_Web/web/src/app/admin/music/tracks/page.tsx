'use client';
import { useState, useEffect, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import Modal from '@/components/Modal';
import Pagination from '@/components/Pagination';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';

function TracksContent() {
  const searchParams = useSearchParams();
  const albumFilter = searchParams.get('album') || '';
  const { showToast } = useToast();
  const [tracks, setTracks] = useState<any[]>([]);
  const [albums, setAlbums] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [uploadModal, setUploadModal] = useState(false);
  const [editModal, setEditModal] = useState<any>(null);
  const [deleteTarget, setDeleteTarget] = useState<any>(null);

  // Upload form
  const [uploadAlbumId, setUploadAlbumId] = useState(albumFilter);
  const [uploadTitle, setUploadTitle] = useState('');
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Edit form
  const [editTitle, setEditTitle] = useState('');
  const [editAlbumId, setEditAlbumId] = useState('');

  const fetchTracks = async (p = page) => {
    setLoading(true);
    const params = new URLSearchParams({ page: p.toString(), pageSize: '15' });
    if (albumFilter) params.append('albumId', albumFilter);
    const res = await api.get(`/api/music/tracks?${params}`);
    if (res?.items) {
      setTracks(res.items);
      setTotalPages(Math.ceil((res.totalCount || 0) / 15));
    } else if (Array.isArray(res)) {
      setTracks(res);
    }
    setLoading(false);
  };

  const fetchAlbums = async () => {
    const res = await api.get('/api/music/albums');
    const data = Array.isArray(res) ? res : (res.items || res.data || []);
    setAlbums(data);
  };

  useEffect(() => { fetchAlbums(); }, []);
  useEffect(() => { fetchTracks(); }, [page, albumFilter]);

  const formatDuration = (seconds: number) => {
    if (!seconds) return '—';
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  };

  const formatSize = (bytes: number) => {
    if (!bytes) return '—';
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const handleUpload = async () => {
    if (!uploadFile || !uploadTitle.trim() || !uploadAlbumId) {
      showToast('Please fill in all fields and select a file', 'error');
      return;
    }

    setIsSubmitting(true);

    // First create the track metadata
    const res = await api.post('/api/music/tracks', {
      title: uploadTitle,
      albumId: parseInt(uploadAlbumId),
    });

    if (res.success) {
      // Then upload the audio file
      const trackId = res.data?.id || res.data;
      if (trackId && uploadFile) {
        await api.upload(`/api/music/tracks/${trackId}/audio`, uploadFile, 'audio');
      }
      showToast('Track uploaded!', 'success');
      fetchTracks();
      setUploadModal(false);
      setUploadTitle('');
      setUploadFile(null);
    } else {
      showToast(res.error || 'Failed to upload', 'error');
    }
    setIsSubmitting(false);
  };

  const handleEdit = async () => {
    if (!editModal) return;
    setIsSubmitting(true);
    const res = await api.put(`/api/music/tracks/${editModal.id}`, {
      title: editTitle,
      albumId: editAlbumId ? parseInt(editAlbumId) : undefined,
    });
    if (res.success) {
      showToast('Track updated', 'success');
      fetchTracks();
    } else {
      showToast(res.error || 'Failed to update', 'error');
    }
    setEditModal(null);
    setIsSubmitting(false);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const res = await api.delete(`/api/music/tracks/${deleteTarget.id}`);
    if (res.success) {
      showToast('Track deleted', 'success');
      fetchTracks();
    } else {
      showToast(res.error || 'Failed to delete', 'error');
    }
    setDeleteTarget(null);
  };

  return (
    <div>
      <div className="admin-page-header">
        <h1>🎶 Music Tracks</h1>
        <button className="btn btn-primary btn-sm" onClick={() => setUploadModal(true)}>
          + Upload Track
        </button>
      </div>

      <div className="admin-table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Title</th>
              <th>Album</th>
              <th>Duration</th>
              <th>Plays</th>
              <th>Size</th>
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
            ) : tracks.length === 0 ? (
              <tr>
                <td colSpan={6} className="text-center text-muted" style={{ padding: 'var(--space-2xl)' }}>
                  No tracks found
                </td>
              </tr>
            ) : (
              tracks.map((track) => (
                <tr key={track.id}>
                  <td><strong>{track.title}</strong></td>
                  <td className="text-muted">{track.albumTitle || '—'}</td>
                  <td className="text-muted">{formatDuration(track.duration)}</td>
                  <td>{track.playCount || 0}</td>
                  <td className="text-muted">{formatSize(track.fileSize)}</td>
                  <td>
                    <div className="actions">
                      <button
                        className="btn btn-sm btn-ghost"
                        onClick={() => {
                          setEditTitle(track.title);
                          setEditAlbumId(track.albumId?.toString() || '');
                          setEditModal(track);
                        }}
                      >
                        Edit
                      </button>
                      <button
                        className="btn btn-sm btn-danger"
                        onClick={() => setDeleteTarget(track)}
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

      {/* Upload Modal */}
      <Modal
        isOpen={uploadModal}
        onClose={() => setUploadModal(false)}
        title="Upload Track"
      >
        <div className="auth-form">
          <div className="input-group">
            <label>Title</label>
            <input className="input" value={uploadTitle} onChange={(e) => setUploadTitle(e.target.value)} placeholder="Track title" />
          </div>
          <div className="input-group">
            <label>Album</label>
            <select className="input" value={uploadAlbumId} onChange={(e) => setUploadAlbumId(e.target.value)}>
              <option value="">Select album...</option>
              {albums.map(a => (
                <option key={a.id} value={a.id}>{a.title}</option>
              ))}
            </select>
          </div>
          <div className="input-group">
            <label>Audio File</label>
            <input type="file" accept="audio/*" className="input" onChange={(e) => setUploadFile(e.target.files?.[0] || null)} />
          </div>
        </div>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setUploadModal(false)}>Cancel</button>
          <button className="btn btn-primary" onClick={handleUpload} disabled={isSubmitting}>
            {isSubmitting ? 'Uploading...' : 'Upload'}
          </button>
        </div>
      </Modal>

      {/* Edit Modal */}
      <Modal
        isOpen={!!editModal}
        onClose={() => setEditModal(null)}
        title="Edit Track"
      >
        <div className="auth-form">
          <div className="input-group">
            <label>Title</label>
            <input className="input" value={editTitle} onChange={(e) => setEditTitle(e.target.value)} />
          </div>
          <div className="input-group">
            <label>Album</label>
            <select className="input" value={editAlbumId} onChange={(e) => setEditAlbumId(e.target.value)}>
              <option value="">No album</option>
              {albums.map(a => (
                <option key={a.id} value={a.id}>{a.title}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setEditModal(null)}>Cancel</button>
          <button className="btn btn-primary" onClick={handleEdit} disabled={isSubmitting}>
            {isSubmitting ? 'Saving...' : 'Save'}
          </button>
        </div>
      </Modal>

      {/* Delete Modal */}
      <Modal
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title="Delete Track"
      >
        <p className="text-muted mb-lg">
          Are you sure you want to delete <strong>&quot;{deleteTarget?.title}&quot;</strong>?
        </p>
        <div className="modal-actions">
          <button className="btn btn-ghost" onClick={() => setDeleteTarget(null)}>Cancel</button>
          <button className="btn btn-danger" onClick={handleDelete}>Delete</button>
        </div>
      </Modal>
    </div>
  );
}

export default function AdminMusicTracksPage() {
  return (
    <Suspense fallback={
      <div className="loading-screen">
        <div className="spinner spinner-lg" />
        <p className="text-muted">Loading tracks...</p>
      </div>
    }>
      <TracksContent />
    </Suspense>
  );
}
