/**
 * Public game build metadata for the download page.
 *
 * The builds themselves are NOT in this repository — ~90 MB binaries would bloat the repo
 * permanently and sit near GitHub's 100 MB per-file limit. They live in the Assets service upload
 * volume (`assets-data`), which `deploy.py` deliberately excludes from its project-dir wipe, so the
 * files survive deploys. Assets.Service serves that volume as static files under
 * `/api/assets/media`, and the gateway already routes `/api/assets/{**catch-all}`, so no new route
 * or nginx rule is needed.
 *
 * To publish a new build, copy it into the volume and prepend an entry to GAME_BUILDS:
 *   docker cp Attrition_Game_1.0.zip attrition-assets:/app/uploads/builds/
 *
 * Older entries stay listed so a player on a slower machine — or one mid-run on an old save — can
 * still get the build they were playing.
 *
 * NEXT_PUBLIC_GAME_DOWNLOAD_URL overrides the current build's location (e.g. to point at an
 * external mirror if the build outgrows self-hosting). Empty string disables the download and the
 * page says the build is being prepared, rather than offering a link that 404s.
 */
export interface GameBuild {
  version: string;
  channel: string;
  /** Windows 64-bit only: every build ships a Unity Windows player. */
  platform: string;
  sizeLabel: string;
  /** sha256 of the archive, so players can verify it after downloading. */
  sha256: string;
  /** Archive extension. `.zip` opens natively on Windows; `.rar` needs 7-Zip / WinRAR. */
  archive: string;
  /** Executable inside the archive. */
  executable: string;
  url: string;
}

/** Newest first. GAME_BUILD (the download button) is always the head of this list. */
export const GAME_BUILDS: readonly GameBuild[] = [
  {
    version: "1.0",
    channel: "Release",
    platform: "Windows 10 / 11 (64-bit)",
    sizeLabel: "90.3 MB",
    sha256: "229cfb0ecad7c18ec219f4af3b2686880e7bab781fcccefc7ce44656f14e8c2b",
    archive: ".zip",
    executable: "Attrition_Game.exe",
    url: process.env.NEXT_PUBLIC_GAME_DOWNLOAD_URL ?? "/api/assets/media/builds/Attrition_Game_1.0.zip",
  },
  {
    version: "0.9.4",
    channel: "Open Beta",
    platform: "Windows 10 / 11 (64-bit)",
    sizeLabel: "81.3 MB",
    archive: ".rar",
    sha256: "d093cf9c0831e50bed4b8eaca45d4d57498306d99c459a6056af871132e82edd",
    /** Spelled "Attrion", not "Attrition", in this build — fixed in 1.0. */
    executable: "Attrion.exe",
    url: "/api/assets/media/builds/Attrition_Game_0.9.4.rar",
  },
];

/** The build the download button points at. */
export const GAME_BUILD = GAME_BUILDS[0];

/** Everything still downloadable but superseded. */
export const OLDER_BUILDS = GAME_BUILDS.slice(1);

/** False when no build is published, so the page can degrade instead of linking nowhere. */
export const GAME_BUILD_AVAILABLE = GAME_BUILD.url.length > 0;
