#!/usr/bin/env python3
"""Offline audit of the working tree. Unity's native import audit resolves package GUIDs."""
import argparse
import collections
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'HUS_Escape/Assets'


def audit():
    errors = []
    guid_paths = collections.defaultdict(list)
    files = [p for p in ASSETS.rglob('*') if p.is_file() and not any(x.startswith('.') for x in p.relative_to(ASSETS).parts)]
    for path in files:
        if path.suffix == '.meta':
            if not Path(str(path)[:-5]).exists():
                errors.append(f'Orphan meta: {path.relative_to(ROOT)}')
            match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(), re.M)
            if not match:
                errors.append(f'Invalid GUID: {path.relative_to(ROOT)}')
            else:
                guid_paths[match[1]].append(str(path.relative_to(ROOT)))
        elif not Path(str(path) + '.meta').is_file():
            errors.append(f'Missing meta: {path.relative_to(ROOT)}')
    for path in ASSETS.rglob('*'):
        if path.is_dir() and not any(x.startswith('.') for x in path.relative_to(ASSETS).parts) and not Path(str(path) + '.meta').is_file():
            errors.append(f'Missing folder meta: {path.relative_to(ROOT)}')
    for guid, paths in guid_paths.items():
        if len(paths) > 1:
            errors.append(f'Duplicate GUID {guid}: {paths}')
    refs = set()
    for path in files:
        if path.suffix in {'.unity', '.prefab', '.asset', '.mat', '.controller', '.meta'}:
            content = path.read_text(errors='replace')
            refs.update(re.findall(r'guid: ([0-9a-f]{32})', content))
            if re.search(r'm_Script: \{fileID: 0\}', content):
                errors.append(f'Null MonoBehaviour script: {path.relative_to(ROOT)}')
    external = sorted(g for g in refs - guid_paths.keys() if not g.startswith('0000000000000000'))
    tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0')
    sizes = [{'path': p, 'bytes': (ROOT / p).stat().st_size} for p in tracked if p and (ROOT / p).is_file()]
    sizes.sort(key=lambda v: v['bytes'], reverse=True)
    importable = [p for p in files if p.suffix != '.meta']
    for path in importable:
        with path.open('rb') as f:
            if f.read(100).startswith(b'version https://git-lfs.github.com/spec/v1'):
                errors.append(f'LFS pointer not materialized: {path.relative_to(ROOT)}')
    must_ignore = ['HUS_Escape/Library/check', 'HUS_Escape/Temp/check', 'HUS_Escape/Logs/check',
                   'HUS_Escape/UserSettings/check', 'HUS_Escape/Builds/check', 'HUS_Escape/local.slnx',
                   'HUS_Escape/.vscode/settings.json', 'tools/VT105/bin/check', 'tools/VT105/obj/check', 'artifacts/check']
    must_keep = ['HUS_Escape/Assets/_Project/Scripts/Utilities/JourneySession.cs',
                 'HUS_Escape/Assets/_Project/Scripts/Utilities/JourneySession.cs.meta',
                 'HUS_Escape/Packages/manifest.json', 'HUS_Escape/ProjectSettings/ProjectVersion.txt',
                 'tools/VT105/Harness.csproj', 'assignments/VT105/SaveSystem/SaveSlotService.cs']
    for path in must_ignore + must_keep:
        result = subprocess.run(['git', 'check-ignore', '--no-index', '-q', path], cwd=ROOT)
        if (result.returncode == 0) != (path in must_ignore):
            errors.append('Incorrect ignore behavior: ' + path)
    if any(p.startswith('HUS_Escape/Library/') or p.startswith('HUS_Escape/Temp/') for p in tracked):
        errors.append('Unity cache tracked in Git')
    if (ROOT / 'Assets/Scripts/SaveSystem').exists():
        errors.append('Legacy root SaveSystem still present')
    scene_text = (ROOT / 'HUS_Escape/ProjectSettings/EditorBuildSettings.asset').read_text()
    scenes = re.findall(r'- enabled: (\d)\s+path: (.+)\s+guid: ([0-9a-f]{32})', scene_text)
    for enabled, path, guid in scenes:
        if enabled != '1' or guid_paths.get(guid) != ['HUS_Escape/' + path + '.meta']:
            errors.append('Invalid build scene: ' + path)
    if not scenes or scenes[0][1] != 'Assets/_Project/Scenes/Bootstrap/Bootstrap.unity':
        errors.append('Bootstrap must be first build scene')
    return {'passed': not errors, 'errors': errors, 'asset_guid_count': len(guid_paths),
            'missing_or_duplicate_guid_count': len(errors), 'package_guids_requiring_native_resolution': external,
            'build_scenes': [p for _, p, _ in scenes], 'tracked_files': len(sizes),
            'tracked_bytes': sum(p['bytes'] for p in sizes),
            'importable_asset_bytes': sum(p.stat().st_size for p in importable),
            'top_tracked_files': sizes[:15], 'files_over_10_mib': [p for p in sizes if p['bytes'] >= 10 * 1024**2],
            'files_over_50_mib': [p for p in sizes if p['bytes'] > 50 * 1024**2]}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit()
    encoded = json.dumps(report, indent=2, ensure_ascii=False) + '\n'
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded)
        print(f"{'PASS' if report['passed'] else 'FAIL'} repository audit: {args.output}")
    else:
        print(encoded)
    raise SystemExit(0 if report['passed'] else 1)
