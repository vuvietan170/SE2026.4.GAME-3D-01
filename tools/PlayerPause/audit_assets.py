#!/usr/bin/env python3
"""Check unchanged script identities, scene use, and original UI bindings against pre-change evidence."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
baseline = json.loads((root / 'docs/Development/PlayerPauseEvidence/before-assets.json').read_text())
assets = root / 'HUS_Escape/Assets'
for name in ['PlayerController', 'PlayerController1', 'ThirdPersonCameraFollow', 'GameplayInputGate']:
    meta = assets / f'_Project/Scripts/Player/{name}.cs.meta'
    expected = baseline[name]
    assert hashlib.sha256(meta.read_bytes()).hexdigest() == expected['meta_sha256'], name
    actual = {}
    for path in list(assets.rglob('*.unity')) + list(assets.rglob('*.prefab')):
        count = path.read_text().count('guid: ' + expected['guid'] + ',')
        if count:
            actual[str(path.relative_to(root))] = count
    assert actual == expected['assets'], (name, actual)
actions = json.loads((assets / 'InputSystem_Actions.inputactions').read_text())
ui = next(m for m in actions['maps'] if m['name'] == 'UI')
assert ui == baseline['ui_map'], 'Existing UI map changed'
print('PASS: original script .meta bytes, scene/prefab controller use, and entire UI map preserved')
