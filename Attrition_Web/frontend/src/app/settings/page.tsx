"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { PageLoader } from "@/components/ui/spinner";
import {
  ProfileSection, PasswordSection, EmailSection, DangerSection,
} from "@/components/account-sections";

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
        {/* Avatar lives on the profile page; theme lives in the navbar — not duplicated here (UIBD-6). */}
        <ProfileSection user={user} setUser={setUser} />
        <PasswordSection />
        <EmailSection user={user} refreshUser={refreshUser} />
        <DangerSection logout={logout} />
      </div>
    </PageShell>
  );
}
