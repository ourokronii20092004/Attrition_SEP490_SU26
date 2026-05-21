"use client";

import { useAuth } from "@/contexts/AuthContext";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

/**
 * /profile → redirects to /profile/settings if logged in, or /login if not.
 */
export default function ProfileRedirect() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (isLoading) return;
    if (isAuthenticated) {
      router.replace("/profile/settings");
    } else {
      router.replace("/login?redirect=profile");
    }
  }, [isAuthenticated, isLoading, router]);

  return (
    <div className="page">
      <div className="container" style={{ display: "flex", justifyContent: "center", paddingTop: "20vh" }}>
        <span className="text-muted text-sm">Redirecting...</span>
      </div>
    </div>
  );
}
