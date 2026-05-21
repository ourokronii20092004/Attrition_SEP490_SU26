"use client";

import { useEffect, useState, useRef, useCallback } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { formatCompact } from "@/lib/utils";
import styles from "./admin.module.css";

interface DashboardStats {
  totalUsers: number;
  totalWikiArticles: number;
  totalForumThreads: number;
  totalForumPosts: number;
  pendingContributions: number;
  totalMusicAlbums: number;
  totalMusicTracks: number;
  removedPosts: number;
}

// Animated count-up using requestAnimationFrame
function AnimatedNumber({ value, duration = 800 }: { value: number; duration?: number }) {
  const [display, setDisplay] = useState(0);
  const startTime = useRef<number>(0);
  const startVal = useRef<number>(0);

  const animate = useCallback(
    (timestamp: number) => {
      if (!startTime.current) {
        startTime.current = timestamp;
        startVal.current = display;
      }
      const progress = Math.min((timestamp - startTime.current) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      const current = Math.round(startVal.current + (value - startVal.current) * eased);
      setDisplay(current);
      if (progress < 1) {
        requestAnimationFrame(animate);
      }
    },
    [value, duration, display]
  );

  useEffect(() => {
    startTime.current = 0;
    requestAnimationFrame(animate);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value]);

  return <span>{formatCompact(display)}</span>;
}

export default function AdminDashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api
      .get<DashboardStats>("/admin/stats")
      .then((res) => {
        if (res.success && res.data) {
          setStats(res.data);
        }
      })
      .catch(() => {
        setStats({
          totalUsers: 0,
          totalWikiArticles: 0,
          totalForumThreads: 0,
          totalForumPosts: 0,
          pendingContributions: 0,
          totalMusicAlbums: 0,
          totalMusicTracks: 0,
          removedPosts: 0,
        });
      })
      .finally(() => setLoading(false));
  }, []);

  const statCards = stats
    ? [
        { label: "Total Users", value: stats.totalUsers },
        { label: "Wiki Articles", value: stats.totalWikiArticles },
        { label: "Forum Threads", value: stats.totalForumThreads },
        { label: "Forum Posts", value: stats.totalForumPosts },
        { label: "Pending Contributions", value: stats.pendingContributions },
        { label: "Removed Posts", value: stats.removedPosts },
        { label: "Music Albums", value: stats.totalMusicAlbums },
        { label: "Music Tracks", value: stats.totalMusicTracks },
      ]
    : [];

  return (
    <div>
      <h1 style={{ marginBottom: "var(--space-6)" }}>Dashboard</h1>

      {loading ? (
        <div className={styles.statsGrid}>
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className={styles.statCard}>
              <div className="skeleton skeleton-text" style={{ width: "80px" }} />
              <div className="skeleton skeleton-heading" style={{ width: "60px", marginTop: "var(--space-2)" }} />
            </div>
          ))}
        </div>
      ) : (
        <div className={styles.statsGrid}>
          {statCards.map((card) => (
            <div key={card.label} className={styles.statCard}>
              <div className={styles.statCardLabel}>{card.label}</div>
              <div className={styles.statCardValue}>
                <AnimatedNumber value={card.value} />
              </div>
            </div>
          ))}
        </div>
      )}

      <div className={styles.quickActions}>
        <Link href="/admin/wiki" className="btn btn-secondary btn-sm">
          Manage Articles
        </Link>
        <Link href="/admin/wiki/contributions" className="btn btn-secondary btn-sm">
          Review Contributions
        </Link>
        <Link href="/admin/users" className="btn btn-secondary btn-sm">
          Manage Users
        </Link>
        <Link href="/admin/music" className="btn btn-secondary btn-sm">
          Manage Music
        </Link>
      </div>
    </div>
  );
}
