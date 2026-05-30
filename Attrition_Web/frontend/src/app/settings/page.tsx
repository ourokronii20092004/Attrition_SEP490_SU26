"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Sun, Moon } from "lucide-react";
import { useAuth } from "@/lib/providers";
import { accountApi } from "@/lib/api/account";
import { authApi } from "@/lib/api/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card } from "@/components/ui/card";
import { Avatar } from "@/components/ui/avatar";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { PageLoader } from "@/components/ui/spinner";

const ACCENTS: { name: string; color: string }[] = [
  { name: "corruption", color: "#38e8a0" },
  { name: "crimson", color: "#ff4365" },
  { name: "ember", color: "#ff7a45" },
  { name: "gold", color: "#e7b549" },
  { name: "azure", color: "#4d9bff" },
  { name: "violet", color: "#a274ff" },
  { name: "rose", color: "#ff5fa8" },
  { name: "cyan", color: "#2fd6e8" },
  { name: "amber", color: "#ffb02e" },
  { name: "sky", color: "#34b3f1" },
  { name: "bone", color: "#d8d2c0" },
];

export default function SettingsPage() {
  const { user, loading, refreshUser, logout, setUser } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) router.push("/login");
  }, [loading, user, router]);

  if (loading || !user) return <PageLoader />;

  return (
    <PageShell size="md">
      <PageTitle>Settings</PageTitle>

      <div className="space-y-6">
        <ProfileSection user={user} refreshUser={refreshUser} setUser={setUser} />
        <AvatarSection user={user} refreshUser={refreshUser} />
        <ThemeSection user={user} refreshUser={refreshUser} />
        <PasswordSection />
        <EmailSection user={user} refreshUser={refreshUser} />
        <DangerSection logout={logout} />
      </div>
    </PageShell>
  );
}

function SettingsCard({ title, children, danger }: { title: string; children: React.ReactNode; danger?: boolean }) {
  return (
    <Card className="p-5 sm:p-6">
      <h2 className={`text-lg font-semibold ${danger ? "text-danger" : "text-fg"}`}>{title}</h2>
      <div className="mt-4">{children}</div>
    </Card>
  );
}

function ProfileSection({ user, refreshUser, setUser }: any) {
  const [bio, setBio] = useState(user.bio ?? "");
  const [notifyOnReply, setNotifyOnReply] = useState(user.notifyOnReply ?? true);
  const [notifyOnMention, setNotifyOnMention] = useState(user.notifyOnMention ?? true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const save = async () => {
    setSaving(true);
    setError("");
    try {
      const res = await accountApi.updateProfile({ bio, notifyOnReply, notifyOnMention });
      if (res.success && res.data) setUser(res.data);
    } catch {
      setError("Failed to save profile. Please try again.");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Profile">
      <div className="space-y-4">
        <div className="space-y-1.5">
          <label className="block text-sm font-medium text-fg-muted">Bio</label>
          <textarea
            value={bio}
            onChange={(e) => setBio(e.target.value)}
            rows={3}
            className="w-full resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-fg outline-none transition-colors focus:border-accent focus:ring-1 focus:ring-accent"
          />
        </div>
        <div className="space-y-2">
          <label className="flex items-center gap-2 text-sm text-fg-muted">
            <input
              type="checkbox"
              checked={notifyOnReply}
              onChange={(e) => setNotifyOnReply(e.target.checked)}
              className="rounded border-border accent-accent"
            />
            Notify on replies
          </label>
          <label className="flex items-center gap-2 text-sm text-fg-muted">
            <input
              type="checkbox"
              checked={notifyOnMention}
              onChange={(e) => setNotifyOnMention(e.target.checked)}
              className="rounded border-border accent-accent"
            />
            Notify on mentions
          </label>
        </div>
        <Button onClick={save} loading={saving}>Save Profile</Button>
        {error && <p className="text-sm text-danger">{error}</p>}
      </div>
    </SettingsCard>
  );
}

function AvatarSection({ user, refreshUser }: any) {
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");

  const handleUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    setError("");
    try {
      await accountApi.uploadAvatar(file);
      await refreshUser();
    } catch {
      setError("Failed to upload avatar. Please try again.");
    }
    setUploading(false);
  };

  const handleDelete = async () => {
    setError("");
    try {
      await accountApi.deleteAvatar();
      await refreshUser();
    } catch {
      setError("Failed to remove avatar. Please try again.");
    }
  };

  return (
    <SettingsCard title="Avatar & Background">
      <div className="flex items-center gap-4">
        <Avatar src={user.avatarUrl} name={user.displayName ?? user.username} size="lg" />
        <div className="flex flex-wrap items-center gap-2">
          <label className="cursor-pointer rounded-lg border border-border-strong px-3 py-1.5 text-sm text-fg-muted transition-colors hover:border-accent/60 hover:text-fg">
            {uploading ? "Uploading..." : "Upload Avatar"}
            <input type="file" accept="image/*" onChange={handleUpload} className="hidden" />
          </label>
          {user.avatarUrl && (
            <button onClick={handleDelete} className="text-sm text-danger transition-opacity hover:opacity-80">Remove</button>
          )}
        </div>
      </div>
      {error && <p className="mt-2 text-sm text-danger">{error}</p>}
    </SettingsCard>
  );
}

function ThemeSection({ user, refreshUser }: any) {
  const [mode, setMode] = useState(user.themeMode ?? "dark");
  const [accent, setAccent] = useState(user.themeAccent ?? "corruption");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const save = async () => {
    setSaving(true);
    setError("");
    try {
      await accountApi.updateTheme({ themeMode: mode, themeAccent: accent });
      await refreshUser();
    } catch {
      setError("Failed to save theme. Please try again.");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Theme">
      <div className="space-y-5">
        <div>
          <p className="mb-2 text-sm font-medium text-fg-muted">Mode</p>
          <div className="grid max-w-xs grid-cols-2 gap-2">
            <button
              onClick={() => setMode("dark")}
              className={`flex items-center justify-center gap-2 rounded-lg border px-4 py-2.5 text-sm font-medium transition-colors ${mode === "dark" ? "border-accent bg-accent-soft text-accent" : "border-border text-fg-muted hover:text-fg"}`}
            >
              <Moon size={16} /> Dark
            </button>
            <button
              onClick={() => setMode("light")}
              className={`flex items-center justify-center gap-2 rounded-lg border px-4 py-2.5 text-sm font-medium transition-colors ${mode === "light" ? "border-accent bg-accent-soft text-accent" : "border-border text-fg-muted hover:text-fg"}`}
            >
              <Sun size={16} /> Light
            </button>
          </div>
        </div>
        <div>
          <p className="mb-2 text-sm font-medium text-fg-muted">Accent color</p>
          <div className="flex flex-wrap gap-2.5">
            {ACCENTS.map((a) => (
              <button
                key={a.name}
                onClick={() => setAccent(a.name)}
                title={a.name}
                aria-label={a.name}
                aria-pressed={accent === a.name}
                style={{ backgroundColor: a.color }}
                className={`h-9 w-9 rounded-full transition-transform duration-150 hover:scale-110 ${accent === a.name ? "ring-2 ring-fg ring-offset-2 ring-offset-surface" : ""}`}
              />
            ))}
          </div>
        </div>
        <Button onClick={save} loading={saving}>Save Theme</Button>
        {error && <p className="text-sm text-danger">{error}</p>}
      </div>
    </SettingsCard>
  );
}

function PasswordSection() {
  const [current, setCurrent] = useState("");
  const [newPw, setNewPw] = useState("");
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState("");

  const save = async () => {
    setSaving(true);
    setMsg("");
    try {
      await authApi.changePassword({ currentPassword: current, newPassword: newPw });
      setMsg("Password updated");
      setCurrent("");
      setNewPw("");
    } catch {
      setMsg("Failed to update password");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Password">
      <div className="space-y-3">
        <Input label="Current Password" type="password" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} />
        <Input label="New Password" type="password" autoComplete="new-password" value={newPw} onChange={(e) => setNewPw(e.target.value)} />
        {msg && <p className="text-sm text-fg-muted">{msg}</p>}
        <Button onClick={save} loading={saving} disabled={!current || !newPw}>Change Password</Button>
      </div>
    </SettingsCard>
  );
}

function EmailSection({ user, refreshUser }: any) {
  const [email, setEmail] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState("");

  const save = async () => {
    setSaving(true);
    setMsg("");
    try {
      await accountApi.updateEmail({ newEmail: email, currentPassword });
      setMsg("Verification email sent to new address");
      await refreshUser();
    } catch {
      setMsg("Failed to update email");
    }
    setSaving(false);
  };

  return (
    <SettingsCard title="Email">
      <p className="-mt-2 text-sm text-fg-muted">Current: {user.email}</p>
      <div className="mt-4 space-y-3">
        <Input label="New Email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        <Input label="Current Password" type="password" autoComplete="current-password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} />
        {msg && <p className="text-sm text-fg-muted">{msg}</p>}
        <Button onClick={save} loading={saving} disabled={!email || !currentPassword}>Update Email</Button>
      </div>
    </SettingsCard>
  );
}

function DangerSection({ logout }: { logout: () => void }) {
  const [confirming, setConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState("");

  const handleDelete = async () => {
    setDeleting(true);
    setError("");
    try {
      await accountApi.deleteAccount();
      logout();
    } catch {
      setError("Failed to delete account. Please try again.");
      setDeleting(false);
    }
  };

  return (
    <SettingsCard title="Danger Zone" danger>
      {!confirming ? (
        <Button variant="danger" onClick={() => setConfirming(true)}>Delete Account</Button>
      ) : (
        <div className="space-y-3">
          <p className="text-sm text-fg-muted">This action is irreversible. All your data will be permanently removed. Are you sure?</p>
          <div className="flex gap-2">
            <Button variant="danger" onClick={handleDelete} loading={deleting}>Yes, Delete</Button>
            <Button variant="secondary" onClick={() => setConfirming(false)}>Cancel</Button>
          </div>
          {error && <p className="text-sm text-danger">{error}</p>}
        </div>
      )}
    </SettingsCard>
  );
}
