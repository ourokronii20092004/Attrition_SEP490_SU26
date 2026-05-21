"use client";
 
import { useEffect, useState, useCallback, useRef } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate } from "@/lib/utils";
import styles from "../admin.module.css";
 
interface MusicAlbum {
  albumId: number;
  title: string;
  artists: string[];
  description: string | null;
  coverPath: string | null;
  isCoverUserDefined: boolean;
  trackCount: number;
  createdAt: string;
}
 
export default function AdminMusicPage() {
  const toast = useToast();
  const [albums, setAlbums] = useState<MusicAlbum[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [title, setTitle] = useState("");
  const [artist, setArtist] = useState("");
  const [description, setDescription] = useState("");
  const coverInputRef = useRef<HTMLInputElement>(null);
  const pendingCoverAlbumId = useRef<number | null>(null);
 
  const fetchAlbums = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<MusicAlbum[]>("/music/albums");
      if (res.success && res.data) setAlbums(Array.isArray(res.data) ? res.data : []);
    } catch {
      toast.error("Failed to load albums");
    } finally {
      setLoading(false);
    }
  }, [toast]);
 
  useEffect(() => { fetchAlbums(); }, [fetchAlbums]);
 
  const resetForm = () => {
    setShowForm(false); setEditId(null);
    setTitle(""); setArtist(""); setDescription("");
  };
 
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const artistsList = artist.split(/[,/;|\\]/).map(a => a.trim()).filter(Boolean);
    try {
      if (editId) {
        await api.put(`/music/albums/${editId}`, { title, artists: artistsList, description });
        toast.success("Album updated");
      } else {
        await api.post("/music/albums", { title, artists: artistsList, description });
        toast.success("Album created");
      }
      resetForm();
      fetchAlbums();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to save album");
    }
  };
 
  const handleCoverFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    const albumId = pendingCoverAlbumId.current;
    e.target.value = ""; // reset input
    if (!file || !albumId) return;
 
    const formData = new FormData();
    formData.append("file", file);
    try {
      await api.upload(`/music/albums/${albumId}/cover`, formData);
      toast.success("Cover uploaded");
      fetchAlbums();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to upload cover");
    }
  };
 
  const triggerCoverUpload = (albumId: number) => {
    pendingCoverAlbumId.current = albumId;
    coverInputRef.current?.click();
  };
 
  const handleDelete = async (id: number) => {
    if (!confirm("Delete this album and all its tracks?")) return;
    try {
      await api.delete(`/music/albums/${id}`);
      setAlbums((prev) => prev.filter((a) => a.albumId !== id));
      toast.success("Album deleted");
    } catch {
      toast.error("Failed to delete album");
    }
  };
 
  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-6)" }}>
        <h1>Music Albums</h1>
        <button className="btn btn-primary btn-sm" onClick={() => { resetForm(); setShowForm(true); }}>Add Album</button>
      </div>
 
      {showForm && (
        <form onSubmit={handleSubmit} style={{
          background: "var(--surface)", border: "1px solid var(--border)",
          borderRadius: "var(--radius-lg)", padding: "var(--space-5)",
          marginBottom: "var(--space-6)", display: "flex", flexDirection: "column", gap: "var(--space-4)"
        }}>
          <div className="input-group">
            <label className="input-label">Title</label>
            <input className="input" value={title} onChange={(e) => setTitle(e.target.value)} required />
          </div>
          <div className="input-group">
            <label className="input-label">Artists (comma-separated)</label>
            <input className="input" value={artist} onChange={(e) => setArtist(e.target.value)} required placeholder="Attrition OST, Composer A" />
          </div>
          <div className="input-group">
            <label className="input-label">Description</label>
            <textarea className="input" value={description} onChange={(e) => setDescription(e.target.value)} rows={2} />
          </div>
          <div style={{ display: "flex", gap: "var(--space-3)" }}>
            <button type="submit" className="btn btn-primary btn-sm">{editId ? "Update" : "Create"}</button>
            <button type="button" className="btn btn-secondary btn-sm" onClick={resetForm}>Cancel</button>
          </div>
        </form>
      )}
 
      {/* Hidden file input — albumId stored in ref, not state */}
      <input ref={coverInputRef} type="file" accept="image/*" hidden onChange={handleCoverFileChange} />
 
      <div className={styles.adminTableWrapper}>
        <table className="table">
          <thead><tr><th>Cover</th><th>Title</th><th>Artists</th><th>Tracks</th><th>Created</th><th>Actions</th></tr></thead>
          <tbody>
            {loading ? (
              Array.from({ length: 3 }).map((_, i) => (
                <tr key={i}>{Array.from({ length: 6 }).map((_, j) => (<td key={j}><div className="skeleton skeleton-text" /></td>))}</tr>
              ))
            ) : albums.length > 0 ? (
              albums.map((a) => (
                <tr key={a.albumId}>
                  <td>
                    <div style={{ position: "relative", width: 40, height: 40 }}>
                      <div style={{ width: 40, height: 40, borderRadius: "var(--radius-sm)", overflow: "hidden", background: "var(--bg-secondary)", display: "flex", alignItems: "center", justifyContent: "center", fontSize: "var(--text-lg)" }}>
                        {a.coverPath ? <img src={a.coverPath} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} /> : "🎵"}
                      </div>
                      {a.coverPath && (
                        <span style={{
                          position: "absolute",
                          bottom: -4,
                          right: -4,
                          fontSize: "8px",
                          padding: "2px 4px",
                          borderRadius: "4px",
                          background: a.isCoverUserDefined ? "#22c55e" : "#64748b",
                          color: "#fff",
                          fontWeight: "bold",
                          lineHeight: 1,
                          border: "1px solid var(--border)"
                        }}>
                          {a.isCoverUserDefined ? "User" : "Auto"}
                        </span>
                      )}
                    </div>
                  </td>
                  <td><strong>{a.title}</strong></td>
                  <td>
                    <span style={{ display: "inline-flex", gap: "var(--space-1)", alignItems: "center" }}>
                      {a.artists && a.artists.length > 0 ? a.artists.join(", ") : "Attrition OST"}
                    </span>
                  </td>
                  <td>{a.trackCount ?? 0}</td>
                  <td>{formatDate(a.createdAt)}</td>
                  <td>
                    <div style={{ display: "flex", gap: "var(--space-1)" }}>
                      <button className="btn btn-secondary btn-sm" style={{ fontSize: "var(--text-xs)" }}
                        onClick={() => { setEditId(a.albumId); setTitle(a.title); setArtist(a.artists?.join(", ") || ""); setDescription(a.description || ""); setShowForm(true); }}>Edit</button>
                      <button className="btn btn-secondary btn-sm" style={{ fontSize: "var(--text-xs)" }}
                        onClick={() => triggerCoverUpload(a.albumId)}>Cover</button>
                      <button className="btn btn-danger btn-sm" style={{ fontSize: "var(--text-xs)" }}
                        onClick={() => handleDelete(a.albumId)}>Delete</button>
                    </div>
                  </td>
                </tr>
              ))
            ) : (
              <tr><td colSpan={6} style={{ textAlign: "center", padding: "var(--space-8)" }}>No albums yet</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
