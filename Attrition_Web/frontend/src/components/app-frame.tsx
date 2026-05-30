"use client";

import { usePathname } from "next/navigation";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { AudioPlayer } from "@/components/audio-player";

/**
 * Renders the public site chrome (header, footer, music player) for all routes
 * except /admin, which provides its own workspace shell via app/admin/layout.tsx.
 * Keeping this decision in one client component lets admin and the public site
 * diverge completely without moving every public page into a route group.
 */
export function AppFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const isAdmin = pathname === "/admin" || pathname.startsWith("/admin/");

  if (isAdmin) return <>{children}</>;

  return (
    <>
      <div className="flex min-h-dvh flex-col">
        <Header />
        <main id="main-content" className="flex-1">
          {children}
        </main>
        <Footer />
      </div>
      <AudioPlayer />
    </>
  );
}
