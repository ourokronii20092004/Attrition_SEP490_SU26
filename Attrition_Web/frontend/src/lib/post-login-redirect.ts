/**
 * Post-login navigation: send people back where they were.
 *
 * Pressing "Sign in" from halfway down a forum thread used to land you on the homepage, losing
 * your place. Every sign-in entry point now carries the current path, and the login page returns
 * you to it.
 *
 * Two transports, because the two flows differ:
 *  - Username/password stays on the page, so the destination rides in `?redirect=`.
 *  - Google leaves the site entirely and its callback lands on `/`, so the destination is stashed
 *    in sessionStorage and consumed when the app reloads authenticated.
 */

const PENDING_KEY = "attrition:post-login-redirect";

/** Control characters, which could smuggle a scheme past the same-origin checks. */
const CONTROL_CHARS = /[\u0000-\u001F\u007F]/;

/**
 * Whether a redirect target is safe to navigate to.
 *
 * Only same-origin, path-absolute URLs pass. Rejecting anything else is what stops `?redirect=`
 * from becoming an open redirect that bounces users to an attacker's lookalike login page:
 *  - `//evil.com` and `https://evil.com` are off-origin.
 *  - a backslash form like `/\evil.com` is read as `//evil.com` by some browsers.
 *  - `/login` and `/register` would bounce straight back into the flow we just finished.
 */
export function isSafeRedirect(target: string | null | undefined): target is string {
  if (!target) return false;
  const path = target.trim();
  if (!path.startsWith("/")) return false;
  if (path.startsWith("//")) return false;
  if (path.includes("\\")) return false;
  if (CONTROL_CHARS.test(path)) return false;
  const route = path.split(/[?#]/)[0];
  return !["/login", "/register", "/logout"].includes(route);
}

/** `?redirect=` value for a sign-in link, or "" when the current page isn't worth returning to. */
export function redirectParam(pathname: string, search = ""): string {
  const full = `${pathname}${search}`;
  return isSafeRedirect(full) ? `?redirect=${encodeURIComponent(full)}` : "";
}

/** Sign-in href that returns to `pathname` afterwards. */
export function loginHref(pathname: string, search = ""): string {
  return `/login${redirectParam(pathname, search)}`;
}

/** Where to go after signing in: the requested target if it's safe, else the homepage. */
export function safeRedirectTarget(target: string | null | undefined): string {
  return isSafeRedirect(target) ? target : "/";
}

/**
 * Remember where to return after an off-site auth round-trip (Google).
 * Uses sessionStorage so it dies with the tab and can't strand a later visit.
 */
export function stashPendingRedirect(target: string): void {
  if (typeof window === "undefined" || !isSafeRedirect(target)) return;
  try {
    window.sessionStorage.setItem(PENDING_KEY, target);
  } catch {
    // Private browsing / storage disabled: fall back to landing on the homepage.
  }
}

/** Read and clear the stashed destination. Returns null when there is nothing pending. */
export function takePendingRedirect(): string | null {
  if (typeof window === "undefined") return null;
  try {
    const target = window.sessionStorage.getItem(PENDING_KEY);
    if (target) window.sessionStorage.removeItem(PENDING_KEY);
    return isSafeRedirect(target) ? target : null;
  } catch {
    return null;
  }
}
