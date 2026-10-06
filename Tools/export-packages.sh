#!/bin/bash
# Exports the four release packages into Build/Packages/<version>/.
# Usage: Tools/export-packages.sh 2.4.0
#
# Each package is whole folders, except:
# - test folders, which need the Unity Test Framework that games may not have
# - Core/User/ThirdPartyAuthentication, which only the SocialAuthentication extension carries
# Never shipped: Soil/Demo, Plugins/Android templates, Assets/Resources, Assets/UI and the editor tools.
# Unity must not have this project open.
set -euo pipefail

VERSION=${1:?usage: Tools/export-packages.sh <version>}
PROJECT=$(cd "$(dirname "$0")/.." && pwd)
EDITOR_VERSION=$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")
UNITY=${UNITY:-/Applications/Unity/Hub/Editor/$EDITOR_VERSION/Unity.app/Contents/MacOS/Unity}
OUT=$PROJECT/Build/Packages/$VERSION

SOIL=Assets/FlyingAcorn/Soil
THIRD_PARTY=$SOIL/Core/User/ThirdPartyAuthentication
TESTS=($SOIL/Advertisement/Tests $SOIL/Economy/Tests $SOIL/Advertisement/Editor/Tests $SOIL/Socialization/Tests $SOIL/Feedback/Tests $SOIL/Push/Tests $THIRD_PARTY/GoogleCredentialUnity/Tests)

cd "$PROJECT"
mkdir -p "$OUT"

# Prints the path, or its children recursively when an excluded path lies inside it
expand() {
  local path=$1 excluded child
  for excluded in "${EXCLUDE[@]}"; do [[ $excluded == "$path" ]] && return; done
  for excluded in "${EXCLUDE[@]}"; do
    if [[ $excluded == "$path/"* ]]; then
      for child in "$path"/*; do [[ $child == *.meta ]] || expand "$child"; done
      return
    fi
  done
  printf '%s\n' "$path"
}

export_package() {
  local name=$1 root line paths=()
  shift
  for root in "$@"; do
    while IFS= read -r line; do paths+=("$line"); done < <(expand "$root")
  done
  "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$OUT/export-$name.log" \
    -exportPackage "${paths[@]}" "$OUT/Soil-$VERSION-$name.unitypackage"
  echo "Soil-$VERSION-$name.unitypackage: $(du -h "$OUT/Soil-$VERSION-$name.unitypackage" | cut -f1)"
}

BASE=(Assets/FlyingAcorn/Analytics $SOIL/Core Assets/Plugins/UniTask)

EXCLUDE=($THIRD_PARTY "${TESTS[@]}")
export_package BoosterPack "${BASE[@]}" $SOIL/CloudSave $SOIL/Economy $SOIL/Leaderboard $SOIL/RemoteConfig $SOIL/Socialization $SOIL/Feedback $SOIL/Push
export_package Purchasing "${BASE[@]}" $SOIL/Purchasing $SOIL/RemoteConfig $SOIL/Economy
export_package Advertisement "${BASE[@]}" $SOIL/Advertisement

EXCLUDE=("${TESTS[@]}")
export_package SocialAuthentication_Extension $THIRD_PARTY Assets/ExternalDependencyManager
