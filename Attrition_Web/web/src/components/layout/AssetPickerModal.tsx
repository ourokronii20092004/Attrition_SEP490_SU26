"use client";

import React, { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useToast } from "@/contexts/ToastContext";
import { cn } from "@/lib/utils";
import { X, Search, Image as ImageIcon, FileText } from "lucide-react";
import styles from "./AssetPickerModal.module.css";

interface Asset {
  id: string;
  fileName: string;
  filePath: string;
  fileType: string;
  fileSize: number;
  uploadedBy: string;
  uploadedAt: string;
}

interface AssetPickerModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSelect: (filePath: string) => void;
  allowedTypes?: string[]; // e.g. ["image", "sprite"]
}

export default function AssetPickerModal({
  isOpen,
  onClose,
  onSelect,
  allowedTypes = ["image", "sprite"],
}: AssetPickerModalProps) {
  const toast = useToast();
  const [assets, setAssets] = useState<Asset[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [selectedType, setSelectedType] = useState<string>("");

  const fetchAssets = useCallback(async () => {
    if (!isOpen) return;
    setLoading(true);
    try {
      // Query with size 100 for easy picker search
      const res = await api.get<Asset[]>("/admin/assets", {
        search,
        fileType: selectedType || undefined,
        page: 1,
        pageSize: 100,
      });
      if (res.success && res.data) {
        // Filter locally by allowedTypes if specified
        const filtered = res.data.filter((a) =>
          allowedTypes.includes(a.fileType.toLowerCase())
        );
        setAssets(filtered);
      }
    } catch {
      toast.error("Failed to load assets in picker");
    } finally {
      setLoading(false);
    }
  }, [isOpen, search, selectedType, allowedTypes, toast]);

  useEffect(() => {
    fetchAssets();
  }, [fetchAssets]);

  if (!isOpen) return null;

  return (
    <div className={styles.modalOverlay} onClick={onClose}>
      <div className={styles.modalContent} onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className={styles.modalHeader}>
          <h3>Select Media Asset</h3>
          <button className={styles.closeBtn} onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        {/* Filters */}
        <div className={styles.modalFilters}>
          <div className={styles.searchWrapper}>
            <Search size={16} className={styles.searchIcon} />
            <input
              type="text"
              placeholder="Search by filename..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="input"
            />
          </div>
          <select
            value={selectedType}
            onChange={(e) => setSelectedType(e.target.value)}
            className="input"
            style={{ width: 140 }}
          >
            <option value="">All Categories</option>
            {allowedTypes.map((type) => (
              <option key={type} value={type}>
                {type.charAt(0).toUpperCase() + type.slice(1)}s
              </option>
            ))}
          </select>
        </div>

        {/* Asset Grid */}
        <div className={styles.gridContainer}>
          {loading ? (
            <div className={styles.loadingGrid}>
              {Array.from({ length: 8 }).map((_, i) => (
                <div key={i} className="skeleton" style={{ height: 110, borderRadius: "var(--radius-md)" }} />
              ))}
            </div>
          ) : assets.length > 0 ? (
            <div className={styles.assetGrid}>
              {assets.map((asset) => {
                const isImage = asset.fileType === "image" || asset.fileType === "sprite";
                const absoluteUrl = asset.filePath.startsWith("http")
                  ? asset.filePath
                  : `${process.env.NEXT_PUBLIC_API_URL?.replace("/api", "") || "http://localhost:5000"}${asset.filePath}`;

                return (
                  <div
                    key={asset.id}
                    className={styles.assetCard}
                    onClick={() => {
                      onSelect(asset.filePath);
                      onClose();
                    }}
                  >
                    <div className={styles.thumbnailWrapper}>
                      {isImage ? (
                        <img src={absoluteUrl} alt={asset.fileName} />
                      ) : (
                        <FileText size={24} className={styles.fileIcon} />
                      )}
                      <span className={cn(
                        styles.typeBadge,
                        asset.fileType === "sprite" && styles.badgeSprite,
                        asset.fileType === "image" && styles.badgeImage
                      )}>
                        {asset.fileType}
                      </span>
                    </div>
                    <div className={styles.metaInfo}>
                      <span className={styles.fileName} title={asset.fileName}>
                        {asset.fileName}
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <div className={styles.emptyState}>
              <ImageIcon size={32} />
              <p>No matching assets found</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
