"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import dynamic from "next/dynamic";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { VerifyEmailBanner } from "@/components/verify-email-banner";
import { PageLoader } from "@/components/ui/spinner";
import { useAuth } from "@/lib/providers";

// The audio player is a heavy, client-only island (zustand state, range streaming, no SSR
// value). Lazy-load it so it doesn't ship in the initial bundle / first paint.
const AudioPlayer = dynamic(() => import("@/components/audio-player").then((m) => m.AudioPlayer), {
  ssr: false,
});

/**
 * Renders the public site chrome (header, footer, music player) for all routes except /admin,
 * which provides its own workspace shell via app/admin/layout.tsx.
 *
 * PROB-6: admins do NOT browse the user side. An authenticated admin who lands on any public
 * (non-admin) route is redirected into the dashboard. They keep full reply/comment/post ability,
 * but they do it from dashboard surfaces (e.g. the admin thread view's "Reply as admin"), never
 * from the user-facing site. Logged-out visitors and normal users are unaffected.
 */

export function AppFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const { user } = useAuth();

  const isAdminRoute = pathname === "/admin" || pathname.startsWith("/admin/");
  // Auth routes must stay reachable so an admin can actually sign in / out / verify.
  const isAuthRoute =
    pathname.startsWith("/login") ||
    pathname.startsWith("/register") ||
    pathname.startsWith("/verify-email") ||
    pathname.startsWith("/forgot-password") ||
    pathname.startsWith("/reset-password") ||
    pathname.startsWith("/auth");

  const shouldRedirectAdmin = !!user && user.role === "Admin" && !isAdminRoute && !isAuthRoute;

  useEffect(() => {
    if (shouldRedirectAdmin) router.replace("/admin");
  }, [shouldRedirectAdmin, router]);

  if (isAdminRoute) return <>{children}</>;

  // While redirecting an admin off the user side, don't flash the public page underneath.
  if (shouldRedirectAdmin) return <PageLoader />;

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
