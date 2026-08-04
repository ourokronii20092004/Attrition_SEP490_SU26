#!/usr/bin/env bash
# Publish a game build so the /download page can serve it.
#
# The build is deliberately NOT in git: at ~90 MB it would bloat the repo permanently and sits
# near GitHub's 100 MB per-file limit. Instead it lives in the assets-data docker volume, which
# Assets.Service serves as static files under /api/assets/media, and which deploy.py excludes
# from its project-dir wipe — so the file survives deploys.
#
# Usage (on the host running docker compose):
#   ./scripts/publish-game-build.sh /path/to/Attrition_Game.zip 1.0
#
# The destination keeps the source file's extension, so .zip and .rar builds can coexist —
# older versions stay downloadable rather than being overwritten.
#
# Then confirm src/lib/game-build.ts has an entry pointing at the resulting filename.
set -euo pipefail

SRC=${1:-}
VERSION=${2:-}
CONTAINER=${CONTAINER:-attrition-assets}

if [[ -z "$SRC" || -z "$VERSION" ]]; then
  echo "usage: $0 <path-to-build> <version>   e.g. $0 ~/Attrition_Game.zip 1.0" >&2
  exit 64
fi
if [[ ! -f "$SRC" ]]; then
  echo "error: no such file: $SRC" >&2
  exit 66
fi
if ! docker inspect "$CONTAINER" >/dev/null 2>&1; then
  echo "error: container '$CONTAINER' not found. Start the stack, or set CONTAINER=<name>." >&2
  exit 69
fi

# Keep the source extension (.zip / .rar) rather than assuming one, so publishing a zip can't
# silently land under a .rar name that tells players to reach for WinRAR.
EXT=".${SRC##*.}"
DEST_NAME="Attrition_Game_${VERSION}${EXT}"
DEST_DIR=/app/uploads/builds

echo "Publishing $(basename "$SRC") as $DEST_NAME ..."
docker exec "$CONTAINER" mkdir -p "$DEST_DIR"
docker cp "$SRC" "$CONTAINER:${DEST_DIR}/${DEST_NAME}"

# Verify the copy landed intact — a truncated build is worse than a missing one, because the
# page would happily serve it.
LOCAL_SUM=$(sha256sum "$SRC" | cut -d' ' -f1)
REMOTE_SUM=$(docker exec "$CONTAINER" sha256sum "${DEST_DIR}/${DEST_NAME}" | cut -d' ' -f1)
if [[ "$LOCAL_SUM" != "$REMOTE_SUM" ]]; then
  echo "error: checksum mismatch after copy (local $LOCAL_SUM != remote $REMOTE_SUM)" >&2
  exit 65
fi

echo "Published OK."
echo "  sha256: $LOCAL_SUM"
echo "  served at: /api/assets/media/builds/${DEST_NAME}"
echo
echo "Add/update the v${VERSION} entry in Attrition_Web/frontend/src/lib/game-build.ts with the"
echo "sha256 above (newest build first), then rebuild the web image so the page picks it up:"
echo "  docker compose build web && docker compose up -d web"
