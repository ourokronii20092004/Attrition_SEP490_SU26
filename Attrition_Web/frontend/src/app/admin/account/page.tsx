"use client";

import { useAuth } from "@/lib/providers";
import { PageLoader } from "@/components/ui/spinner";
import {
  ProfileSection, AvatarSection, ThemeSection, PasswordSection, EmailSection,
} from "@/components/account-sections";

export default function AdminAccountPage() {
  const { user, loading, refreshUser, setUser } = useAuth();

  if (loading) return <PageLoader />;
  if (!user) return null;

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="font-display text-3xl font-bold text-fg">My Account</h1>
      <p className="mt-2 text-fg-muted">Manage your administrator account.</p>
      <div className="mt-6 space-y-6">
        <ProfileSection user={user} setUser={setUser} />
        <AvatarSection user={user} refreshUser={refreshUser} />
        <ThemeSection />
        <PasswordSection />
        <EmailSection user={user} refreshUser={refreshUser} />
      </div>
    </div>
  );
}
