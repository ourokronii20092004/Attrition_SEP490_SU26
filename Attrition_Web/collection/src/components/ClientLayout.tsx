"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { usePlayer } from "@/contexts/PlayerContext";
import { cn, formatDuration, getInitials, getAvatarUrl, assetUrl } from "@/lib/utils";
import {
  Home, Music, Heart, Star,
  Play, Pause, SkipForward, SkipBack,
  Shuffle, Repeat, Repeat1,
  Volume2, Volume1, VolumeX,
  ArrowLeft,
} from "lucide-react";
import { useTheme, ACCENT_COLORS, type ThemeMode, type AccentName } from "@/contexts/ThemeContext";
import styles from "./ClientLayout.module.css";

// ─── Sidebar ───────────────────────────────────────────────

const NAV_LINKS = [
  { href: "/", label: "Home", icon: <Home size={18} /> },
  { href: "/featured", label: "Featured", icon: <Star size={18} /> },
  { href: "/albums", label: "Albums", icon: <Music size={18} /> },
  { href: "/favorites", label: "Favorites", icon: <Heart size={18} /> },
];

function Sidebar() {
  const pathname = usePathname();
  const { user, isAuthenticated, logout } = useAuth();
  const { currentTrack } = usePlayer();
  const { mode, setMode, accent, setAccent } = useTheme();
  const [isMenuOpen, setIsMenuOpen] = React.useState(false);

  // Close menu when clicking outside
  React.useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (isMenuOpen && !(e.target as Element).closest(`.${styles.userMenuContainer}`)) {
        setIsMenuOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [isMenuOpen]);

  return (
    <aside className={styles.sidebar}>
      <div className={styles.sidebarHeader}>
        <Link href="/" className={styles.sidebarLogo}>
          COLLECTION
        </Link>
      </div>

      <nav className={styles.sidebarNav}>
        {NAV_LINKS.map((link) => (
          <Link
            key={link.href}
            href={link.href}
            className={cn(
              styles.sidebarLink,
              pathname === link.href && styles.sidebarLinkActive
            )}
          >
            <span className={styles.sidebarIcon}>{link.icon}</span>
            {link.label}
          </Link>
        ))}
      </nav>

      {currentTrack && (
        <div className={styles.nowPlaying}>
          <span className={styles.nowPlayingLabel}>Now Playing</span>
          <div className={styles.nowPlayingTrack}>
            <div className={styles.nowPlayingArt}>
              {currentTrack.coverPath ? (
                <img src={assetUrl(currentTrack.coverPath)} alt="" />
              ) : (
                <span><Music size={16} /></span>
              )}
            </div>
            <div className={styles.nowPlayingInfo}>
              <span className={styles.nowPlayingTitle}>{currentTrack.title}</span>
              <span className={styles.nowPlayingArtist}>
                {currentTrack.albumArtist}
              </span>
            </div>
          </div>
        </div>
      )}

      <div className={styles.sidebarFooter}>
        {isAuthenticated && user ? (
          <div className={styles.userMenuContainer}>
            <div 
              className={cn(styles.sidebarUser, isMenuOpen && styles.sidebarUserActive)} 
              onClick={() => setIsMenuOpen(!isMenuOpen)}
            >
              <div className="avatar avatar-sm">
                {getAvatarUrl(user.avatarUrl) ? (
                  <img src={getAvatarUrl(user.avatarUrl)!} alt="" />
                ) : (
                  getInitials(user.displayName || user.username)
                )}
              </div>
              <span className={styles.sidebarUserName}>
                {user.displayName || user.username}
              </span>
            </div>

            {isMenuOpen && (
              <div className={styles.userMenuDropdown}>
                <div className={styles.sidebarTheme}>
                  <div className={styles.themeRow}>
                    <span className={styles.themeLabel}>Theme</span>
                    <div className={styles.themeOptions}>
                      {(["light", "dark", "system"] as ThemeMode[]).map((m) => (
                        <button
                          key={m}
                          className={cn(styles.themeBtn, mode === m && styles.themeBtnActive)}
                          onClick={(e) => { e.stopPropagation(); setMode(m); }}
                          title={m}
                        >
                          {m === "light" ? "☀" : m === "dark" ? "☾" : "⚙"}
                        </button>
                      ))}
                    </div>
                  </div>
                  <div className={styles.themeRow}>
                    <span className={styles.themeLabel}>Color</span>
                    <div className={styles.accentGrid}>
                      {(Object.keys(ACCENT_COLORS) as AccentName[]).map((name) => (
                        <button
                          key={name}
                          className={cn(styles.accentBtn, accent === name && styles.accentBtnActive)}
                          onClick={(e) => { e.stopPropagation(); setAccent(name); }}
                          title={ACCENT_COLORS[name].label}
                        >
                          <span className={styles.accentDot} style={{ background: ACCENT_COLORS[name].hex }} />
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
                <button 
                  className={styles.logoutBtn} 
                  onClick={() => {
                    logout();
                    setIsMenuOpen(false);
                    window.location.href = "/";
                  }}
                >
                  Log Out
                </button>
              </div>
            )}
          </div>
        ) : (
          <div style={{ display: "flex", gap: "var(--space-2)", width: "100%" }}>
            <a
              href={`${process.env.NEXT_PUBLIC_WEB_URL || "http://localhost:3000"}/login?redirect=collection-relay`}
              className="btn btn-secondary btn-sm"
              style={{ flex: 1 }}
            >
              Sign In
            </a>
            <div className={styles.themeOptions} style={{ background: "transparent", padding: 0 }}>
              {(["light", "dark", "system"] as ThemeMode[]).map((m) => (
                <button
                  key={m}
                  className={cn(styles.themeBtn, mode === m && styles.themeBtnActive)}
                  onClick={() => setMode(m)}
                  title={m}
                >
                  {m === "light" ? "☀" : m === "dark" ? "☾" : "⚙"}
                </button>
              ))}
            </div>
          </div>
        )}

        <a
          href={process.env.NEXT_PUBLIC_WEB_URL || "http://localhost:3000"}
          className={styles.backLink}
          target="_blank"
          rel="noopener noreferrer"
        >
          <ArrowLeft size={14} style={{ marginRight: 6 }} /> Back to Attrition
        </a>
      </div>
    </aside>
  );
}

// ─── Player Bar ────────────────────────────────────────────

function PlayerBar() {
  const {
    currentTrack,
    isPlaying,
    progress,
    currentTime,
    duration,
    volume,
    isMuted,
    isShuffled,
    repeatMode,
    togglePlayPause,
    next,
    previous,
    seek,
    pause,
    resume,
    setVolume,
    toggleMute,
    toggleShuffle,
    cycleRepeat,
  } = usePlayer();

  if (!currentTrack) {
    return (
      <div className={cn(styles.playerBar, styles.playerBarEmpty)}>
        <span className={styles.playerEmptyText}>
          Select a track to start listening
        </span>
      </div>
    );
  }

  const progressRef = React.useRef<HTMLDivElement>(null);
  const [dragProgress, setDragProgress] = React.useState<number | null>(null);

  const getSeekPosition = (e: MouseEvent | React.MouseEvent) => {
    const bar = progressRef.current;
    if (!bar) return null;
    const rect = bar.getBoundingClientRect();
    return Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
  };

  const handleProgressMouseDown = (e: React.MouseEvent<HTMLDivElement>) => {
    const pos = getSeekPosition(e as unknown as MouseEvent);
    if (pos !== null) {
      setDragProgress(pos);
      pause();
    }

    const onMouseMove = (moveEvent: MouseEvent) => {
      const movePos = getSeekPosition(moveEvent);
      if (movePos !== null) {
        setDragProgress(movePos);
      }
    };

    const onMouseUp = (upEvent: MouseEvent) => {
      const upPos = getSeekPosition(upEvent);
      // Fallback to latest drag state if release event is out of bounds
      const finalPos = upPos !== null ? upPos : pos;
      if (finalPos !== null) {
        seek(finalPos * duration);
        resume();
      }
      setDragProgress(null);
      document.removeEventListener("mousemove", onMouseMove);
      document.removeEventListener("mouseup", onMouseUp);
    };

    document.addEventListener("mousemove", onMouseMove);
    document.addEventListener("mouseup", onMouseUp);
  };

  const handleVolumeChange = (e: React.MouseEvent<HTMLDivElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = (e.clientX - rect.left) / rect.width;
    setVolume(Math.max(0, Math.min(1, x)));
  };

  return (
    <div className={styles.playerBar}>
      {/* Track info */}
      <div className={styles.playerTrackInfo}>
        <div className={styles.playerArt}>
          {currentTrack.coverPath ? (
            <img src={assetUrl(currentTrack.coverPath)} alt="" />
          ) : (
            <span><Music size={16} /></span>
          )}
        </div>
        <div className={styles.playerTrackMeta}>
          <span className={styles.playerTrackTitle}>{currentTrack.title}</span>
          <span className={styles.playerTrackArtist}>{currentTrack.albumArtist}</span>
        </div>
      </div>

      {/* Controls */}
      <div className={styles.playerControls}>
        <div className={styles.playerButtons}>
          <button
            className={cn(styles.controlBtn, isShuffled && styles.controlBtnActive)}
            onClick={toggleShuffle}
            title="Shuffle"
          >
            <Shuffle size={18} />
          </button>
          <button className={styles.controlBtn} onClick={previous} title="Previous">
            <SkipBack size={18} />
          </button>
          <button
            className={cn(styles.controlBtn, styles.playPauseBtn)}
            onClick={togglePlayPause}
            title={isPlaying ? "Pause" : "Play"}
          >
            {isPlaying ? <Pause size={22} /> : <Play size={22} />}
          </button>
          <button className={styles.controlBtn} onClick={next} title="Next">
            <SkipForward size={18} />
          </button>
          <button
            className={cn(
              styles.controlBtn,
              repeatMode !== "none" && styles.controlBtnActive
            )}
            onClick={cycleRepeat}
            title={`Repeat: ${repeatMode}`}
          >
            {repeatMode === "one" ? <Repeat1 size={18} /> : <Repeat size={18} />}
          </button>
        </div>

        {/* Progress */}
        {
          (() => {
            const displayProgress = dragProgress !== null ? dragProgress : progress;
            const displayTime = dragProgress !== null ? dragProgress * duration : currentTime;
            return (
              <div className={styles.progressRow}>
                <span className={styles.timeLabel}>{formatDuration(displayTime)}</span>
                <div
                  className={styles.progressBar}
                  onMouseDown={handleProgressMouseDown}
                  ref={progressRef}
                  data-dragging={dragProgress !== null || undefined}
                >
                  <div
                    className={styles.progressFill}
                    style={{ width: `${displayProgress * 100}%` }}
                  />
                </div>
                <span className={styles.timeLabel}>{formatDuration(duration)}</span>
              </div>
            );
          })()
        }
      </div>

      {/* Volume */}
      <div className={styles.playerVolume}>
        <button
          className={styles.controlBtn}
          onClick={toggleMute}
          title={isMuted ? "Unmute" : "Mute"}
        >
          {isMuted || volume === 0 ? <VolumeX size={18} /> : volume < 0.5 ? <Volume1 size={18} /> : <Volume2 size={18} />}
        </button>
        <div className={styles.volumeBar} onClick={handleVolumeChange}>
          <div
            className={styles.volumeFill}
            style={{ width: `${(isMuted ? 0 : volume) * 100}%` }}
          />
        </div>
      </div>
    </div>
  );
}

// ─── Client Layout ─────────────────────────────────────────

export default function ClientLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className={styles.appLayout}>
      <Sidebar />
      <main className={styles.mainContent}>{children}</main>
      <PlayerBar />
    </div>
  );
}
