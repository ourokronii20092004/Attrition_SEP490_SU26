/**
 * Public game build metadata for the download page.
 *
 * The build itself is NOT in this repository — an 81 MB binary would bloat the repo permanently
 * and sits near GitHub's 100 MB per-file limit. It lives in the Assets service upload volume
 * (`assets-data`), which `deploy.py` deliberately excludes from its project-dir wipe, so the file
 * survives deploys. Assets.Service serves that volume as static files under
 * `/api/assets/media`, and the gateway already routes `/api/assets/{**catch-all}`, so no new
 * route or nginx rule is needed.
 *
 * To publish a new build, copy it into the volume and bump the values here:
 *   docker cp Attrition_Game_0.9.4.rar attrition-assets:/app/uploads/builds/
 *
 * NEXT_PUBLIC_GAME_DOWNLOAD_URL overrides the location (e.g. to point at an external mirror if
 * the build outgrows self-hosting). Empty string disables the download and the page says the
 * build is being prepared, rather than offering a link that 404s.
 */
export const GAME_BUILD = {
  version: "0.9.4",
  channel: "Open Beta",
  /** Windows 64-bit only: the build ships a Unity Windows player (Attrion.exe). */
  platform: "Windows 10 / 11 (64-bit)",
  sizeLabel: "81.3 MB",
  /** sha256 of Attrition_Game.rar, so players can verify the archive after downloading. */
  sha256: "d093cf9c0831e50bed4b8eaca45d4d57498306d99c459a6056af871132e82edd",
  /** Archive format — needs 7-Zip / WinRAR, which Windows can't open natively. */
  archive: ".rar",
  /** Executable inside the archive. Spelled "Attrion", not "Attrition", in this build. */
  executable: "Attrion.exe",
  url: process.env.NEXT_PUBLIC_GAME_DOWNLOAD_URL ?? "/api/assets/media/builds/Attrition_Game_0.9.4.rar",
} as const;

/** False when no build is published, so the page can degrade instead of linking nowhere. */
export const GAME_BUILD_AVAILABLE = GAME_BUILD.url.length > 0;
