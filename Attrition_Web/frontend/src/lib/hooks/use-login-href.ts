"use client";

import { usePathname } from "next/navigation";
import { loginHref } from "@/lib/post-login-redirect";

/**
 * Sign-in href that returns to the current page afterwards.
 *
 * Every "Sign in" prompt should hand the user back to where they were — pressing it from deep in a
 * forum thread and landing on the homepage loses your place. Call sites spread this into a Link
 * instead of hardcoding "/login".
 *
 * Deliberately pathname-only: `useSearchParams` would force every page that shows a sign-in prompt
 * behind a Suspense boundary (or silently de-opt it out of static rendering), and none of these
 * prompts sit on a page whose query string is worth preserving through a login.
 */
export function useLoginHref(): string {
  const pathname = usePathname();
  return loginHref(pathname ?? "/");
}
