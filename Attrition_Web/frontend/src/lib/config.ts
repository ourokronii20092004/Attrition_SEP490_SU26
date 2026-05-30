/**
 * Runtime configuration sourced from NEXT_PUBLIC_* env vars (baked at build time).
 */

function stripTrailingSlash(url: string): string {
  return url.endsWith("/") ? url.slice(0, -1) : url;
}

// In the browser, when served behind nginx the API shares the site origin, so an
// empty base ("" => same-origin relative /api/...) is correct. NEXT_PUBLIC_API_URL
// lets local dev point straight at the dockerized gateway (http://localhost:8080).
export const API_BASE = stripTrailingSlash(process.env.NEXT_PUBLIC_API_URL ?? "");

export const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID ?? "";

export const SITE_NAME = "Attrition";
export const SITE_TAGLINE = "Wiki, lore, and community for the Attrition universe";
