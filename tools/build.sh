#!/usr/bin/env bash
# 黑流树海 Mod 编译 + 打包脚本（guzhenren 模式：只构建并打包到本地 dist/，不写游戏目录）
# 部署由用户手动复制到 <游戏>/SlayTheSpire2.app/Contents/MacOS/mods/。
# 用法:
#   tools/build.sh [Configuration]            # 打包到 dist/（默认 Debug）
#   DIST_DIR=<自定义目录> tools/build.sh       # 指定打包输出目录
#   INSTALL_DIR=<游戏mods目录> tools/build.sh  # 附加：显式安装到指定目录（默认不写游戏目录）
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MOD_ID="TreeSeaMap"
CONFIGURATION="${1:-Debug}"
DIST_DIR="${DIST_DIR:-$ROOT_DIR/dist}"
OUTPUT_DIR="$DIST_DIR/$MOD_ID"
RITSULIB_SRC="$ROOT_DIR/../lib/STS2-RitsuLib"
RITSULIB_OUT="$DIST_DIR/STS2-RitsuLib"

echo "==> Building TreeSeaMap ($CONFIGURATION)"
dotnet build "$ROOT_DIR/TreeSeaMap.csproj" -c "$CONFIGURATION"

DLL_PATH="$ROOT_DIR/bin/$CONFIGURATION/net9.0/TreeSeaMap.dll"
if [[ ! -f "$DLL_PATH" ]]; then
  echo "DLL not found: $DLL_PATH" >&2
  exit 1
fi

mkdir -p "$OUTPUT_DIR"
echo "==> Packing to $OUTPUT_DIR/"
cp "$DLL_PATH" "$OUTPUT_DIR/$MOD_ID.dll"
cp "$ROOT_DIR/mod_manifest.json" "$OUTPUT_DIR/mod_manifest.json"

# RitsuLib 运行时依赖：mod_manifest 声明依赖它，一并打包让部署一次复制到位
if [[ -f "$RITSULIB_SRC/STS2-RitsuLib.dll" && -f "$RITSULIB_SRC/mod_manifest.json" ]]; then
  mkdir -p "$RITSULIB_OUT"
  cp "$RITSULIB_SRC/STS2-RitsuLib.dll" "$RITSULIB_OUT/STS2-RitsuLib.dll"
  cp "$RITSULIB_SRC/mod_manifest.json" "$RITSULIB_OUT/mod_manifest.json"
  echo "==> Packed RitsuLib runtime to $RITSULIB_OUT/"
else
  echo "==> 警告: 未找到本地 RitsuLib 运行时 ($RITSULIB_SRC)。"
  echo "     请从 GitHub Release (BAKAOLC/STS2-RitsuLib) 或 Steam Workshop 获取，否则 mod 无法加载。"
fi

echo
echo "==> 打包完成。手动部署到游戏（复制整个文件夹到 mods/ 下）："
GAME_MODS="<Slay the Spire 2>/SlayTheSpire2.app/Contents/MacOS/mods"
echo "    cp -R \"$OUTPUT_DIR\"    \"\$GAME_MODS/\""
echo "    cp -R \"$RITSULIB_OUT\"  \"\$GAME_MODS/\""

# 可选：INSTALL_DIR 显式指定安装目录（用户确认后使用，默认不写游戏目录）
if [[ -n "${INSTALL_DIR:-}" ]]; then
  mkdir -p "$INSTALL_DIR/$MOD_ID"
  cp "$OUTPUT_DIR/$MOD_ID.dll" "$INSTALL_DIR/$MOD_ID/$MOD_ID.dll"
  cp "$OUTPUT_DIR/mod_manifest.json" "$INSTALL_DIR/$MOD_ID/mod_manifest.json"
  if [[ -f "$RITSULIB_OUT/STS2-RitsuLib.dll" ]]; then
    mkdir -p "$INSTALL_DIR/STS2-RitsuLib"
    cp "$RITSULIB_OUT/STS2-RitsuLib.dll" "$INSTALL_DIR/STS2-RitsuLib/STS2-RitsuLib.dll"
    cp "$RITSULIB_OUT/mod_manifest.json" "$INSTALL_DIR/STS2-RitsuLib/mod_manifest.json"
  fi
  echo "==> Installed to $INSTALL_DIR/"
fi
