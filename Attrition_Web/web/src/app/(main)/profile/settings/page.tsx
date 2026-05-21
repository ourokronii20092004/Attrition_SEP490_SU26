"use client";

import { useState, useRef } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { useTheme, ACCENT_COLORS, type ThemeMode, type AccentName } from "@/contexts/ThemeContext";
import { useToast } from "@/contexts/ToastContext";
import { api } from "@/lib/api";
import { cn, getAvatarUrl, getInitials } from "@/lib/utils";
import { Camera, ArrowLeft } from "lucide-react";
import Link from "next/link";
import styles from "./settings.module.css";
import ImageCropper from "@/components/ImageCropper";

export default function SettingsPage() {
  const { user, isLoading, refreshUser, changePassword } = useAuth();
  const { mode, setMode, accent, setAccent } = useTheme();
  const toast = useToast();

  // Profile
  const [bio, setBio] = useState(user?.bio || "");
  const [email, setEmail] = useState(user?.email || "");
  const [savingProfile, setSavingProfile] = useState(false);

  // Avatar & Background
  const [uploadingAvatar, setUploadingAvatar] = useState(false);
  const [uploadingBg, setUploadingBg] = useState(false);
  const avatarInputRef = useRef<HTMLInputElement>(null);
  const bgInputRef = useRef<HTMLInputElement>(null);

  // Crop modal state
  const [cropFile, setCropFile] = useState<File | null>(null);
  const [cropShape, setCropShape] = useState<"circle" | "rectangle">("circle");
  const [cropTarget, setCropTarget] = useState<"avatar" | "background">("avatar");

  // Password
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [savingPassword, setSavingPassword] = useState(false);

  if (isLoading || !user) {
    return (
      <div className="page">
        <div className="container container-narrow">
          <div className="skeleton skeleton-heading" />
          <div className="skeleton" style={{ height: 200 }} />
        </div>
      </div>
    );
  }

  const avatarUrl = getAvatarUrl(user.avatarUrl);

  const handleAvatarSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 5 * 1024 * 1024) {
      toast.error("Image must be under 5MB");
      return;
    }

    setCropFile(file);
    setCropShape("circle");
    setCropTarget("avatar");
    if (avatarInputRef.current) avatarInputRef.current.value = "";
  };

  const uploadAvatarBlob = async (blob: Blob) => {
    setCropFile(null);
    setUploadingAvatar(true);
    try {
      const formData = new FormData();
      formData.append("file", blob, "avatar.png");
      const res = await api.upload<string>("/users/avatar", formData);
      if (res.success) {
        await refreshUser();
        toast.success("Avatar updated");
      } else {
        toast.error("Failed to upload avatar");
      }
    } catch {
      toast.error("Failed to upload avatar");
    } finally {
      setUploadingAvatar(false);
    }
  };

  const handleBgSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 10 * 1024 * 1024) {
      toast.error("Image must be under 10MB");
      return;
    }

    setCropFile(file);
    setCropShape("rectangle");
    setCropTarget("background");
    if (bgInputRef.current) bgInputRef.current.value = "";
  };

  const uploadBgBlob = async (blob: Blob) => {
    setCropFile(null);
    setUploadingBg(true);
    try {
      const formData = new FormData();
      formData.append("file", blob, "background.png");
      const res = await api.upload<string>("/users/background", formData);
      if (res.success) {
        await refreshUser();
        toast.success("Background updated");
      } else {
        toast.error("Failed to upload background");
      }
    } catch {
      toast.error("Failed to upload background");
    } finally {
      setUploadingBg(false);
    }
  };

  const handleDeleteAvatar = async () => {
    if (!confirm("Are you sure you want to remove your avatar?")) return;
    try {
      await api.delete("/users/avatar");
      await refreshUser();
      toast.success("Avatar removed");
    } catch {
      toast.error("Failed to remove avatar");
    }
  };

  const handleDeleteBg = async () => {
    if (!confirm("Are you sure you want to remove your background?")) return;
    try {
      await api.delete("/users/background");
      await refreshUser();
      toast.success("Background removed");
    } catch {
      toast.error("Failed to remove background");
    }
  };

  const handleSaveProfile = async () => {
    setSavingProfile(true);
    try {
      await api.put("/users/profile", { bio, email });
      await refreshUser();
      toast.success("Profile updated");
    } catch {
      toast.error("Failed to update profile");
    } finally {
      setSavingProfile(false);
    }
  };

  const handleChangePassword = async () => {
    if (newPassword !== confirmPassword) {
      toast.error("Passwords don't match");
      return;
    }
    if (newPassword.length < 6) {
      toast.error("Password must be at least 6 characters");
      return;
    }

    setSavingPassword(true);
    try {
      await changePassword(currentPassword, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      toast.success("Password changed");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to change password");
    } finally {
      setSavingPassword(false);
    }
  };

  return (
    <div className="page">
      <div className="container container-narrow">
        <div style={{ marginBottom: "var(--space-6)" }}>
          <Link href={`/profile/${user.username}`} style={{ display: "inline-flex", alignItems: "center", gap: 6, color: "var(--text-muted)", fontSize: "var(--text-sm)", textDecoration: "none" }}>
            <ArrowLeft size={14} /> Back to Profile
          </Link>
        </div>

        <div className="page-header">
          <h1>Settings</h1>
          <p>Manage your profile, account, and appearance.</p>
        </div>

        {/* Avatar & Profile */}
        <section className={styles.settingsSection}>
          <h2 className={styles.sectionTitle}>Profile</h2>
          <div className={styles.settingsCard}>
            {/* Avatar upload */}
            <div style={{ display: "flex", alignItems: "center", gap: "var(--space-5)", marginBottom: "var(--space-5)", flexWrap: "wrap" }}>
              <div style={{ position: "relative", flexShrink: 0 }}>
                <div style={{
                  width: 80, height: 80, borderRadius: "50%",
                  background: "var(--accent-subtle)", display: "flex", alignItems: "center",
                  justifyContent: "center", fontSize: "var(--text-xl)", fontWeight: "var(--weight-bold)",
                  color: "var(--accent)", overflow: "hidden"
                }}>
                  {avatarUrl ? (
                    <img src={avatarUrl} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                  ) : (
                    getInitials(user.displayName || user.username)
                  )}
                </div>
                <button
                  onClick={() => avatarInputRef.current?.click()}
                  disabled={uploadingAvatar}
                  style={{
                    position: "absolute", bottom: -2, right: -2,
                    width: 28, height: 28, borderRadius: "50%",
                    background: "var(--accent)", color: "white",
                    border: "2px solid var(--surface)", display: "flex",
                    alignItems: "center", justifyContent: "center",
                    cursor: "pointer", padding: 0
                  }}
                  title="Upload Avatar"
                >
                  <Camera size={13} />
                </button>
                <input
                  ref={avatarInputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/gif,image/webp"
                  style={{ display: "none" }}
                  onChange={handleAvatarSelect}
                />
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: "var(--weight-semibold)", marginBottom: 2 }}>
                  {user.displayName || user.username}
                </div>
                <div style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)", marginBottom: 8 }}>
                  Avatar: JPG, PNG, WebP · Max 5MB
                </div>
                <div style={{ display: "flex", gap: "var(--space-2)" }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => avatarInputRef.current?.click()} disabled={uploadingAvatar}>
                    {uploadingAvatar ? "Uploading..." : "Change Avatar"}
                  </button>
                  {user.avatarUrl && (
                    <button className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)" }} onClick={handleDeleteAvatar}>
                      Remove
                    </button>
                  )}
                </div>
              </div>
            </div>

            {/* Background upload */}
            <div style={{ marginBottom: "var(--space-5)" }}>
              <label className="input-label">Profile Background</label>
              <div style={{
                width: "100%", height: 120, borderRadius: "var(--radius-md)",
                background: user.backgroundUrl ? `url(${getAvatarUrl(user.backgroundUrl)}) center/cover` : "var(--bg-secondary)",
                border: "1px dashed var(--border)", display: "flex", alignItems: "center", justifyContent: "center",
                flexDirection: "column", gap: "var(--space-2)", position: "relative"
              }}>
                <div style={{ display: "flex", gap: "var(--space-2)", background: "rgba(0,0,0,0.5)", padding: "var(--space-2)", borderRadius: "var(--radius-md)" }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => bgInputRef.current?.click()} disabled={uploadingBg}>
                    <Camera size={14} style={{ marginRight: 6 }} />
                    {uploadingBg ? "Uploading..." : "Change Background"}
                  </button>
                  {user.backgroundUrl && (
                    <button className="btn btn-secondary btn-sm" style={{ color: "var(--text-danger)" }} onClick={handleDeleteBg}>
                      Remove
                    </button>
                  )}
                </div>
                <input
                  ref={bgInputRef}
                  type="file"
                  accept="image/jpeg,image/png,image/gif,image/webp"
                  style={{ display: "none" }}
                  onChange={handleBgSelect}
                />
              </div>
            </div>

            <div className="input-group" style={{ marginBottom: "var(--space-4)" }}>
              <label className="input-label">Email</label>
              <input
                type="email"
                className="input"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="your.email@example.com"
              />
            </div>

            <div className="input-group">
              <label className="input-label">Bio</label>
              <textarea
                className="input"
                value={bio}
                onChange={(e) => setBio(e.target.value)}
                placeholder="Tell us about yourself..."
                rows={4}
                maxLength={500}
              />
              <span style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)", textAlign: "right", display: "block", marginTop: 4 }}>
                {bio.length}/500
              </span>
            </div>
            <button
              className={cn("btn btn-primary btn-sm", savingProfile && "btn-loading")}
              onClick={handleSaveProfile}
              disabled={savingProfile}
            >
              Save Profile
            </button>
          </div>
        </section>

        {/* Password */}
        <section className={styles.settingsSection}>
          <h2 className={styles.sectionTitle}>Password</h2>
          <div className={styles.settingsCard}>
            <div className="input-group">
              <label className="input-label">Current Password</label>
              <input
                type="password"
                className="input"
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                autoComplete="current-password"
              />
            </div>
            <div className="input-group">
              <label className="input-label">New Password</label>
              <input
                type="password"
                className="input"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                autoComplete="new-password"
              />
            </div>
            <div className="input-group">
              <label className="input-label">Confirm New Password</label>
              <input
                type="password"
                className="input"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                autoComplete="new-password"
              />
            </div>
            <button
              className={cn("btn btn-primary btn-sm", savingPassword && "btn-loading")}
              onClick={handleChangePassword}
              disabled={savingPassword || !currentPassword || !newPassword}
            >
              Change Password
            </button>
          </div>
        </section>

        {/* Appearance */}
        <section className={styles.settingsSection}>
          <h2 className={styles.sectionTitle}>Appearance</h2>
          <div className={styles.settingsCard}>
            {/* Theme mode */}
            <div className={styles.settingRow}>
              <span className={styles.settingLabel}>Theme</span>
              <div className={styles.themeOptions}>
                {(["light", "dark", "system"] as ThemeMode[]).map((m) => (
                  <button
                    key={m}
                    className={cn(
                      styles.themeOption,
                      mode === m && styles.themeOptionActive
                    )}
                    onClick={() => setMode(m)}
                  >
                    <span className={styles.themeOptionIcon}>
                      {m === "light" ? "☀" : m === "dark" ? "☾" : "⚙"}
                    </span>
                    {m.charAt(0).toUpperCase() + m.slice(1)}
                  </button>
                ))}
              </div>
            </div>

            {/* Accent color */}
            <div className={styles.settingRow}>
              <span className={styles.settingLabel}>Accent Color</span>
              <div className={styles.accentGrid}>
                {(Object.keys(ACCENT_COLORS) as AccentName[]).map((name) => (
                  <button
                    key={name}
                    className={cn(
                      styles.accentOption,
                      accent === name && styles.accentOptionActive
                    )}
                    onClick={() => setAccent(name)}
                  >
                    <span
                      className={styles.accentDot}
                      style={{ background: ACCENT_COLORS[name].hex }}
                    />
                    <span className={styles.accentLabel}>
                      {ACCENT_COLORS[name].label}
                    </span>
                    <span className={styles.accentDescription}>
                      {ACCENT_COLORS[name].description}
                    </span>
                  </button>
                ))}
              </div>
            </div>
          </div>
        </section>
      </div>

      {/* Image Cropper Modal */}
      {cropFile && (
        <ImageCropper
          file={cropFile}
          shape={cropShape}
          aspectRatio={cropShape === "circle" ? 1 : 4.5}
          cropSize={cropShape === "circle" ? 280 : 440}
          onCrop={(blob) => {
            if (cropTarget === "avatar") {
              uploadAvatarBlob(blob);
            } else {
              uploadBgBlob(blob);
            }
          }}
          onCancel={() => setCropFile(null)}
        />
      )}
    </div>
  );
}
