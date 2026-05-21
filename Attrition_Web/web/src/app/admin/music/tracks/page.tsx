"use client";

import { useEffect, useState, useCallback, useRef, useMemo } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, debounce } from "@/lib/utils";
import styles from "../../admin.module.css";
import { Plus, Pencil, Trash2, ChevronDown, ChevronRight, ChevronLeft, Upload } from "lucide-react";

interface MusicTrack {
  trackId: number;
  title: string;
  albumTitle: string;
  albumId: number;
  trackNumber: number;
  duration: number;
  playCount: number;
  genre: string | null;
  coverPath: string | null;
  isFeatured: boolean;
  artists: string[];
}

interface Album {
  albumId: number;
  title: string;
  coverPath: string | null;
}

export default function AdminTracksPage() {
  const toast = useToast();
  const [tracks, setTracks] = useState<MusicTrack[]>([]);
  const [albums, setAlbums] = useState<Album[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");

  // Pagination states
  const ITEMS_PER_PAGE = 5;
  const [currentPage, setCurrentPage] = useState(1);

  // Reset page when search term changes
  useEffect(() => {
    setCurrentPage(1);
  }, [search]);
  
  // Dialog & Wizard state
  const [showUpload, setShowUpload] = useState(false);
  const [uploadAlbumId, setUploadAlbumId] = useState<number | "">("");
  const [uploadTitle, setUploadTitle] = useState("");
  const [uploadTrackNum, setUploadTrackNum] = useState(1);
  const [trackArtists, setTrackArtists] = useState("");
  const [trackGenre, setTrackGenre] = useState("");
  const [isFeatured, setIsFeatured] = useState(false);
  const [newAlbumTitle, setNewAlbumTitle] = useState("");
  
  // Two-stage states
  const [scanStep, setScanStep] = useState<1 | 2>(1); // 1 = Upload & Scan, 2 = Adjust Metadata
  const [scannedData, setScannedData] = useState<{
    tempFileKey: string;
    title: string;
    album: string | null;
    artists: string[];
    genre: string | null;
    trackNumber: number;
    duration: number;
    tempCoverPath: string | null;
  } | null>(null);

  // Duplicate warning state
  const [duplicateWarning, setDuplicateWarning] = useState<string | null>(null);

  // Pending quick-upload album id (set before file picker opens)
  const pendingAlbumIdRef = useRef<number | null>(null);

  // Edit track states
  const [isEditMode, setIsEditMode] = useState(false);
  const [editTrackId, setEditTrackId] = useState<number | null>(null);

  // Custom cover upload
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [coverPreviewUrl, setCoverPreviewUrl] = useState<string | null>(null);

  // Accordion collapsed state
  const [collapsedAlbums, setCollapsedAlbums] = useState<Record<number, boolean>>({});

  // Upload overlay progress state
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const uploadAbortRef = useRef<AbortController | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const coverFileRef = useRef<HTMLInputElement>(null);

  const fetchTracks = useCallback(async () => {
    setLoading(true);
    try {
      const res = await api.get<MusicTrack[]>("/music/tracks");
      if (res.success && res.data) setTracks(Array.isArray(res.data) ? res.data : []);
    } catch {
      toast.error("Failed to load tracks");
    } finally {
      setLoading(false);
    }
  }, [toast]);

  const fetchAlbums = useCallback(async () => {
    try {
      const res = await api.get<Album[]>("/music/albums");
      if (res.success && res.data) setAlbums(res.data);
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    fetchTracks();
    fetchAlbums();
  }, [fetchTracks, fetchAlbums]);

  // Grouping logic
  const groupedTracks = useMemo(() => {
    const grouped: Record<number, { albumId: number; albumTitle: string; albumCoverPath: string | null; tracks: MusicTrack[] }> = {};
    
    // Pre-populate with all known albums so empty albums are displayed
    albums.forEach((a) => {
      grouped[a.albumId] = {
        albumId: a.albumId,
        albumTitle: a.title,
        albumCoverPath: a.coverPath,
        tracks: []
      };
    });

    tracks.forEach((track) => {
      const albumId = track.albumId;
      if (!grouped[albumId]) {
        grouped[albumId] = {
          albumId: track.albumId,
          albumTitle: track.albumTitle || `Album #${track.albumId}`,
          albumCoverPath: null,
          tracks: []
        };
      }
      grouped[albumId].tracks.push(track);
    });

    // Apply search filter if present
    if (search.trim()) {
      const term = search.toLowerCase();
      return Object.values(grouped)
        .map(group => ({
          ...group,
          tracks: group.tracks.filter(t => 
            t.title.toLowerCase().includes(term) || 
            (t.genre && t.genre.toLowerCase().includes(term)) ||
            (t.artists && t.artists.some(artist => artist.toLowerCase().includes(term)))
          )
        }))
        .filter(group => group.tracks.length > 0);
    }

    return Object.values(grouped);
  }, [tracks, albums, search]);

  const totalPages = useMemo(() => {
    return Math.ceil(groupedTracks.length / ITEMS_PER_PAGE);
  }, [groupedTracks]);

  const paginatedGroups = useMemo(() => {
    const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
    return groupedTracks.slice(startIndex, startIndex + ITEMS_PER_PAGE);
  }, [groupedTracks, currentPage]);

  const toggleAlbum = (albumId: number) => {
    setCollapsedAlbums(prev => ({ ...prev, [albumId]: !prev[albumId] }));
  };

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const debouncedSearch = useCallback(debounce((term: string) => setSearch(term), 300), []);

  const formatDuration = (s: number) => {
    const mins = Math.floor(s / 60);
    const secs = s % 60;
    return `${mins}:${secs.toString().padStart(2, "0")}`;
  };

  // Helper: find the next available track number in an album
  const getNextAvailableTrackNum = useCallback((albumId: number | "", desiredNum: number): number => {
    if (albumId === "") return desiredNum;
    const albumTracks = tracks.filter(t => t.albumId === albumId);
    const usedNums = new Set(albumTracks.map(t => t.trackNumber));
    let num = desiredNum;
    while (usedNums.has(num)) {
      num++;
    }
    return num;
  }, [tracks]);

  // Helper: check for duplicate title in an album (client-side)
  // When albumId is "", tries to resolve by albumName (handles "Create New Album" with existing name)
  const checkDuplicateTitle = useCallback((title: string, albumId: number | "", albumName?: string): string | null => {
    if (!title.trim()) return null;

    let resolvedAlbumId = albumId;
    let resolvedAlbumName = "";

    if (resolvedAlbumId === "" && albumName?.trim()) {
      const matched = albums.find(a => a.title.trim().toLowerCase() === albumName.trim().toLowerCase());
      if (matched) {
        resolvedAlbumId = matched.albumId;
        resolvedAlbumName = matched.title;
      }
    }

    if (resolvedAlbumId === "") return null;

    const existing = tracks.find(
      t => t.albumId === resolvedAlbumId && t.title.trim().toLowerCase() === title.trim().toLowerCase()
    );
    if (existing) {
      const displayName = resolvedAlbumName || albums.find(a => a.albumId === resolvedAlbumId)?.title || `Album #${resolvedAlbumId}`;
      return `A track titled "${existing.title}" already exists in album "${displayName}". Please change the title or select a different album.`;
    }
    return null;
  }, [tracks, albums]);

  const handleFileChangeAndScan = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Show the upload panel now that a file is picked
    setShowUpload(true);
    setDuplicateWarning(null);
    setUploading(true);
    setUploadProgress(0);
    setUploadError(null);
    setScanStep(1);

    const formData = new FormData();
    formData.append("file", file);

    const abortController = new AbortController();
    uploadAbortRef.current = abortController;

    try {
      const result = await new Promise<any>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open("POST", "/api/music/tracks/scan");

        const token = localStorage.getItem("attrition-token");
        if (token) xhr.setRequestHeader("Authorization", `Bearer ${token}`);

        xhr.upload.onprogress = (event) => {
          if (event.lengthComputable) {
            setUploadProgress(Math.round((event.loaded / event.total) * 100));
          }
        };

        xhr.onload = () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              const body = JSON.parse(xhr.responseText);
              resolve(body.data);
            } catch (err) {
              reject(new Error("Failed to parse scanner response"));
            }
          } else {
            try {
              const body = JSON.parse(xhr.responseText);
              reject(new Error(body.error || body.Error || `Scan failed (${xhr.status})`));
            } catch {
              reject(new Error(`Scan failed (${xhr.status})`));
            }
          }
        };

        xhr.onerror = () => reject(new Error("Network error during scanning"));
        abortController.signal.addEventListener("abort", () => {
          xhr.abort();
          reject(new Error("Scan cancelled"));
        });

        xhr.send(formData);
      });

      // Populate form fields with scanned metadata
      setScannedData(result);
      setUploadTitle(result.title || file.name.replace(/\.[^/.]+$/, ""));
      setTrackArtists(result.artists?.join(", ") || "");
      setTrackGenre(result.genre || "");
      setCoverPreviewUrl(result.tempCoverPath || null);

      // Determine the target album
      let resolvedAlbumId: number | "" = pendingAlbumIdRef.current ?? "";
      if (result.album && resolvedAlbumId === "") {
        const matched = albums.find(
          (a) => a.title.trim().toLowerCase() === result.album.trim().toLowerCase()
        );
        if (matched) {
          resolvedAlbumId = matched.albumId;
          setUploadAlbumId(matched.albumId);
          setNewAlbumTitle("");
        } else {
          setUploadAlbumId("");
          setNewAlbumTitle(result.album);
        }
      } else if (resolvedAlbumId !== "") {
        setUploadAlbumId(resolvedAlbumId);
        setNewAlbumTitle("");
      } else {
        setNewAlbumTitle("");
      }

      // Auto-bump track number if it conflicts
      const desiredTrackNum = result.trackNumber || 1;
      const safeTrackNum = getNextAvailableTrackNum(resolvedAlbumId, desiredTrackNum);
      setUploadTrackNum(safeTrackNum);

      // Client-side duplicate title check (warning, not blocking)
      const scannedTitle = result.title || file.name.replace(/\.[^/.]+$/, "");
      const warning = checkDuplicateTitle(scannedTitle, resolvedAlbumId);
      setDuplicateWarning(warning);

      setScanStep(2);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Failed to scan track";
      if (msg !== "Scan cancelled") {
        setUploadError(msg);
      }
    } finally {
      setUploading(false);
      uploadAbortRef.current = null;
      pendingAlbumIdRef.current = null;
      // Reset file input so re-picking the same file triggers onChange
      if (fileRef.current) fileRef.current.value = "";
    }
  };

  const handleCommitUpload = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!scannedData) return;

    // Client-side duplicate check before saving
    // When "Create New Album" is selected, also check by album name
    const dupCheck = checkDuplicateTitle(uploadTitle, uploadAlbumId, uploadAlbumId === "" ? (newAlbumTitle.trim() || scannedData.album?.trim()) : undefined);
    if (dupCheck) {
      toast.error(dupCheck);
      return;
    }

    let finalAlbumId = uploadAlbumId;
    if (finalAlbumId === "") {
      const albumTitleToCreate = newAlbumTitle.trim() || scannedData.album?.trim();
      if (!albumTitleToCreate) {
        toast.error("Please specify a name for the new album");
        return;
      }

      const existingAlbum = albums.find(
        (a) => a.title.toLowerCase() === albumTitleToCreate.toLowerCase()
      );

      if (existingAlbum) {
        finalAlbumId = existingAlbum.albumId;
      } else {
        setUploading(true);
        setUploadProgress(100);
        setUploadError(null);

        try {
          const albumRes = await api.post<{ albumId: number }>("/music/albums", {
            title: albumTitleToCreate,
            artists: trackArtists.split(/[,/;|\\]/).map(a => a.trim()).filter(Boolean),
            genre: trackGenre,
            albumType: "soundtrack"
          });

          if (albumRes.success && albumRes.data) {
            finalAlbumId = albumRes.data.albumId;
            fetchAlbums();
          } else {
            throw new Error(albumRes.error || "Failed to create new album");
          }
        } catch (err) {
          setUploadError(err instanceof Error ? err.message : "Failed to create album on-the-fly");
          setUploading(false);
          return;
        } finally {
          setUploading(false);
        }
      }
    }

    setUploading(true);
    setUploadProgress(100);
    setUploadError(null);

    const formData = new FormData();
    formData.append("TempFileKey", scannedData.tempFileKey);
    formData.append("AlbumId", String(finalAlbumId));
    formData.append("Title", uploadTitle);
    formData.append("TrackNumber", String(uploadTrackNum));
    formData.append("Duration", String(scannedData.duration));
    formData.append("Genre", trackGenre);
    formData.append("IsFeatured", String(isFeatured));
    
    if (scannedData.tempCoverPath) {
      formData.append("TempCoverPath", scannedData.tempCoverPath);
    }
    if (coverFile) {
      formData.append("CoverFile", coverFile);
    }

    const artistsList = trackArtists.split(/[,/;|\\]/).map(a => a.trim()).filter(Boolean);
    artistsList.forEach((artist, idx) => {
      formData.append(`Artists[${idx}]`, artist);
    });

    try {
      const res = await api.upload<any>("/music/tracks", formData);
      if (res.success) {
        toast.success("Track successfully saved!");
        setShowUpload(false);
        resetUploadWizard();
        fetchTracks();
      } else {
        throw new Error(res.error || "Save failed");
      }
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : "Failed to save track");
    } finally {
      setUploading(false);
    }
  };

  const handleEditClick = (track: MusicTrack) => {
    setEditTrackId(track.trackId);
    setUploadAlbumId(track.albumId);
    setUploadTitle(track.title);
    setUploadTrackNum(track.trackNumber);
    setTrackArtists(track.artists?.join(", ") || "");
    setTrackGenre(track.genre || "");
    setIsFeatured(track.isFeatured || false);
    setIsEditMode(true);
    setShowUpload(true);
  };

  const handleUpdateTrack = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editTrackId) return;

    try {
      const artistsList = trackArtists.split(/[,/;|\\]/).map(a => a.trim()).filter(Boolean);
      const res = await api.put<any>(`/music/tracks/${editTrackId}`, {
        title: uploadTitle,
        artists: artistsList,
        trackNumber: uploadTrackNum,
        genre: trackGenre,
        isFeatured
      });

      if (res.success) {
        toast.success("Track updated");
        setShowUpload(false);
        resetUploadWizard();
        fetchTracks();
      } else {
        throw new Error(res.error || "Update failed");
      }
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to update track");
    }
  };

  const resetUploadWizard = () => {
    setScanStep(1);
    setScannedData(null);
    setUploadTitle("");
    setUploadAlbumId("");
    setNewAlbumTitle("");
    setUploadTrackNum(1);
    setTrackArtists("");
    setTrackGenre("");
    setIsFeatured(false);
    setIsEditMode(false);
    setEditTrackId(null);
    setCoverFile(null);
    setCoverPreviewUrl(null);
    setDuplicateWarning(null);
    pendingAlbumIdRef.current = null;
  };

  const handleCoverOverrideChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setCoverFile(file);
      setCoverPreviewUrl(URL.createObjectURL(file));
    }
  };

  const handleCancelUpload = () => {
    uploadAbortRef.current?.abort();
    setUploading(false);
    setUploadProgress(0);
    setUploadError(null);
  };

  const handleDelete = async (id: number) => {
    if (!confirm("Delete this track?")) return;
    try {
      await api.delete(`/music/tracks/${id}`);
      setTracks((prev) => prev.filter((t) => t.trackId !== id));
      toast.success("Track deleted");
    } catch { toast.error("Failed to delete track"); }
  };

  const triggerQuickUpload = (albumId: number) => {
    resetUploadWizard();
    pendingAlbumIdRef.current = albumId;
    setUploadAlbumId(albumId);
    // Find track number for quick upload
    const albumGroup = tracks.filter(t => t.albumId === albumId);
    const maxTrackNum = albumGroup.length > 0 ? Math.max(...albumGroup.map(t => t.trackNumber)) : 0;
    setUploadTrackNum(maxTrackNum + 1);
    // Open file picker directly — form only appears after scan completes
    fileRef.current?.click();
  };

  return (
    <div>
      {/* Scanning/Uploading progress overlay */}
      {uploading && (
        <div style={{
          position: "fixed", inset: 0, zIndex: 9999,
          background: "rgba(0,0,0,0.75)", backdropFilter: "blur(6px)",
          display: "flex", alignItems: "center", justifyContent: "center"
        }}>
          <div style={{
            background: "var(--surface)", border: "1px solid var(--border)",
            borderRadius: "var(--radius-xl)", padding: "var(--space-8)",
            width: "100%", maxWidth: 420, textAlign: "center",
            boxShadow: "var(--shadow-2xl)"
          }}>
            <div style={{ fontSize: "var(--text-lg)", fontWeight: "var(--weight-semibold)", marginBottom: "var(--space-4)" }}>
              {scanStep === 1 ? "Uploading & Scanning Metadata..." : "Saving Track..."}
            </div>
            <div style={{ fontSize: "var(--text-4xl)", fontWeight: "var(--weight-bold)", marginBottom: "var(--space-4)", color: "var(--accent)" }}>
              {uploadProgress}%
            </div>
            <div style={{
              width: "100%", height: 8, borderRadius: 4,
              background: "var(--bg-secondary)", overflow: "hidden",
              marginBottom: "var(--space-5)"
            }}>
              <div style={{
                width: `${uploadProgress}%`, height: "100%",
                background: "var(--accent)", borderRadius: 4,
                transition: "width 0.2s ease"
              }} />
            </div>
            <p style={{ fontSize: "var(--text-sm)", color: "var(--text-muted)", marginBottom: "var(--space-5)" }}>
              Please do not refresh or close this tab.
            </p>
            {scanStep === 1 && (
              <button className="btn btn-danger btn-sm" onClick={handleCancelUpload}>
                Cancel Upload
              </button>
            )}
          </div>
        </div>
      )}

      {/* Upload/Scan error overlay */}
      {uploadError && (
        <div style={{
          position: "fixed", inset: 0, zIndex: 9999,
          background: "rgba(0,0,0,0.75)", backdropFilter: "blur(6px)",
          display: "flex", alignItems: "center", justifyContent: "center"
        }}>
          <div style={{
            background: "var(--surface)", border: "1px solid var(--danger)",
            borderRadius: "var(--radius-xl)", padding: "var(--space-8)",
            width: "100%", maxWidth: 420, textAlign: "center",
            boxShadow: "var(--shadow-2xl)"
          }}>
            <div style={{ fontSize: "var(--text-lg)", fontWeight: "var(--weight-semibold)", marginBottom: "var(--space-3)", color: "var(--danger)" }}>
              Upload Failed
            </div>
            <p style={{ fontSize: "var(--text-sm)", color: "var(--text-secondary)", marginBottom: "var(--space-5)", overflowWrap: "break-word" }}>
              {uploadError}
            </p>
            <button className="btn btn-secondary btn-sm" onClick={() => setUploadError(null)}>
              Dismiss
            </button>
          </div>
        </div>
      )}

      {/* Hidden file input — triggered programmatically */}
      <input ref={fileRef} type="file" accept="audio/*" hidden onChange={handleFileChangeAndScan} />

      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "var(--space-6)" }}>
        <h1>Music Tracks</h1>
        <button className="btn btn-primary btn-sm" onClick={() => { resetUploadWizard(); fileRef.current?.click(); }}>Upload Track</button>
      </div>

      {showUpload && (
        <form onSubmit={isEditMode ? handleUpdateTrack : handleCommitUpload} style={{
          background: "var(--surface)", border: "1px solid var(--border)",
          borderRadius: "var(--radius-lg)", padding: "var(--space-6)",
          marginBottom: "var(--space-6)", display: "flex", flexDirection: "column", gap: "var(--space-4)",
          boxShadow: "var(--shadow-md)"
        }}>
          <h3 style={{ margin: 0, fontSize: "var(--text-lg)", fontWeight: "var(--weight-bold)", borderBottom: "1px solid var(--border)", paddingBottom: "var(--space-2)" }}>
            {isEditMode ? "Edit Track Details" : "Review & Edit Track Details"}
          </h3>

          {/* Duplicate warning banner */}
          {duplicateWarning && (
            <div style={{
              background: "rgba(255, 170, 0, 0.1)",
              border: "1px solid rgba(255, 170, 0, 0.4)",
              borderRadius: "var(--radius-md)",
              padding: "var(--space-3) var(--space-4)",
              display: "flex",
              alignItems: "flex-start",
              gap: "var(--space-3)",
              fontSize: "var(--text-sm)",
              color: "var(--text-primary)",
              lineHeight: 1.5
            }}>
              <span>{duplicateWarning}</span>
            </div>
          )}

          {(
            // Metadata review form (post-scan or edit mode)
            <>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-4)" }}>
                <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
                  <div className="input-group">
                    <label className="input-label">Track Title</label>
                    <input className="input" value={uploadTitle} onChange={(e) => setUploadTitle(e.target.value)} required />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Track Artists (comma-separated)</label>
                    <input className="input" value={trackArtists} onChange={(e) => setTrackArtists(e.target.value)} placeholder="Composer A, Composer B" />
                  </div>
                  <div className="input-group">
                    <label className="input-label">Genre</label>
                    <input className="input" value={trackGenre} onChange={(e) => setTrackGenre(e.target.value)} placeholder="e.g. Ambient, Orchestral" />
                  </div>
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
                  {!isEditMode && (
                    <>
                      <div className="input-group">
                        <label className="input-label">Target Album</label>
                        <select 
                          className="input" 
                          value={uploadAlbumId} 
                          onChange={(e) => {
                            const val = e.target.value === "" ? "" : Number(e.target.value);
                            setUploadAlbumId(val);
                            if (val !== "") {
                              setNewAlbumTitle("");
                              // Auto-bump track number for the new album
                              setUploadTrackNum(getNextAvailableTrackNum(val, uploadTrackNum));
                            } else {
                              setNewAlbumTitle(scannedData?.album || "");
                            }
                            // Re-check duplicate warning for new album
                            setDuplicateWarning(checkDuplicateTitle(uploadTitle, val));
                          }}
                        >
                          <option value="">Create New Album</option>
                          {albums.map((a) => (
                            <option key={a.albumId} value={a.albumId}>
                              {a.title}
                            </option>
                          ))}
                        </select>
                      </div>

                      {uploadAlbumId === "" && (
                        <div className="input-group">
                          <label className="input-label">New Album Name</label>
                          <input 
                            className="input" 
                            value={newAlbumTitle} 
                            onChange={(e) => setNewAlbumTitle(e.target.value)} 
                            placeholder={scannedData?.album || "Enter new album name..."} 
                            required
                          />
                          <span className="text-xs text-muted" style={{ marginTop: "2px" }}>
                            {scannedData?.album ? `Detected from metadata: "${scannedData.album}"` : "Please type a name to create a new album."}
                          </span>
                        </div>
                      )}
                    </>
                  )}
                  <div className="input-group">
                    <label className="input-label">Track Number</label>
                    <input className="input" type="number" min={1} value={uploadTrackNum} onChange={(e) => setUploadTrackNum(Number(e.target.value))} required />
                  </div>
                </div>
              </div>

              {!isEditMode && (
                <div style={{
                  border: "1px solid var(--border)",
                  borderRadius: "var(--radius-md)",
                  padding: "var(--space-4)",
                  background: "var(--bg-secondary)",
                  display: "flex",
                  alignItems: "center",
                  gap: "var(--space-4)"
                }}>
                  <div style={{
                    width: 70, height: 70,
                    borderRadius: "var(--radius-sm)",
                    background: "var(--surface)",
                    border: "1px solid var(--border)",
                    display: "flex", alignItems: "center", justifyContent: "center",
                    fontSize: "var(--text-2xl)",
                    overflow: "hidden",
                    flexShrink: 0
                  }}>
                    {coverPreviewUrl ? <img src={coverPreviewUrl} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} /> : "🎵"}
                  </div>
                  <div style={{ flexGrow: 1 }}>
                    <div style={{ fontSize: "var(--text-sm)", fontWeight: "var(--weight-semibold)", marginBottom: "2px" }}>
                      {scannedData?.tempCoverPath && !coverFile ? "Scanned Album Artwork Found" : (coverFile ? "Custom Override Cover Selected" : "No Embedded Album Artwork")}
                    </div>
                    <div style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)", marginBottom: "var(--space-2)" }}>
                      You can override the cover art by uploading a custom image below.
                    </div>
                    <input ref={coverFileRef} type="file" accept="image/*" hidden onChange={handleCoverOverrideChange} />
                    <button 
                      type="button" 
                      className="btn btn-secondary" 
                      style={{ 
                        display: "inline-flex", 
                        alignItems: "center", 
                        justifyContent: "center",
                        width: 28, 
                        height: 28, 
                        padding: 0,
                        borderRadius: "var(--radius-md)",
                        minWidth: "auto",
                        marginTop: "4px"
                      }} 
                      onClick={() => coverFileRef.current?.click()}
                      title="Upload Custom Cover"
                    >
                      <Upload size={14} />
                    </button>
                    {coverFile && (
                      <button type="button" className="btn btn-danger btn-xs" style={{ marginLeft: "var(--space-2)" }} onClick={() => {
                        setCoverFile(null);
                        setCoverPreviewUrl(scannedData?.tempCoverPath || null);
                      }}>
                        Reset
                      </button>
                    )}
                  </div>
                </div>
              )}

              <div style={{ display: "flex", gap: "var(--space-3)", marginTop: "var(--space-2)", borderTop: "1px solid var(--border)", paddingTop: "var(--space-4)" }}>
                <button type="submit" className="btn btn-primary btn-sm">{isEditMode ? "Update Details" : "Save & Commit Track"}</button>
                {!isEditMode && (
                  <button type="button" className="btn btn-secondary btn-sm" onClick={() => { resetUploadWizard(); fileRef.current?.click(); }}>Pick Different File</button>
                )}
                <button type="button" className="btn btn-secondary btn-sm" onClick={() => { setShowUpload(false); resetUploadWizard(); }}>Cancel</button>
              </div>
            </>
          )}
        </form>
      )}

      {/* Main content grid */}
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-5)" }}>
        {/* Search header bar */}
        <div style={{
          background: "var(--surface)", border: "1px solid var(--border)",
          borderRadius: "var(--radius-lg)", padding: "var(--space-4)",
          display: "flex", justifyContent: "space-between", alignItems: "center"
        }}>
          <input type="text" className="input" placeholder="Search tracks, genres, artists..." onChange={(e) => debouncedSearch(e.target.value)} style={{ maxWidth: 320 }} />
          <span style={{ fontSize: "var(--text-sm)", fontWeight: "var(--weight-semibold)", color: "var(--text-secondary)" }}>
            Total Library: {tracks.length} tracks
          </span>
        </div>

        {loading ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="skeleton" style={{ height: 60, borderRadius: "var(--radius-lg)" }} />
            ))}
          </div>
        ) : paginatedGroups.length > 0 ? (
          <>
            {paginatedGroups.map((group) => {
              const isCollapsed = collapsedAlbums[group.albumId] ?? false;
              return (
                <div key={group.albumId} style={{
                  background: "var(--surface)",
                  border: "1px solid var(--border)",
                  borderRadius: "var(--radius-lg)",
                  overflow: "hidden",
                  boxShadow: "var(--shadow-sm)"
                }}>
                  {/* Accordion Album Header */}
                  <div style={{
                    padding: "var(--space-4) var(--space-5)",
                    background: "var(--bg-secondary)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    cursor: "pointer",
                    userSelect: "none"
                  }} onClick={() => toggleAlbum(group.albumId)}>
                    <div style={{ display: "flex", alignItems: "center", gap: "var(--space-4)" }}>
                      {/* Album cover thumbnail */}
                      <div style={{ width: 44, height: 44, borderRadius: "var(--radius-sm)", overflow: "hidden", background: "var(--surface)", border: "1px solid var(--border)", display: "flex", alignItems: "center", justifyContent: "center", fontSize: "var(--text-xl)" }}>
                        {group.albumCoverPath ? <img src={group.albumCoverPath} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} /> : "🎵"}
                      </div>
                      <div>
                        <div style={{ fontSize: "var(--text-base)", fontWeight: "var(--weight-bold)", lineHeight: 1.2 }}>
                          {group.albumTitle}
                        </div>
                        <div style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)", marginTop: "2px" }}>
                          {group.tracks.length} {group.tracks.length === 1 ? "track" : "tracks"}
                        </div>
                      </div>
                    </div>

                    <div style={{ display: "flex", alignItems: "center", gap: "var(--space-3)" }} onClick={(e) => e.stopPropagation()}>
                      <button 
                        type="button"
                        style={{ 
                          background: "none",
                          border: "none",
                          padding: 0,
                          color: "var(--text-muted)",
                          display: "inline-flex", 
                          alignItems: "center", 
                          justifyContent: "center",
                          cursor: "pointer",
                          transition: "color 0.2s ease"
                        }} 
                        onClick={(e) => {
                          e.stopPropagation();
                          triggerQuickUpload(group.albumId);
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.color = "var(--primary)"}
                        onMouseLeave={(e) => e.currentTarget.style.color = "var(--text-muted)"}
                        title="Quick Upload Track"
                      >
                        <Plus size={18} />
                      </button>
                      <div style={{ color: "var(--text-muted)", display: "flex", alignItems: "center" }} onClick={() => toggleAlbum(group.albumId)}>
                        {isCollapsed ? (
                          <ChevronRight size={18} />
                        ) : (
                          <ChevronDown size={18} />
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Accordion Table Body */}
                  {!isCollapsed && (
                    <div style={{ padding: "0 var(--space-4) var(--space-2) var(--space-4)" }}>
                      {group.tracks.length > 0 ? (
                        <table className="table" style={{ margin: "var(--space-2) 0", width: "100%", tableLayout: "fixed" }}>
                          <thead>
                            <tr>
                              <th style={{ width: "50px", textAlign: "center" }}>#</th>
                              <th style={{ width: "35%" }}>Title</th>
                              <th style={{ width: "25%" }}>Artists</th>
                              <th style={{ width: "15%" }}>Genre</th>
                              <th style={{ width: "80px" }}>Duration</th>
                              <th style={{ width: "70px", textAlign: "center" }}>Plays</th>
                              <th style={{ width: "80px", textAlign: "right" }}>Actions</th>
                            </tr>
                          </thead>
                          <tbody>
                            {group.tracks.map((t) => (
                              <tr key={t.trackId}>
                                <td style={{ textAlign: "center", color: "var(--text-muted)" }}>
                                  {t.trackNumber.toString().padStart(2, "0")}
                                </td>
                                <td>
                                  <div style={{ display: "flex", alignItems: "center", gap: "var(--space-3)", overflow: "hidden" }}>
                                    {t.coverPath && (
                                      <div style={{ width: 28, height: 28, borderRadius: "4px", overflow: "hidden", border: "1px solid var(--border)", flexShrink: 0 }}>
                                        <img src={t.coverPath} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                                      </div>
                                    )}
                                    <div style={{ display: "flex", flexDirection: "column", overflow: "hidden" }}>
                                      <span style={{ fontWeight: "var(--weight-semibold)", textOverflow: "ellipsis", overflow: "hidden", whiteSpace: "nowrap" }}>{t.title}</span>

                                    </div>
                                  </div>
                                </td>
                                <td style={{ fontSize: "var(--text-sm)", color: "var(--text-secondary)", textOverflow: "ellipsis", overflow: "hidden", whiteSpace: "nowrap" }}>
                                  {t.artists && t.artists.length > 0 ? t.artists.join(", ") : "Attrition OST"}
                                </td>
                                <td style={{ fontSize: "var(--text-sm)", color: "var(--text-secondary)", textOverflow: "ellipsis", overflow: "hidden", whiteSpace: "nowrap" }}>
                                  {t.genre || <span style={{ color: "var(--text-muted)", fontStyle: "italic" }}>None</span>}
                                </td>
                                <td>{formatDuration(t.duration || 0)}</td>
                                <td style={{ textAlign: "center", color: "var(--text-muted)" }}>{t.playCount}</td>
                                <td>
                                  <div style={{ display: "flex", gap: "var(--space-1.5)", justifyContent: "flex-end" }}>
                                    <button 
                                      type="button"
                                      className="btn btn-secondary" 
                                      style={{ 
                                        display: "inline-flex", 
                                        alignItems: "center", 
                                        justifyContent: "center",
                                        width: 28, 
                                        height: 28, 
                                        padding: 0,
                                        borderRadius: "var(--radius-md)",
                                        minWidth: "auto"
                                      }} 
                                      onClick={() => handleEditClick(t)}
                                      title="Edit Track"
                                    >
                                      <Pencil size={14} />
                                    </button>
                                    <button 
                                      type="button"
                                      className="btn btn-danger" 
                                      style={{ 
                                        display: "inline-flex", 
                                        alignItems: "center", 
                                        justifyContent: "center",
                                        width: 28, 
                                        height: 28, 
                                        padding: 0,
                                        borderRadius: "var(--radius-md)",
                                        minWidth: "auto"
                                      }} 
                                      onClick={() => handleDelete(t.trackId)}
                                      title="Delete Track"
                                    >
                                      <Trash2 size={14} />
                                    </button>
                                  </div>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      ) : (
                        <div style={{ textAlign: "center", padding: "var(--space-6)", color: "var(--text-muted)", fontSize: "var(--text-sm)" }}>
                          No tracks in this album yet. Click the <strong>Quick Upload</strong> button above to add one!
                        </div>
                      )}
                    </div>
                  )}
                </div>
              );
            })}

            {/* Pagination controls */}
            {totalPages > 1 && (
              <div style={{
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                gap: "var(--space-2)",
                marginTop: "var(--space-4)",
                padding: "var(--space-3) 0"
              }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    justifyContent: "center",
                    width: 32,
                    height: 32,
                    padding: 0,
                    borderRadius: "var(--radius-md)",
                    minWidth: "auto"
                  }}
                  onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
                  disabled={currentPage === 1}
                  title="Previous Page"
                >
                  <ChevronLeft size={16} />
                </button>
                
                {Array.from({ length: totalPages }).map((_, idx) => {
                  const pageNum = idx + 1;
                  const isCurrent = pageNum === currentPage;
                  return (
                    <button
                      key={pageNum}
                      type="button"
                      className={isCurrent ? "btn btn-primary" : "btn btn-secondary"}
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        justifyContent: "center",
                        width: 32,
                        height: 32,
                        padding: 0,
                        borderRadius: "var(--radius-md)",
                        minWidth: "auto",
                        fontWeight: isCurrent ? "var(--weight-bold)" : "var(--weight-normal)",
                        boxShadow: isCurrent ? "var(--shadow-sm)" : "none"
                      }}
                      onClick={() => setCurrentPage(pageNum)}
                    >
                      {pageNum}
                    </button>
                  );
                })}

                <button
                  type="button"
                  className="btn btn-secondary"
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    justifyContent: "center",
                    width: 32,
                    height: 32,
                    padding: 0,
                    borderRadius: "var(--radius-md)",
                    minWidth: "auto"
                  }}
                  onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
                  disabled={currentPage === totalPages}
                  title="Next Page"
                >
                  <ChevronRight size={16} />
                </button>
              </div>
            )}
          </>
        ) : (
          <div style={{
            background: "var(--surface)", border: "1px solid var(--border)",
            borderRadius: "var(--radius-lg)", padding: "var(--space-8)",
            textAlign: "center", color: "var(--text-muted)"
          }}>
            No matches found for search query. Try creating an album first or adjusting your search term.
          </div>
        )}
      </div>
    </div>
  );
}
