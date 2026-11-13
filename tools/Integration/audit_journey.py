#!/usr/bin/env python3
"""Source audit; Unity's AssetDatabase probe remains authoritative for imported assets."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'HUS_Escape'
index = {}
for meta in (PROJECT / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
    if match:
        index.setdefault(match[1], []).append(meta)
errors = [f'duplicate GUID {key}: {paths}' for key, paths in index.items() if len(paths) > 1]
settings = (PROJECT / 'ProjectSettings/EditorBuildSettings.asset').read_text()
scenes = re.findall(r'- enabled: (\d)\s+path: (.+)\s+guid: ([0-9a-f]{32})', settings)
expected = ['Assets/_Project/Scenes/Bootstrap/Bootstrap.unity', 'Assets/_Project/Scenes/MainMenu/MainMenu.unity',
            'Assets/Scenes/SampleScene.unity'] + [f'Assets/_Project/Scenes/{name}_Prototype.unity' for name in ['Floor01', 'Campus', 'TrainingOffice']]
if [s[1] for s in scenes] != expected:
    errors.append('Unexpected Build Settings order')
script_guids = set()
for enabled, path, guid in scenes:
    scene = PROJECT / path
    if enabled != '1' or not scene.is_file() or index.get(guid) != [Path(str(scene) + '.meta')]:
        errors.append(f'Invalid enabled build path/GUID: {path}')
    for script in re.findall(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}', scene.read_text()):
        script_guids.add(script)
        # Package scripts are resolved by the native AssetDatabase audit.
        if script in index and not Path(str(index[script][0])[:-5]).is_file():
            errors.append(f'Missing script asset: {script}')
bootstrap = (PROJECT / expected[0]).read_text()
if 'playScenePath: Assets/_Project/Scenes/Floor01_Prototype.unity' not in bootstrap:
    errors.append('Start does not target Floor01')
for name in ['Floor01', 'Campus', 'TrainingOffice']:
    text = (PROJECT / f'Assets/_Project/Scenes/{name}_Prototype.unity').read_text()
    if text.count('guid: 680d7e264f104d7eb94ab44640024b07,') != 1:
        errors.append(f'{name}: expected one JourneySceneEntry')
    for key in ['player', 'viewCamera', 'inventory', 'quests', 'ui', 'spawn']:
        if not re.search(rf'^  {key}: \{{fileID: [1-9]\d*\}}$', text, re.M):
            errors.append(f'{name}: missing {key}')
print(json.dumps({'passed': not errors, 'build_scenes': [s[1] for s in scenes],
                  'asset_guids': len(index), 'scene_script_guids': len(script_guids),
                  'package_script_guids_requiring_unity': sorted(script_guids - index.keys()), 'errors': errors}, indent=2))
raise SystemExit(bool(errors))
