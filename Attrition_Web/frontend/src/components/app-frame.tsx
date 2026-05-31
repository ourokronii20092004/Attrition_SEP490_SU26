"use client";

import { usePathname } from "next/navigation";
import dynamic from "next/dynamic";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { VerifyEmailBanner } from "@/components/verify-email-banner";

// The audio player is a heavy, client-only island (zustand state, range streaming, no SSR
// value). Lazy-load it so it doesn't ship in the initial bundle / first paint.
const AudioPlayer = dynamic(() => import("@/components/audio-player").then((m) => m.AudioPlayer), {
  ssr: false,
});

/**
 * Renders the public site chrome (header, footer, music player) for all routes
 * except /admin, which provides its own workspace shell via app/admin/layout.tsx.
 * Admins are NOT force-redirected off the public site — they're users too and can
 * browse/post/edit/delete their own content; they reach the panel via the header link.
 */

export function AppFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const isAdmin = pathname === "/admin" || pathname.startsWith("/admin/");

  if (isAdmin) return <>{children}</>;

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
