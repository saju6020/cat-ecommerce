#!/usr/bin/env bash
# deploy/vm -- run on a DEVELOPER machine (it needs the private Platform.Infrastructure.* packages, already in the local NuGet
# cache or reachable through BRAC_NUGET_USERNAME/BRAC_NUGET_TOKEN). Builds the API for Linux and the migrations bundle into
# deploy/vm/out/ (git-ignored), then packs them as deploy/vm/out/api-build.tar.gz to copy to /opt/purrcare/ on the VM.
# usage: deploy/vm/scripts/publish-api.sh
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
out="$here/../out"
rm -rf "$out/api" "$out/efbundle" "$out/api-build.tar.gz"
cd "$root"
dotnet publish src/Api/CatEcommerce.Api/CatEcommerce.Api.csproj -c Release -r linux-x64 --self-contained false -o "$out/api" -nologo -v q
rm -f "$out/api/appsettings.Development.json"   # local-only settings never ship
dotnet ef migrations bundle --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api \
  --configuration Release --self-contained -r linux-x64 -o "$out/efbundle" --force
tar -czf "$out/api-build.tar.gz" -C "$out" api efbundle
echo "packed $out/api-build.tar.gz"
