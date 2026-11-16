"""Docs/Design と Docs/Proposals の一覧（INDEX.md）を作り直す。Git に入っている資料だけを数える。"""
import os, subprocess

def title(path):
    try:
        with open(path, encoding='utf-8', errors='replace') as f:
            for line in f:
                if line.startswith('# '):
                    return line[2:].strip()
    except OSError:
        pass
    return ''

def main():
    tracked = set(subprocess.run(['git', 'ls-files', '-z', 'Docs'], capture_output=True, check=True).stdout.decode('utf-8').split('\0'))
    rows = []
    for d in sorted(os.listdir('Docs/Design')):
        p = 'Docs/Design/' + d
        files = [f for f in tracked if f.startswith(p + '/')]
        if not os.path.isdir(p) or not files:
            continue
        link = next((d + '/' + n for n in ('README.md', 'scope.md', 'entry-verification.md') if p + '/' + n in tracked), None)
        if link is None:
            md = sorted(f for f in files if f.endswith('.md'))
            link = md[0][len('Docs/Design/'):] if md else None
        rows.append(f'| [{d}]({link}) | {title("Docs/Design/" + link)} |' if link else f'| {d} | |')
    head = ['# 工程ごとの記録の一覧（Docs/Design）', '', '各工程の完成条件・検証・統合の記録。Scripts/make_docs_index.py で自動生成。手で書き足さない。', '', '| フォルダ | 見出し |', '|---|---|']
    with open('Docs/Design/INDEX.md', 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(head + rows) + '\n')
    rows = [f'| [{n}]({n}) | {title("Docs/Proposals/" + n)} |' for n in sorted(os.listdir('Docs/Proposals'))
            if n.endswith('.md') and n != 'INDEX.md' and 'Docs/Proposals/' + n in tracked]
    head = ['# 提案と具体化の一覧（Docs/Proposals）', '', '設計の提案・具体化の資料。採用済みかどうかは各資料と Docs/Decisions を見る。「履歴資料の移動案内」は本文を Docs/Archive へ移した印。Scripts/make_docs_index.py で自動生成。', '', '| ファイル | 見出し |', '|---|---|']
    with open('Docs/Proposals/INDEX.md', 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(head + rows) + '\n')

if __name__ == '__main__':
    main()
