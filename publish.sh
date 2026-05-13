#!/usr/bin/env bash
# publish.sh - Build and package the Azure Databricks LINQPad driver (Linux/macOS)
# Usage: ./publish.sh [Release|Debug] [<version>]

set -euo pipefail

CONFIGURATION="${1:-Release}"
VERSION="${2:-0.1.0}"
ROOT="$(cd "$(dirname "$0")" && pwd)"
DIST="$ROOT/dist"
PUBLISH_DIR="$DIST/publish"

echo "Building LinqPad.Databricks.Driver v$VERSION ($CONFIGURATION)..."

# Clean
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

# Publish the driver
dotnet publish "$ROOT/src/LinqPad.Databricks.Driver/LinqPad.Databricks.Driver.csproj" \
    -c "$CONFIGURATION" \
    -o "$PUBLISH_DIR" \
    /p:Version="$VERSION" \
    --no-self-contained

# Create .LPX6 (zip renamed)
LPX6="$DIST/LinqPad.Databricks.Driver.$VERSION.LPX6"
rm -f "$LPX6"
(cd "$PUBLISH_DIR" && zip -r "$LPX6" .)
echo "Created: $LPX6"

# Build NuGet package
dotnet pack "$ROOT/src/LinqPad.Databricks.Driver/LinqPad.Databricks.Driver.csproj" \
    -c "$CONFIGURATION" \
    -o "$DIST" \
    /p:Version="$VERSION"

echo ""
echo "Artifacts in $DIST:"
ls -lh "$DIST"/*.LPX6 "$DIST"/*.nupkg 2>/dev/null || true
