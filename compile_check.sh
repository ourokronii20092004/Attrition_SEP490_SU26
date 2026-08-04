#!/usr/bin/env bash
# Compile-check Attrition_Game asmdefs without opening the Unity Editor.
# See memory: reference_unity_compile_check.md
set -u

GAME="d:/ALL CODE/PJUnity/Đồ Án/Attrition_SEP490_SU26/Attrition_Game"
UNITY="D:/ALL CODE/unity/6000.3.15f1/Editor/Data"
CSC="/c/Program Files/dotnet/sdk/10.0.302/Roslyn/bincore/csc.dll"
OUT="/tmp/attrition_cc"
mkdir -p "$OUT"
cd "$GAME" || exit 1

REFS=()
for d in "$UNITY/Managed/UnityEngine"/*.dll; do REFS+=("-r:$d"); done
REFS+=("-r:$UNITY/NetStandard/ref/2.1.0/netstandard.dll")
for d in Assets/Photon/Fusion/Assemblies/*.dll; do REFS+=("-r:$d"); done
for d in Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll; do REFS+=("-r:$d"); done

# Assemblies we rebuild ourselves, in dependency order.
declare -a NAMES=(Attrition.Core Attrition.Data Attrition.Systems Attrition.Persistence Attrition.Networking Attrition.Gameplay Attrition.UI)
declare -a DIRS=(Core Data Systems Persistence Networking Gameplay UI)

# Everything else in ScriptAssemblies stays as a prebuilt reference (skip the ones we rebuild).
for d in Library/ScriptAssemblies/*.dll; do
  base=$(basename "$d" .dll)
  skip=0
  for n in "${NAMES[@]}"; do [ "$base" = "$n" ] && skip=1 && break; done
  [ $skip -eq 0 ] && REFS+=("-r:$d")
done

FLAGS=(-target:library -langversion:9.0 -nostdlib+ -noconfig -unsafe+ -nologo)

fail=0
for i in "${!NAMES[@]}"; do
  name="${NAMES[$i]}"
  dir="Assets/_Project/Scripts/${DIRS[$i]}"
  mapfile -t SRC < <(find "$dir" -name '*.cs')
  [ ${#SRC[@]} -eq 0 ] && continue

  # Reference the assemblies we already compiled this run.
  MYREFS=()
  for j in "${!NAMES[@]}"; do
    [ "$j" -ge "$i" ] && break
    [ -f "$OUT/${NAMES[$j]}.dll" ] && MYREFS+=("-r:$OUT/${NAMES[$j]}.dll")
  done

  echo "=== $name ==="
  dotnet "$CSC" "${FLAGS[@]}" "${REFS[@]}" "${MYREFS[@]}" \
    -out:"$OUT/$name.dll" "${SRC[@]}" 2>&1 \
    | grep -E "error CS" | sort -u | head -25
  rc=${PIPESTATUS[0]}
  [ "$rc" -ne 0 ] && fail=1 && echo "  ^ $name FAILED"
done
echo "=== done (fail=$fail) ==="
