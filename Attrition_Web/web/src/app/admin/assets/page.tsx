"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { formatDate, cn, debounce } from "@/lib/utils";
import styles from "../admin.module.css";

interface Asset {
  id: string;
  fileName: string;
  filePath: string;
  fileType: string;
  fileSize: number;
  uploadedBy: string;
  uploadedAt: string;
}

export default function AdminAssetsPage() {
  const toast = useToast();
  const [assets, setAssets] = useState<Asset[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [fileType, setFileType] = useState<string>("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const pageSize = 12; // Grid friendly

  // Upload state
  const [uploading, setUploading] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadType, setUploadType] = useState("image");

  const fetchAssets = useCallback(async (searchTerm: string, typeFilter: string, pageNum: number) => {
    setLoading(true);
    try {
      const res = await api.get<Asset[]>(
        "/admin/assets",
        { search: searchTerm, fileType: typeFilter, page: pageNum, pageSize }
      );
      if (res.success && res.data) {
        setAssets(res.data);
        setTotalCount(res.totalCount ?? res.data.length);
      }
    } catch {
      toast.error("Failed to load assets");
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    fetchAssets(search, fileType, page);
  }, [fetchAssets, search, fileType, page]);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const debouncedSearch = useCallback(
    debounce((term: string) => {
      setPage(1);
      setSearch(term);
    }, 300),
    []
  );

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) {
      toast.error("Please select a file to upload");
      return;
    }

    setUploading(true);
    const formData = new FormData();
    formData.append("file", selectedFile);
    formData.append("fileType", uploadType);

    try {
      // Direct fetch call because next api wrapper handles standard JSON bodies
      const token = localStorage.getItem("token");
      const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api"}/admin/assets`, {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
        },
        body: formData,
      });

      const res = await response.json();
      if (res.success) {
        toast.success("Asset uploaded successfully");
        setSelectedFile(null);
        // Clear input file element
        const fileInput = document.getElementById("asset-file-input") as HTMLInputElement;
        if (fileInput) fileInput.value = "";
        fetchAssets(search, fileType, page);
      } else {
        toast.error(res.error || "Upload failed");
      }
    } catch {
      toast.error("Failed to upload asset");
    } finally {
      setUploading(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to delete this asset?")) return;
    try {
      await api.delete(`/admin/assets/${id}`);
      setAssets((prev) => prev.filter((a) => a.id !== id));
      setTotalCount((c) => c - 1);
      toast.success("Asset deleted");
    } catch {
      toast.error("Failed to delete asset");
    }
  };

  const copyToClipboard = (text: string) => {
    // Generate absolute URL if relative
    const absoluteUrl = text.startsWith("http")
      ? text
      : `${process.env.NEXT_PUBLIC_API_URL?.replace("/api", "") || "http://localhost:5000"}${text}`;

    navigator.clipboard.writeText(absoluteUrl);
    toast.success("URL copied to clipboard!");
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div>
      <h1 style={{ marginBottom: "var(--space-6)" }}>Media Library & Assets</h1>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 350fr", gap: "var(--space-6)", alignItems: "start" }} className="admin-asset-layout">
        {/* Assets Main Section */}
        <div style={{ flex: 1 }}>
          <div className={cn(styles.adminTableHeader, "flex-wrap")} style={{ gap: "var(--space-4)", marginBottom: "var(--space-4)" }}>
            <input
              type="text"
              className="input"
              placeholder="Search assets..."
              onChange={(e) => debouncedSearch(e.target.value)}
              style={{ maxWidth: 280 }}
            />
            <div style={{ display: "flex", gap: "var(--space-2)" }}>
              <select
                className="input"
                value={fileType}
                onChange={(e) => { setFileType(e.target.value); setPage(1); }}
                style={{ width: 140 }}
              >
                <option value="">All Types</option>
                <option value="image">Images</option>
                <option value="sprite">Sprites</option>
                <option value="document">Documents</option>
              </select>
            </div>
            <span className="text-sm text-muted" style={{ marginLeft: "auto" }}>{totalCount} assets total</span>
          </div>

          {loading ? (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: "var(--space-4)" }}>
              {Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="skeleton" style={{ height: 200, borderRadius: "var(--radius-lg)" }} />
              ))}
            </div>
          ) : assets.length > 0 ? (
            <div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: "var(--space-4)", marginBottom: "var(--space-6)" }}>
                {assets.map((a) => {
                  const isImage = a.fileType === "image" || a.fileType === "sprite";
                  const absoluteUrl = a.filePath.startsWith("http")
                    ? a.filePath
                    : `${process.env.NEXT_PUBLIC_API_URL?.replace("/api", "") || "http://localhost:5000"}${a.filePath}`;

                  return (
                    <div
                      key={a.id}
                      className="card"
                      style={{
                        padding: "var(--space-3)",
                        display: "flex",
                        flexDirection: "column",
                        background: "rgba(20, 20, 25, 0.6)",
                        backdropFilter: "blur(8px)",
                        border: "1px solid rgba(255, 255, 255, 0.08)",
                        boxShadow: "0 8px 32px 0 rgba(0, 0, 0, 0.2)",
                        transition: "all 0.3s ease",
                        position: "relative",
                        overflow: "hidden"
                      }}
                      onMouseEnter={(e) => {
                        e.currentTarget.style.border = "1px solid var(--color-primary-semi)";
                        e.currentTarget.style.boxShadow = "0 8px 32px 0 rgba(235, 94, 40, 0.15)";
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.border = "1px solid rgba(255, 255, 255, 0.08)";
                        e.currentTarget.style.boxShadow = "0 8px 32px 0 rgba(0, 0, 0, 0.2)";
                      }}
                    >
                      {/* Thumbnail Container */}
                      <div
                        style={{
                          height: 120,
                          borderRadius: "var(--radius-md)",
                          overflow: "hidden",
                          background: "#0c0c0e",
                          display: "flex",
                          justifyContent: "center",
                          alignItems: "center",
                          marginBottom: "var(--space-3)",
                          position: "relative"
                        }}
                      >
                        {isImage ? (
                          // eslint-disable-next-line @next/next/no-img-element
                          <img
                            src={absoluteUrl}
                            alt={a.fileName}
                            style={{ width: "100%", height: "100%", objectFit: "contain" }}
                          />
                        ) : (
                          <div style={{ fontSize: "2.5rem", opacity: 0.5 }}>📄</div>
                        )}
                        <span
                          className={cn(
                            "badge text-xs",
                            a.fileType === "sprite" ? "badge-warning" : a.fileType === "document" ? "badge-primary" : "badge-success"
                          )}
                          style={{ position: "absolute", top: 8, right: 8, textTransform: "capitalize" }}
                        >
                          {a.fileType}
                        </span>
                      </div>

                      {/* Detail text */}
                      <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: "var(--space-1)" }}>
                        <span
                          className="text-sm font-semibold"
                          style={{
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            whiteSpace: "nowrap",
                            color: "var(--color-text-bright)"
                          }}
                          title={a.fileName}
                        >
                          {a.fileName}
                        </span>
                        <span className="text-xs text-muted">Size: {formatBytes(a.fileSize)}</span>
                        <span className="text-xs text-muted">By: {a.uploadedBy}</span>
                        <span className="text-xs text-muted">Uploaded: {formatDate(a.uploadedAt)}</span>
                      </div>

                      {/* Card actions */}
                      <div
                        style={{
                          display: "flex",
                          gap: "var(--space-1)",
                          marginTop: "var(--space-3)",
                          paddingTop: "var(--space-2)",
                          borderTop: "1px solid rgba(255, 255, 255, 0.05)"
                        }}
                      >
                        <button
                          className="btn btn-secondary btn-sm"
                          style={{ flex: 1, fontSize: "var(--text-xs)", padding: "4px" }}
                          onClick={() => copyToClipboard(a.filePath)}
                        >
                          🔗 Copy Link
                        </button>
                        <button
                          className="btn btn-danger btn-sm"
                          style={{ fontSize: "var(--text-xs)", padding: "4px 8px" }}
                          onClick={() => handleDelete(a.id)}
                        >
                          Delete
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>

              {totalPages > 1 && (
                <div className={styles.adminTableFooter}>
                  <span>Page {page} of {totalPages}</span>
                  <div className="pagination">
                    <button className="pagination-btn" disabled={page === 1} onClick={() => setPage(page - 1)}>←</button>
                    <button className="pagination-btn" disabled={page === totalPages} onClick={() => setPage(page + 1)}>→</button>
                  </div>
                </div>
              )}
            </div>
          ) : (
            <div className="card" style={{ padding: "var(--space-12)", textAlign: "center" }}>
              <span style={{ fontSize: "3rem", display: "block", marginBottom: "var(--space-4)" }}>📁</span>
              <h3>No assets found</h3>
              <p className="text-muted">Upload sprites, images, or documents to see them here.</p>
            </div>
          )}
        </div>

        {/* Upload Side Panel */}
        <div
          className="card"
          style={{
            background: "rgba(20, 20, 25, 0.6)",
            backdropFilter: "blur(12px)",
            border: "1px solid rgba(255, 255, 255, 0.08)",
            padding: "var(--space-5)"
          }}
        >
          <h3 style={{ marginBottom: "var(--space-4)", color: "var(--color-primary)" }}>Upload Asset</h3>
          <form onSubmit={handleUpload} style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
            <div className="input-group">
              <label className="input-label">Asset File</label>
              <input
                id="asset-file-input"
                type="file"
                className="input"
                style={{ padding: "var(--space-2)" }}
                onChange={(e) => {
                  if (e.target.files && e.target.files.length > 0) {
                    setSelectedFile(e.target.files[0]);
                  }
                }}
              />
            </div>

            <div className="input-group">
              <label className="input-label">Asset Category</label>
              <select
                className="input"
                value={uploadType}
                onChange={(e) => setUploadType(e.target.value)}
              >
                <option value="image">General Image (Concept, UI)</option>
                <option value="sprite">Game Sprite Sheet (Item, Enemy)</option>
                <option value="document">Lore Document (PDF, MD, Docx)</option>
              </select>
            </div>

            <button
              type="submit"
              className={cn("btn btn-primary", uploading && "btn-loading")}
              disabled={uploading || !selectedFile}
              style={{ width: "100%", marginTop: "var(--space-2)" }}
            >
              {uploading ? "Uploading..." : "Upload to Library"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
