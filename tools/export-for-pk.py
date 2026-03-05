#!/usr/bin/env python3
"""SYNC-STATUS.md を読み取り、対象ファイルを shared/ にPK用ファイル名でコピーする"""

import re
import shutil
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SYNC_STATUS = REPO_ROOT / "docs" / "shared" / "SYNC-STATUS.md"
EXPORT_DIR = REPO_ROOT / "shared"


def parse_sync_status():
    """SYNC-STATUS.md のテーブルからファイル一覧を抽出"""
    text = SYNC_STATUS.read_text(encoding="utf-8")
    entries = []
    for line in text.splitlines():
        # テーブル行: | ファイル名 | `パス` | PK名 | ... |
        m = re.match(r"\|\s*[^|]+\|\s*`([^`]+)`\s*\|\s*([^|]+?)\s*\|", line)
        if m:
            repo_path = m.group(1).strip()
            pk_name = m.group(2).strip()
            if repo_path and pk_name and not repo_path.startswith("リポジトリ"):
                entries.append((repo_path, pk_name))
    return entries


def main():
    entries = parse_sync_status()
    if not entries:
        print("対象ファイルが見つかりませんでした")
        return

    EXPORT_DIR.mkdir(exist_ok=True)

    print(f"エクスポート先: {EXPORT_DIR}")
    print("-" * 50)

    copied = 0
    for repo_path, pk_name in entries:
        src = REPO_ROOT / repo_path
        dst = EXPORT_DIR / pk_name
        if src.exists():
            shutil.copy2(src, dst)
            print(f"  OK {repo_path} -> shared/{pk_name}")
            copied += 1
        else:
            print(f"  NG {repo_path} (file not found)")

    print("-" * 50)
    print(f"完了: {copied}/{len(entries)} ファイルをコピー")


if __name__ == "__main__":
    main()
