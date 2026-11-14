"""検証記録（runtime-local）を D ドライブに置き、資料のフォルダからはつなぎ（ジャンクション）で書き込めるようにする。

使い方：python Scripts/link_runtime_local.py
- Docs/Design・Docs/Development の各フォルダに runtime-local のつなぎを作る（D 側のフォルダも作る）。
- すでに本物のフォルダがあれば、中身を D へ移してからつなぎに替える。
- 新しい工程のフォルダを作ったら、もう一度実行する。
置き場所は環境変数 HAKUTAI_RECORDS_DIR で変えられる（既定は D:\\02_プロジェクト別\\百代鏡\\検証記録）。
"""
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = Path(os.environ.get('HAKUTAI_RECORDS_DIR', 'D:\\02_プロジェクト別\\百代鏡\\検証記録'))


def is_link(p: Path) -> bool:
    return p.is_symlink() or (hasattr(os.path, 'isjunction') and os.path.isjunction(p))


def link(folder: Path) -> str:
    local = folder / 'runtime-local'
    target = DEST / local.relative_to(ROOT)
    if is_link(local):
        return 'ok'
    target.mkdir(parents=True, exist_ok=True)
    if local.exists():
        r = subprocess.run(['robocopy', str(local), str(target), '/E', '/MOVE', '/R:1', '/W:1',
                            '/NFL', '/NDL', '/NJH', '/NJS', '/NP'], capture_output=True)
        if r.returncode >= 8 or (local.exists() and any(local.iterdir())):
            return 'move-failed'
        if local.exists():
            local.rmdir()
    r = subprocess.run(['cmd', '/c', 'mklink', '/J', str(local), str(target)], capture_output=True)
    return 'linked' if r.returncode == 0 else 'link-failed'


def main() -> int:
    if not DEST.drive or not Path(DEST.drive + os.sep).exists():
        print('置き場所のドライブがありません:', DEST)
        return 1
    counts = {}
    for parent in ('Docs/Design', 'Docs/Development'):
        for folder in sorted((ROOT / parent).iterdir()):
            if folder.is_dir() and not is_link(folder):
                state = link(folder)
                counts[state] = counts.get(state, 0) + 1
                if state.endswith('failed'):
                    print(state, folder.relative_to(ROOT))
    print(counts)
    return 1 if any(k.endswith('failed') for k in counts) else 0


if __name__ == '__main__':
    sys.exit(main())
