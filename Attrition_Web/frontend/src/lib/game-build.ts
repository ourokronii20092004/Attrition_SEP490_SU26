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
 * The runtime source of truth is a JSON manifest in the same volume, served at MANIFEST_URL.
 * Publishing a new build means uploading the archive and adding one entry to that file on the
 * server — no frontend redeploy:
 *   scp Attrition_Game_1.0.3.zip root@192.168.1.110:/tmp/
 *   ssh root@192.168.1.110 'docker cp /tmp/Attrition_Game_1.0.3.zip attrition-assets:/app/uploads/builds/ && rm /tmp/Attrition_Game_1.0.3.zip'
 *   ...then add the entry to builds.json (attrition-assets:/app/uploads/builds/builds.json).
 *
 * The newest entry is always the hero download. Older entries stay listed so a player on a slower
 * machine — or one mid-run on an old save — can still get the build they were playing.
 *
 * GAME_BUILDS below is the bundled fallback: it renders while the manifest is loading, and if the
 * fetch fails, so the page never goes blank. Keep it roughly in sync with the live manifest.
 */
export interface GameBuild {
  version: string;
  channel: string;
  /** Windows 64-bit only: every build ships a Unity Windows player. */
  platform: string;
  sizeLabel: string;
  /** sha256 of the archive, so players can verify it after downloading. */
  sha256?: string;
  /** Archive extension. `.zip` opens natively on Windows; `.rar` needs 7-Zip / WinRAR. */
  archive: string;
  /** Executable inside the archive. */
  executable: string;
  url: string;
  /** ISO date the build was published — drives the "released" line on the download list. */
  released?: string;
}

/** Where the live manifest is served from (Assets.Service static files, gateway-routed). */
export const MANIFEST_URL = "/api/assets/media/builds/builds.json";

/** Bundled fallback, newest first — mirrors the manifest as of the last deploy. */
export const GAME_BUILDS: readonly GameBuild[] = [
  {
    version: "1.0.2",
    channel: "Release",
    platform: "Windows 10 / 11 (64-bit)",
    sizeLabel: "90.1 MB",
    sha256: "526bdbbdb5e4f624b51258cb72ec622cee3a744370096574826955b459b9b285",
    archive: ".zip",
    executable: "Attrition_Game.exe",
    url: "/api/assets/media/builds/Attrition_Game_1.0.2.zip",
    released: "2026-08-08",
  },
  {
    version: "1.0.1",
    channel: "Release",
    platform: "Windows 10 / 11 (64-bit)",
    sizeLabel: "90.2 MB",
    sha256: "c4ac2e8e69453b0df72c417678ee23f2f51e0eba28a77e8a47b5dec5727ddcee",
    archive: ".zip",
    executable: "Attrition_Game.exe",
    url: "/api/assets/media/builds/Attrition_Game_1.0.1.zip",
    released: "2026-08-07",
  },
  {
    version: "1.0",
    channel: "Release",
    platform: "Windows 10 / 11 (64-bit)",
    sizeLabel: "90.3 MB",
    sha256: "229cfb0ecad7c18ec219f4af3b2686880e7bab781fcccefc7ce44656f14e8c2b",
    archive: ".zip",
    executable: "Attrition_Game.exe",
    url: "/api/assets/media/builds/Attrition_Game_1.0.zip",
    released: "2026-08-04",
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
    released: "2026-08-03",
  },
];

/** Numeric version comparison ("1.0.10" > "1.0.9"); non-numeric segments compare as 0. */
function compareVersions(a: string, b: string): number {
  const pa = a.split(".").map((n) => Number.parseInt(n, 10) || 0);
  const pb = b.split(".").map((n) => Number.parseInt(n, 10) || 0);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const d = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (d !== 0) return d;
  }
  return 0;
}

/** True when the value plausibly came from the manifest rather than a broken/garbage response. */
function isGameBuild(b: unknown): b is GameBuild {
  return (
    typeof b === "object" && b !== null &&
    typeof (b as GameBuild).version === "string" &&
    typeof (b as GameBuild).url === "string"
  );
}

/**
 * Fetch the live manifest and return it newest-first. Rejects (so the caller keeps the bundled
 * fallback) on network errors, non-JSON responses, or payloads with no usable entries.
 */
export async function fetchGameBuilds(): Promise<GameBuild[]> {
  const res = await fetch(MANIFEST_URL, { cache: "no-store" });
  if (!res.ok) throw new Error(`manifest ${res.status}`);
  const json: unknown = await res.json();
  if (!Array.isArray(json)) throw new Error("manifest is not an array");
  const builds = json.filter(isGameBuild);
  if (builds.length === 0) throw new Error("manifest has no builds");
  return builds.sort((a, b) => compareVersions(b.version, a.version));
}
