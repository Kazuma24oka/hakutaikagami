"""Core-only engagement gate verification; not a new-game/3D acceptance claim."""
from pathlib import Path
import argparse
import collections
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'Docs/Design/FieldEngagementV1'
OUT = BASE / 'runtime-local'
FILTER = '|'.join('FullyQualifiedName~' + x for x in
                  ['FieldEngagementTests', 'ContactCombatTests', 'EngagementMissionTests'])
EXPECTED = {'FieldEngagementTests': 32, 'ContactCombatTests': 27, 'EngagementMissionTests': 21}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def inputs():
    files = [Path(__file__), ROOT / 'Tests/HakutaiKagami.Tests.csproj']
    for suffix in ('*.cs', '*.csproj'):
        files += [p for p in (ROOT / 'Core').rglob(suffix) if not {'bin', 'obj'}.intersection(p.parts)]
    files += [ROOT / ('Tests/' + name + '.cs') for name in EXPECTED]
    files += list((ROOT / 'Tests/Fixtures').glob('*.json'))
    return {p.relative_to(ROOT).as_posix(): sha(p) for p in sorted(set(files))}

def check(v):
    assert v['status'] == 'PASSED' and v['scope'] == 'core-only-engagement-gate'
    assert inputs() == v['sources'], 'Source inputs changed'
    for group in ('binaries', 'proofs'):
        for p, h in v[group].items():
            assert sha(ROOT / p) == h, p
    trx = ET.parse(OUT / 'tests/engagement.trx')
    c = trx.find('.//{*}Counters').attrib
    assert int(c['passed']) == sum(EXPECTED.values()) and c['failed'] == c['notExecuted'] == '0', c
    counts = collections.Counter(m.attrib['className'].rsplit('.', 1)[-1]
                                 for m in trx.findall('.//{*}TestMethod'))
    assert dict(counts) == EXPECTED, counts
    assert v['tests'] == dict(passed=80, failed=0, skipped=0, by_class=EXPECTED)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--check-only', action='store_true')
    args = parser.parse_args()
    manifest = BASE / 'verification.json'
    if args.check_only:
        check(json.loads(manifest.read_bytes()))
    else:
        OUT.mkdir(parents=True, exist_ok=True)
        sources = inputs()
        cmd = ['dotnet', 'test', 'Tests/HakutaiKagami.Tests.csproj', '--filter', FILTER,
               '--logger', 'trx;LogFileName=engagement.trx', '--results-directory',
               str(OUT / 'tests'), '--nologo', '--verbosity', 'minimal']
        proc = subprocess.run(cmd, cwd=ROOT, capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        (OUT / 'test.log').write_bytes(proc.stdout + proc.stderr)
        assert proc.returncode == 0, (OUT / 'test.log')
        assert inputs() == sources, 'Source changed during verification'
        v = dict(schema=1, status='PASSED', scope='core-only-engagement-gate',
                 baseline_revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                 tests=dict(passed=80, failed=0, skipped=0, by_class=EXPECTED),
                 new_game_integration='NOT_IMPLEMENTED', new_game_save_restore='NOT_RUN',
                 godot_3d='NOT_RUN', full_core_suite='NOT_RUN',
                 sources=sources,
                 binaries={p: sha(ROOT / p) for p in
                           ['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll',
                            'Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll']},
                 proofs={p.relative_to(ROOT).as_posix(): sha(p) for p in
                         [OUT / 'test.log', OUT / 'tests/engagement.trx']})
        manifest.write_text(json.dumps(v, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        check(v)
    print('PASS: 80 targeted Core tests; no new-game or 3D acceptance claimed')

if __name__ == '__main__':
    main()
