/**
 * Utility functions — formatters, helpers, shared logic
 */

// ─── Date / Time ───────────────────────────────────────────

/**
 * Format a date string to a human-readable relative time.
 * "2 minutes ago", "3 hours ago", "5 days ago", etc.
 */
export function timeAgo(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);

  if (seconds < 10) return "just now";
  if (seconds < 60) return `${seconds}s ago`;

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;

  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;

  const weeks = Math.floor(days / 7);
  if (weeks < 4) return `${weeks}w ago`;

  const months = Math.floor(days / 30);
  if (months < 12) return `${months}mo ago`;

  const years = Math.floor(days / 365);
  return `${years}y ago`;
}

/**
 * Format a date string to a full readable date.
 * "May 20, 2026"
 */
export function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

/**
 * Format a date string to include time.
 * "May 20, 2026, 3:45 PM"
 */
export function formatDateTime(dateString: string): string {
  return new Date(dateString).toLocaleDateString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

// ─── Duration ──────────────────────────────────────────────

/**
 * Format seconds to "m:ss" or "h:mm:ss"
 */
export function formatDuration(seconds: number): string {
  if (!seconds || seconds < 0) return "0:00";

  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = Math.floor(seconds % 60);

  if (h > 0) {
    return `${h}:${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  }
  return `${m}:${s.toString().padStart(2, "0")}`;
}

/**
 * Format seconds to human-readable duration.
 * "1h 23m", "45m", "2h"
 */
export function formatDurationLong(seconds: number): string {
  if (!seconds || seconds < 0) return "0m";

  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);

  if (h > 0 && m > 0) return `${h}h ${m}m`;
  if (h > 0) return `${h}h`;
  return `${m}m`;
}

// ─── Numbers ───────────────────────────────────────────────

/**
 * Format a number with compact notation.
 * 1200 → "1.2K", 1500000 → "1.5M"
 */
export function formatCompact(num: number): string {
  if (num < 1000) return num.toString();
  if (num < 1000000) return `${(num / 1000).toFixed(1).replace(/\.0$/, "")}K`;
  return `${(num / 1000000).toFixed(1).replace(/\.0$/, "")}M`;
}

// ─── Strings ───────────────────────────────────────────────

/**
 * Generate initials from a name.
 * "Le Trung Hau" → "LH", "admin" → "AD"
 */
export function getInitials(name: string): string {
  if (!name) return "?";
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/**
 * Truncate text to a max length with ellipsis.
 */
export function truncate(text: string, maxLength: number): string {
  if (text.length <= maxLength) return text;
  return text.slice(0, maxLength).trimEnd() + "…";
}

/**
 * Slugify a string for URLs.
 * "Hello World" → "hello-world"
 */
export function slugify(text: string): string {
  return text
    .toLowerCase()
    .trim()
    .replace(/[^\w\s-]/g, "")
    .replace(/\s+/g, "-")
    .replace(/-+/g, "-");
}

/**
 * Capitalize first letter.
 */
export function capitalize(text: string): string {
  if (!text) return "";
  return text.charAt(0).toUpperCase() + text.slice(1);
}

// ─── Debounce ──────────────────────────────────────────────

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function debounce<T extends (...args: any[]) => any>(
  fn: T,
  delay: number
): (...args: Parameters<T>) => void {
  let timeoutId: ReturnType<typeof setTimeout>;
  return (...args: Parameters<T>) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => fn(...args), delay);
  };
}

// ─── Class Names ───────────────────────────────────────────

/**
 * Conditionally join class names. Falsy values are filtered out.
 * cn("btn", isActive && "active", size === "lg" && "btn-lg")
 */
export function cn(...classes: (string | false | null | undefined)[]): string {
  return classes.filter(Boolean).join(" ");
}

// ─── Avatar ────────────────────────────────────────────────

/**
 * Get avatar URL, handling various sources.
 */
export function getAvatarUrl(
  avatarPath?: string | null,
  googleAvatarUrl?: string | null
): string | null {
  if (avatarPath) {
    if (avatarPath.startsWith("http")) return avatarPath;
    // Ensure path starts with /uploads/ and prepend the API domain
    const path = avatarPath.startsWith("/uploads/") ? avatarPath : `/uploads/${avatarPath.replace(/^\/+/, "")}`;
    const apiBase = typeof window !== "undefined"
      ? (process.env.NEXT_PUBLIC_API_URL || window.location.origin)
      : (process.env.NEXT_PUBLIC_API_URL || "");
    return `${apiBase}${path}`;
  }
  return googleAvatarUrl || null;
}

// ─── File ──────────────────────────────────────────────────

/**
 * Format file size in bytes to human-readable string.
 */
export function formatFileSize(bytes: number): string {
  if (bytes === 0) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0)} ${units[i]}`;
}

/**
 * Validate file type against allowed extensions.
 */
export function isValidFileType(file: File, allowed: string[]): boolean {
  const ext = file.name.split(".").pop()?.toLowerCase();
  return ext ? allowed.includes(`.${ext}`) : false;
}

// ─── Color ─────────────────────────────────────────────────

/**
 * Convert hex to HSL components.
 */
export function hexToHsl(hex: string): { h: number; s: number; l: number } {
  hex = hex.replace("#", "");
  const r = parseInt(hex.substring(0, 2), 16) / 255;
  const g = parseInt(hex.substring(2, 4), 16) / 255;
  const b = parseInt(hex.substring(4, 6), 16) / 255;

  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  let h = 0;
  let s = 0;
  const l = (max + min) / 2;

  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    switch (max) {
      case r: h = ((g - b) / d + (g < b ? 6 : 0)) / 6; break;
      case g: h = ((b - r) / d + 2) / 6; break;
      case b: h = ((r - g) / d + 4) / 6; break;
    }
  }

  return {
    h: Math.round(h * 360),
    s: Math.round(s * 100),
    l: Math.round(l * 100),
  };
}

/**
 * Convert HSL to hex.
 */
export function hslToHex(h: number, s: number, l: number): string {
  s /= 100;
  l /= 100;
  const a = s * Math.min(l, 1 - l);
  const f = (n: number) => {
    const k = (n + h / 30) % 12;
    const color = l - a * Math.max(Math.min(k - 3, 9 - k, 1), -1);
    return Math.round(255 * color).toString(16).padStart(2, "0");
  };
  return `#${f(0)}${f(8)}${f(4)}`;
}

/**
 * Determine if text on a given background color should be white or black.
 */
export function getContrastColor(hex: string): string {
  const { l } = hexToHsl(hex);
  return l > 55 ? "#000000" : "#ffffff";
}

export function generateAccentVariants(hex: string) {
  const { h, s, l } = hexToHsl(hex);
  return {
    base: hex,
    hover: hslToHex(h, s, Math.max(l - 8, 0)),
    active: hslToHex(h, s, Math.max(l - 16, 0)),
    subtle: `${hex}18`,     // ~10% opacity
    subtleHover: `${hex}28`, // ~16% opacity
    contrast: getContrastColor(hex),
  };
}


/**
 * Get absolute URL for asset path.
 */
export function assetUrl(path: string | null): string {
  if (!path) return "";
  if (path.startsWith("http")) return path;
  const baseUrl = process.env.NEXT_PUBLIC_ASSET_URL || (process.env.NEXT_PUBLIC_API_URL || "").replace("/api", "/uploads");
  const cleanPath = path.replace(/^\/?uploads\/?/, "/");
  return `${baseUrl}${cleanPath}`.replace(/([^:]\/)\/+/g, "$1");
}

