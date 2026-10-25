#!/bin/bash

set -euo pipefail

ARCHITECTURE="arm64"
VERSION=""
UNSIGNED=false
SKIP_NOTARIZATION=false

usage() {
  echo "Usage: $0 --version X.Y.Z [--architecture arm64|x64] [--unsigned] [--skip-notarization]"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --architecture)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      ARCHITECTURE="$2"
      shift 2
      ;;
    --version)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      VERSION="$2"
      shift 2
      ;;
    --unsigned)
      UNSIGNED=true
      shift
      ;;
    --skip-notarization)
      SKIP_NOTARIZATION=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ "$ARCHITECTURE" != "arm64" && "$ARCHITECTURE" != "x64" ]]; then
  echo "Architecture must be arm64 or x64." >&2
  exit 2
fi

if [[ ! "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "A stable SemVer version is required in MAJOR.MINOR.PATCH format without leading zeroes." >&2
  exit 2
fi

if [[ "$UNSIGNED" == false ]]; then
  : "${MACOS_APPLICATION_IDENTITY:?Set MACOS_APPLICATION_IDENTITY to a Developer ID Application identity.}"
  : "${MACOS_INSTALLER_IDENTITY:?Set MACOS_INSTALLER_IDENTITY to a Developer ID Installer identity.}"
  if [[ "$SKIP_NOTARIZATION" == false ]]; then
    : "${APPLE_NOTARY_PROFILE:?Set APPLE_NOTARY_PROFILE to a notarytool keychain profile.}"
  fi
fi

for required_command in dotnet ditto plutil file find codesign lipo pkgbuild productbuild pkgutil shasum; do
  if ! command -v "$required_command" >/dev/null 2>&1; then
    echo "Required command not found: $required_command" >&2
    exit 1
  fi
done
if [[ "$UNSIGNED" == false && "$SKIP_NOTARIZATION" == false ]]; then
  for required_command in xcrun spctl; do
    if ! command -v "$required_command" >/dev/null 2>&1; then
      echo "Required command not found: $required_command" >&2
      exit 1
    fi
  done
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
RUNTIME_IDENTIFIER="osx-$ARCHITECTURE"
EXPECTED_MACH_ARCH="$ARCHITECTURE"
if [[ "$ARCHITECTURE" == "x64" ]]; then
  EXPECTED_MACH_ARCH="x86_64"
fi
HOST_PROJECT="$REPO_ROOT/src/GarantieExpress.Host/GarantieExpress.Host.csproj"
ARTIFACT_ROOT="$REPO_ROOT/artifacts/package/macos/$RUNTIME_IDENTIFIER"
PUBLISH_DIRECTORY="$ARTIFACT_ROOT/publish"
OUTPUT_DIRECTORY="$ARTIFACT_ROOT/installer"
OUTPUT_PACKAGE="$OUTPUT_DIRECTORY/GarantieExpress-$VERSION-$RUNTIME_IDENTIFIER.pkg"

PROJECT_VERSION="$(dotnet msbuild "$HOST_PROJECT" -getProperty:VersionPrefix)"
if [[ "$VERSION" != "$PROJECT_VERSION" ]]; then
  echo "Package version $VERSION must match the application version $PROJECT_VERSION." >&2
  exit 2
fi

WORK_DIRECTORY="$(mktemp -d "${TMPDIR:-/tmp}/garantieexpress.XXXXXX")"
cleanup() {
  case "${WORK_DIRECTORY:-}" in
    */garantieexpress.*) rm -rf -- "$WORK_DIRECTORY" ;;
  esac
}
trap cleanup EXIT

APP_BUNDLE="$WORK_DIRECTORY/GarantieExpress.app"
CONTENTS_DIRECTORY="$APP_BUNDLE/Contents"
MACOS_DIRECTORY="$CONTENTS_DIRECTORY/MacOS"
RESOURCES_DIRECTORY="$CONTENTS_DIRECTORY/Resources"
EXECUTABLE_PATH="$MACOS_DIRECTORY/GarantieExpress"
COMPONENT_PACKAGE="$WORK_DIRECTORY/GarantieExpress-component.pkg"
DISTRIBUTION_FILE="$WORK_DIRECTORY/Distribution.xml"

rm -rf -- "$ARTIFACT_ROOT"
mkdir -p "$PUBLISH_DIRECTORY" "$OUTPUT_DIRECTORY" "$MACOS_DIRECTORY" "$RESOURCES_DIRECTORY"

dotnet publish "$HOST_PROJECT" \
  --configuration Release \
  --runtime "$RUNTIME_IDENTIFIER" \
  --self-contained true \
  --output "$PUBLISH_DIRECTORY" \
  -p:PublishProfile="$RUNTIME_IDENTIFIER" \
  -p:Version="$VERSION"

ditto "$PUBLISH_DIRECTORY" "$MACOS_DIRECTORY"
mv "$MACOS_DIRECTORY/wwwroot" "$RESOURCES_DIRECTORY/wwwroot"
ln -s ../Resources/wwwroot "$MACOS_DIRECTORY/wwwroot"
sed "s/@@VERSION@@/$VERSION/g" "$SCRIPT_DIR/Info.plist.in" > "$CONTENTS_DIRECTORY/Info.plist"
plutil -lint "$CONTENTS_DIRECTORY/Info.plist"
sed "s/@@ARCHITECTURE@@/$EXPECTED_MACH_ARCH/g" \
  "$SCRIPT_DIR/Distribution.xml.in" > "$DISTRIBUTION_FILE"

if [[ ! -f "$EXECUTABLE_PATH" ]]; then
  echo "The publish output does not contain GarantieExpress." >&2
  exit 1
fi
chmod 755 "$EXECUTABLE_PATH"

if ! lipo -archs "$EXECUTABLE_PATH" | tr ' ' '\n' | grep -qx "$EXPECTED_MACH_ARCH"; then
  echo "GarantieExpress does not contain the expected $EXPECTED_MACH_ARCH architecture." >&2
  exit 1
fi

sign_runtime_payloads() {
  while IFS= read -r -d '' candidate; do
    if [[ "$candidate" == "$EXECUTABLE_PATH" ]]; then
      continue
    fi
    if [[ "$UNSIGNED" == true ]]; then
      codesign --force --sign - "$candidate"
    else
      codesign --force --options runtime --timestamp --sign "$MACOS_APPLICATION_IDENTITY" "$candidate"
    fi
  done < <(find "$MACOS_DIRECTORY" -type f -print0)
}

verify_runtime_payloads() {
  while IFS= read -r -d '' candidate; do
    codesign --verify --strict "$candidate"
  done < <(find "$MACOS_DIRECTORY" -type f -print0)
}

sign_runtime_payloads
if [[ "$UNSIGNED" == true ]]; then
  codesign --force \
    --entitlements "$SCRIPT_DIR/Entitlements.plist" \
    --sign - \
    "$APP_BUNDLE"
else
  codesign --force --options runtime --timestamp \
    --entitlements "$SCRIPT_DIR/Entitlements.plist" \
    --sign "$MACOS_APPLICATION_IDENTITY" \
    "$APP_BUNDLE"
fi
codesign --verify --strict "$APP_BUNDLE"
verify_runtime_payloads

pkgbuild \
  --component "$APP_BUNDLE" \
  --identifier "com.garantieexpress.app.pkg" \
  --version "$VERSION" \
  --install-location /Applications \
  "$COMPONENT_PACKAGE"

if [[ "$UNSIGNED" == true ]]; then
  productbuild \
    --distribution "$DISTRIBUTION_FILE" \
    --package-path "$WORK_DIRECTORY" \
    "$OUTPUT_PACKAGE"
else
  productbuild \
    --distribution "$DISTRIBUTION_FILE" \
    --package-path "$WORK_DIRECTORY" \
    --sign "$MACOS_INSTALLER_IDENTITY" \
    "$OUTPUT_PACKAGE"
  pkgutil --check-signature "$OUTPUT_PACKAGE"

  if [[ "$SKIP_NOTARIZATION" == false ]]; then
    xcrun notarytool submit "$OUTPUT_PACKAGE" \
      --keychain-profile "$APPLE_NOTARY_PROFILE" \
      --wait
    xcrun stapler staple "$OUTPUT_PACKAGE"
    xcrun stapler validate "$OUTPUT_PACKAGE"
    spctl --assess --type install --verbose=2 "$OUTPUT_PACKAGE"
  fi
fi

(
  cd "$OUTPUT_DIRECTORY"
  LC_ALL=C shasum -a 256 "$(basename "$OUTPUT_PACKAGE")" > "$(basename "$OUTPUT_PACKAGE").sha256"
)

echo "$OUTPUT_PACKAGE"
echo "$OUTPUT_PACKAGE.sha256"
