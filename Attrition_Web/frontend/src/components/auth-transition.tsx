"use client";

import { useAuth } from "@/lib/providers";
import { LoadingScreen } from "@/components/ui/loading-screen";

/**
 * Full-screen loader shown ONLY during an explicit login/logout (auth `transitioning`), never on
 * ordinary page loads or navigation. Mounted at the app root so it covers every route.
 */
export function AuthTransition() {
  const { transitioning } = useAuth();
  if (!transitioning) return null;
  return <LoadingScreen fullscreen />;
}
