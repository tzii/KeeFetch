#!/usr/bin/env bash
set -euo pipefail

# Linux builds target .NET Framework and compile against the KeePass 2.60 API.
if [[ "$(id -u)" -eq 0 ]]; then
  SUDO=()
else
  SUDO=(sudo)
fi

KEEPASS_VERSION="2.60"
KEEPASS_DIR="/opt/keepass/${KEEPASS_VERSION}"
# Published at https://keepass.info/integrity.html.
KEEPASS_ARCHIVE_SHA256="63872830ae78a0e9075cd32b61693ce1e719929b3124e4987b1f3dc42f31da74"
KEEPASS_MARKER="${KEEPASS_DIR}/.archive.sha256"

"${SUDO[@]}" apt-get update
"${SUDO[@]}" apt-get install --yes \
  ca-certificates \
  curl \
  jq \
  libgdiplus \
  mono-devel \
  python3-yaml \
  tar \
  unzip \
  xvfb

DOTNET_ROOT="/opt/dotnet"
if [[ ! -x "${DOTNET_ROOT}/dotnet" ]] || ! "${DOTNET_ROOT}/dotnet" --list-sdks | grep -q '^8\.'; then
  metadata="$(mktemp)"
  archive="$(mktemp --suffix=.tar.gz)"
  trap 'rm -f "$metadata" "$archive"' EXIT
  curl --fail --location --silent --show-error \
    "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json" \
    --output "$metadata"
  sdk="$(jq -r '.releases[0].sdks[] | .version as $version | .files[] | select(.rid == "linux-x64" and (.name | endswith("linux-x64.tar.gz"))) | [$version, .url, .hash] | @tsv' "$metadata" | head -n 1)"
  IFS=$'\t' read -r sdk_version sdk_url sdk_sha512 <<< "$sdk"
  test -n "$sdk_version"
  test -n "$sdk_url"
  test -n "$sdk_sha512"
  curl --fail --location --silent --show-error "$sdk_url" --output "$archive"
  printf '%s  %s\n' "$sdk_sha512" "$archive" | sha512sum --check --status
  "${SUDO[@]}" install --directory "$DOTNET_ROOT"
  "${SUDO[@]}" tar --extract --gzip --file "$archive" --directory "$DOTNET_ROOT"
  rm -f "$metadata" "$archive"
  trap - EXIT
fi
"${SUDO[@]}" ln -sfn "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet

if ! command -v pwsh >/dev/null; then
  release="$(. /etc/os-release && printf '%s' "$VERSION_ID")"
  package="$(mktemp --suffix=.deb)"
  trap 'rm -f "$package"' EXIT
  curl --fail --location --silent --show-error \
    "https://packages.microsoft.com/config/ubuntu/${release}/packages-microsoft-prod.deb" \
    --output "$package"
  "${SUDO[@]}" dpkg --install "$package"
  "${SUDO[@]}" apt-get update
  "${SUDO[@]}" apt-get install --yes powershell
  rm -f "$package"
  trap - EXIT
fi

if [[ ! -f "${KEEPASS_DIR}/KeePass.exe" ]] || [[ ! -f "$KEEPASS_MARKER" ]] || \
   [[ "$(tr -d '[:space:]' < "$KEEPASS_MARKER")" != "$KEEPASS_ARCHIVE_SHA256" ]]; then
  archive="$(mktemp --suffix=.zip)"
  extract_dir="$(mktemp --directory)"
  trap 'rm -f "$archive"; rm -rf "$extract_dir"' EXIT
  curl --fail --location --silent --show-error \
    "https://sourceforge.net/projects/keepass/files/KeePass%202.x/${KEEPASS_VERSION}/KeePass-${KEEPASS_VERSION}.zip/download" \
    --output "$archive"
  echo "${KEEPASS_ARCHIVE_SHA256}  ${archive}" | sha256sum --check --status
  unzip -q "$archive" -d "$extract_dir"
  test -f "${extract_dir}/KeePass.exe"
  "${SUDO[@]}" install --directory "$KEEPASS_DIR"
  "${SUDO[@]}" cp -a "${extract_dir}/." "$KEEPASS_DIR/"
  printf '%s\n' "$KEEPASS_ARCHIVE_SHA256" | "${SUDO[@]}" tee "$KEEPASS_MARKER" >/dev/null
  rm -f "$archive"
  rm -rf "$extract_dir"
  trap - EXIT
fi

dotnet --list-sdks | grep -q '^8\.'
mono --version | head -n 1
pwsh --version
test -f "${KEEPASS_DIR}/KeePass.exe"
printf 'Sandbox dependencies ready. Build with: -p:KeePassPath=%s\n' "$KEEPASS_DIR"
