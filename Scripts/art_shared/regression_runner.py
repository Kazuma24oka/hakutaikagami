"""Dependency-bound local regression cache. No generation or visual approval.

Callers provide a complete dependency snapshot, including observed outputs,
criteria, code, environment and acceptance scope. Only successful exact matches
are reusable. A cached failure, missing dependency or changed input cannot pass.
"""
import hashlib
import json
import platform
import sys
import time
import os
from datetime import datetime, timezone
from importlib.metadata import version, PackageNotFoundError
from pathlib import Path


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False,
                                     allow_nan=False).encode('utf8')).hexdigest()


def file_digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def environment():
    packages = {}
    for name in ('numpy', 'Pillow', 'scipy'):
        try:
            packages[name] = version(name)
        except PackageNotFoundError:
            packages[name] = 'UNAVAILABLE'
    return {'python': sys.version, 'executable': str(Path(sys.executable).resolve()),
            'executable_sha256': file_digest(sys.executable),
            'platform': platform.platform(), 'packages': packages,
            'numerical_environment': {k: os.environ.get(k) for k in
                ('OMP_NUM_THREADS', 'MKL_NUM_THREADS', 'OPENBLAS_NUM_THREADS',
                 'MKL_CBWR', 'NPY_DISABLE_CPU_FEATURES', 'PYTHONOPTIMIZE')}}


def atomic_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    pending = path.with_suffix(path.suffix + '.pending')
    pending.write_text(json.dumps(value, ensure_ascii=False, indent=2,
                                  allow_nan=False) + '\n', encoding='utf8')
    pending.replace(path)


class RegressionCache:
    """One local cache file per profile; safe to delete and rebuild with --force."""
    def __init__(self, path):
        self.path = Path(path)
        try:
            stored = json.loads(self.path.read_text(encoding='utf8'))
            self.entries = stored['entries'] if stored['schema'] == 1 else {}
        except (OSError, ValueError, KeyError, TypeError):
            self.entries = {}
        if not isinstance(self.entries, dict):
            self.entries = {}

    def run(self, name, snapshot, check, *, force=False):
        start = time.perf_counter()
        fingerprint = None
        try:
            before = snapshot()  # Must check existence/content even on a cache hit.
            fingerprint = digest(before)
            previous = self.entries.get(name, {})
            if (not force and previous.get('status') == 'PASS'
                    and previous.get('fingerprint') == fingerprint
                    and previous.get('result_sha256') == digest(previous.get('result'))
                    and previous.get('dependencies') == before):
                return {**previous, 'execution': 'REUSED_EXACT_DEPENDENCIES',
                        'elapsed_seconds': round(time.perf_counter() - start, 3)}
            result = check()
            if not isinstance(result, dict) or result.get('status') != 'PASS':
                raise ValueError('Check must explicitly return scoped PASS')
            if digest(snapshot()) != fingerprint:
                raise ValueError('Dependencies changed during validation; result discarded')
            entry = {'status': 'PASS', 'fingerprint': fingerprint,
                     'dependencies': before, 'result': result,
                     'result_sha256': digest(result),
                     'validated_at_utc': datetime.now(timezone.utc).isoformat()}
        except Exception as error:
            entry = {'status': 'FAIL', 'fingerprint': fingerprint,
                     'error': type(error).__name__ + ': ' + str(error),
                     'validated_at_utc': datetime.now(timezone.utc).isoformat()}
        self.entries[name] = entry  # Failure replaces any earlier PASS.
        atomic_json(self.path, {'schema': 1, 'entries': self.entries})
        return {**entry, 'execution': 'EXECUTED',
                'elapsed_seconds': round(time.perf_counter() - start, 3)}
