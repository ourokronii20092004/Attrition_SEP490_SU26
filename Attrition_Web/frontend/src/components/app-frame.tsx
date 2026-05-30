"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuth } from "@/lib/providers";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { AudioPlayer } from "@/components/audio-player";
import { VerifyEmailBanner } from "@/components/verify-email-banner";
import { PageLoader } from "@/components/ui/spinner";

/**
 * Renders the public site chrome (header, footer, music player) for all routes
 * except /admin, which provides its own workspace shell via app/admin/layout.tsx.
 * Keeping this decision in one client component lets admin and the public site
 * diverge completely without moving every public page into a route group.
 */

// Routes an Admin may still visit on the public side: their account, their own
// profile, and the auth/transactional pages. Everything else redirects to /admin.
const ADMIN_ALLOWED_PREFIXES = [
  "/settings", "/u/", "/login", "/register", "/logout",
  "/forgot-password", "/reset-password", "/verify-email", "/privacy", "/terms",
];

export function AppFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { user, loading } = useAuth();
  const isAdmin = pathname === "/admin" || pathname.startsWith("/admin/");

  const adminOnUserSide =
    !isAdmin && !loading && user?.role === "Admin" &&
    !ADMIN_ALLOWED_PREFIXES.some((p) => pathname === p || pathname.startsWith(p));

  useEffect(() => {
    if (adminOnUserSide) router.replace("/admin");
  }, [adminOnUserSide, router]);

  if (isAdmin) return <>{children}</>;

  // Hold the public page from flashing while we bounce an admin to their panel.
  if (adminOnUserSide) return <PageLoader />;

  return (
    <>
      <div className="flex min-h-dvh flex-col">
        <Header />
        <VerifyEmailBanner />
        <main id="main-content" className="flex-1">
          {children}
        </main>
        <Footer />
      </div>
      <AudioPlayer />
    </>
  );
}
