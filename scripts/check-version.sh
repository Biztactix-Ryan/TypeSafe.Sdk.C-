#!/bin/sh
# Check that the SDK version is the same in all three places it is written.
#
#   scripts/check-version.sh            # both csproj <Version>s must equal TypeSafeConstants.Version
#   scripts/check-version.sh 0.7.0      # ...and all three must equal the given version
#
# Exits 0 on success, 1 on any mismatch or if a value cannot be found.

set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/.." && pwd)

csproj="$repo_root/src/TypeSafe.Sdk/TypeSafe.Sdk.csproj"
constants="$repo_root/src/TypeSafe.Sdk/Constants.cs"
di_csproj="$repo_root/src/TypeSafe.Sdk.DependencyInjection/TypeSafe.Sdk.DependencyInjection.csproj"

expected=${1:-}

fail() {
    echo "check-version: $1" >&2
    exit 1
}

[ -f "$csproj" ] || fail "not found: $csproj"
[ -f "$constants" ] || fail "not found: $constants"
[ -f "$di_csproj" ] || fail "not found: $di_csproj"

csproj_version=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$csproj" | head -n 1)
constants_version=$(grep -E 'const[[:space:]]+string[[:space:]]+Version[[:space:]]*=' "$constants" \
    | sed -n 's/.*=[[:space:]]*"\([^"]*\)".*/\1/p' | head -n 1)
di_version=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$di_csproj" | head -n 1)

[ -n "$csproj_version" ] || fail "no <Version> element in $csproj"
[ -n "$constants_version" ] || fail "no TypeSafeConstants.Version in $constants"
[ -n "$di_version" ] || fail "no <Version> element in $di_csproj"

echo "TypeSafe.Sdk.csproj                     <Version>                 = $csproj_version"
echo "Constants.cs                            TypeSafeConstants.Version = $constants_version"
echo "TypeSafe.Sdk.DependencyInjection.csproj <Version>                 = $di_version"

if [ "$csproj_version" != "$constants_version" ]; then
    fail "version mismatch: csproj is $csproj_version but TypeSafeConstants.Version is $constants_version. Update all three to the same value."
fi

if [ "$csproj_version" != "$di_version" ]; then
    fail "version mismatch: csproj is $csproj_version but TypeSafe.Sdk.DependencyInjection.csproj is $di_version. Update all three to the same value."
fi

if [ -n "$expected" ] && [ "$csproj_version" != "$expected" ]; then
    fail "version mismatch: expected $expected but the tree is at $csproj_version. Update all three files to $expected."
fi

if [ -n "$expected" ]; then
    echo "check-version: OK - all three values are $csproj_version (matches the expected $expected)"
else
    echo "check-version: OK - all three values are $csproj_version"
fi
