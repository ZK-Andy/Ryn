#!/usr/bin/env bash
set -euo pipefail

# Downloads prebuilt saucer-bindings native libraries from the Ryn GitHub Releases.
# Falls back to building from source if download fails.
#
# Usage:
#   ./build/download-native.sh              # download for current platform
#   ./build/download-native.sh all          # download for all platforms
#   ./build/download-native.sh osx-arm64    # download for specific RID

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO="Yupmoh/Ryn"
API_BASE="https://api.github.com/repos/$REPO"
INTEROP_DIR="$REPO_ROOT/src/Ryn.Interop"

get_github_token() {
    if [[ -n "${GITHUB_TOKEN:-}" ]]; then echo "$GITHUB_TOKEN"; return; fi
    if [[ -n "${GH_TOKEN:-}" ]]; then echo "$GH_TOKEN"; return; fi

    local token
    token=$(printf 'protocol=https\nhost=github.com\n' | git credential fill 2>/dev/null | grep '^password=' | cut -d= -f2- || true)
    if [[ -n "$token" ]]; then echo "$token"; return; fi

    echo ""
}

auth_header() {
    local token
    token="$(get_github_token)"
    if [[ -n "$token" ]]; then
        echo "Authorization: Bearer $token"
    else
        echo "X-No-Auth: true"
    fi
}

detect_rid() {
    local os arch
    os="$(uname -s)"
    arch="$(uname -m)"

    case "$arch" in
        x86_64) arch="x64" ;;
        aarch64) arch="arm64" ;;
    esac

    case "$os" in
        Darwin) echo "osx-$arch" ;;
        Linux)  echo "linux-$arch" ;;
        MINGW*|MSYS*|CYGWIN*) echo "win-x64" ;;
        *) echo "unknown"; return 1 ;;
    esac
}

archive_ext() {
    case "$1" in
        win-*) echo ".zip" ;;
        *)     echo ".tar.gz" ;;
    esac
}

download_rid() {
    local rid="$1"
    local ext dest archive_name

    ext="$(archive_ext "$rid")"
    dest="$INTEROP_DIR/runtimes/$rid/native"
    archive_name="saucer-bindings-${rid}${ext}"

    mkdir -p "$dest"

    echo "==> Downloading $archive_name..."

    local auth_hdr
    auth_hdr="$(auth_header)"

    # Query releases via REST API
    local releases_json
    releases_json=$(curl -sS -H "$auth_hdr" -H "Accept: application/vnd.github+json" "$API_BASE/releases" 2>/dev/null) || {
        echo "    Failed to query GitHub API."
        return 1
    }

    # Find the asset URL — try all releases, prefer native-v* tagged ones
    local asset_url=""
    asset_url=$(echo "$releases_json" | python3 -c "
import json, sys
releases = json.load(sys.stdin)
for r in releases:
    for a in r.get('assets', []):
        if a['name'] == '$archive_name':
            print(a['url'])
            sys.exit(0)
sys.exit(1)
" 2>/dev/null) || true

    if [[ -z "$asset_url" ]]; then
        echo "    No release found containing $archive_name."
        return 1
    fi

    echo "    Found asset, downloading..."

    local tmp_file="/tmp/$archive_name"
    if ! curl -sS -L -H "$auth_hdr" -H "Accept: application/octet-stream" -o "$tmp_file" "$asset_url"; then
        echo "    Download failed."
        return 1
    fi

    echo "    Extracting to $dest..."
    if [[ "$ext" == ".zip" ]]; then
        unzip -o "$tmp_file" -d "$dest"
    else
        tar -xzf "$tmp_file" -C "$dest"
    fi

    rm -f "$tmp_file"
    echo "    Done: $(ls "$dest")"
}

ALL_RIDS=(osx-arm64 linux-x64 win-x64)

case "${1:-}" in
    all)
        echo "==> Downloading native libraries for all platforms..."
        for rid in "${ALL_RIDS[@]}"; do
            download_rid "$rid" || echo "    WARNING: Failed for $rid, skipping."
        done
        ;;
    "")
        rid="$(detect_rid)"
        if ! download_rid "$rid"; then
            echo ""
            echo "==> Download failed. Falling back to building from source..."
            exec "$SCRIPT_DIR/build-native.sh"
        fi
        ;;
    *)
        if ! download_rid "$1"; then
            echo ""
            echo "==> Download failed for $1."
            exit 1
        fi
        ;;
esac

echo ""
echo "==> Native libraries ready."
