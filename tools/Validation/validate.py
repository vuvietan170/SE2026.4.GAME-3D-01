#!/usr/bin/env python3
"""Compile/test/build a disposable copy of HUS Escape; never opens the developer's Unity project."""
import argparse
import json
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
ALL_STEPS = ['compile', 'edit', 'play', 'gameplay', 'build', 'smoke']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', required=True, type=Path, help='Unity 6000.6.5f1 executable')
    parser.add_argument('--output', required=True, type=Path, help='New disposable project/output directory')
    parser.add_argument('--steps', nargs='+', choices=ALL_STEPS, default=ALL_STEPS)
    parser.add_argument('--target', choices=['StandaloneLinux64', 'StandaloneWindows64'], default='StandaloneWindows64' if os.name == 'nt' else 'StandaloneLinux64')
    parser.add_argument('--reuse', action='store_true', help='Reuse only a directory prepared by this runner, keeping Library cache')
    args = parser.parse_args()
    editor = args.unity.expanduser().resolve()
    output = args.output.expanduser().resolve()
    if not editor.is_file():
        parser.error('Unity executable not found: ' + str(editor))
    if output == ROOT or output == ROOT / 'HUS_Escape' or ROOT in output.parents:
        parser.error('Choose an output directory outside the repository')
    marker = output / '.hus-validation.json'
    if output.exists():
        if not args.reuse or not marker.is_file() or json.loads(marker.read_text()).get('repository') != str(ROOT):
            parser.error('Existing output is not explicitly reusable; choose a new directory')
    else:
        output.mkdir(parents=True)
    marker.write_text(json.dumps({'repository': str(ROOT), 'unity': '6000.6.5f1'}) + '\n')
    # Synchronize source folders only; never delete Library or touch the original Unity project.
    for name in ['Assets', 'Packages', 'ProjectSettings']:
        destination = output / name
        if destination.exists():
            shutil.rmtree(destination)
        shutil.copytree(ROOT / 'HUS_Escape' / name, destination)
    editor_dir = output / 'Assets/Editor'
    editor_dir.mkdir(exist_ok=True)
    shutil.copy2(ROOT / 'tools/Validation/RepositoryValidation.cs', editor_dir)
    shutil.copy2(ROOT / 'tools/Validation/BuildSmokeProbe.cs', output / 'Assets')
    for directory in ['MvpLoop', 'Integration', 'PlayerPause']:
        for path in (ROOT / 'tools' / directory).glob('*.cs'):
            shutil.copy2(path, output / 'Assets')
    for name in ['TransactionProbe.cs', 'TransactionValidation.cs']:
        shutil.copy2(ROOT / 'tools/SessionTransactions' / name, output / 'Assets')
    shutil.copy2(ROOT / 'tools/SessionTransactions/TransactionBackend.cs', output / 'Assets/_Project/Scripts/Core')
    fingerprint = hashlib.sha256()
    for directory in ['Assets', 'Packages', 'ProjectSettings']:
        for path in sorted((ROOT / 'HUS_Escape' / directory).rglob('*')):
            if path.is_file():
                fingerprint.update(str(path.relative_to(ROOT / 'HUS_Escape')).encode())
                fingerprint.update(path.read_bytes())
    for path in sorted((ROOT / 'tools/Validation').glob('*.cs')):
        fingerprint.update(path.name.encode())
        fingerprint.update(path.read_bytes())
    input_sha = fingerprint.hexdigest()
    summary = {'unity_executable': str(editor), 'target': args.target, 'project': str(output), 'steps': {}, 'passed': False, 'input_sha256': input_sha}

    def command(label, cmd, timeout=900):
        print('RUN ' + label, flush=True)
        started = time.monotonic()
        with (output / (label + '-stdout.log')).open('w') as log:
            result = subprocess.run([str(v) for v in cmd], stdout=log, stderr=subprocess.STDOUT, timeout=timeout)
        summary['steps'][label] = {'exit_code': result.returncode, 'seconds': round(time.monotonic() - started, 2)}
        if result.returncode:
            raise RuntimeError(f'{label}: exit {result.returncode}; inspect {output}')
        print('PASS ' + label, flush=True)

    def unity(label, options, timeout=900):
        command(label, [editor, '-batchmode', '-nographics', '-projectPath', output, '-logFile', output / (label + '.log')] + options, timeout)

    try:
        command('source-audit', [sys.executable, ROOT / 'tools/Validation/audit_repository.py', '--output', output / 'source-audit.json'])
        for step in args.steps:
            if step == 'compile':
                (output / 'native-audit.json').unlink(missing_ok=True)
                unity('compile', ['-quit', '-executeMethod', 'RepositoryValidation.Audit'])
                native = json.loads((output / 'native-audit.json').read_text())
                if not native['passed']:
                    raise RuntimeError('Native asset audit failed')
            elif step in ['edit', 'play']:
                xml = output / (step + '.xml')
                xml.unlink(missing_ok=True)
                unity(step, ['-runTests', '-testPlatform', 'EditMode' if step == 'edit' else 'PlayMode', '-testResults', str(xml)], 600)
                tests = ET.parse(xml).getroot()
                summary['steps'][step]['tests'] = tests.attrib
                if tests.get('result') != 'Passed' or int(tests.get('failed', 0)):
                    raise RuntimeError(step + ': tests did not pass')
            elif step == 'gameplay':
                for name, result_name in [('MvpLoop', 'mvp-loop-results.txt'), ('JourneyIntegration', 'journey-integration-result.txt'),
                                          ('PlayerPause', 'player-pause-results.txt'), ('Transaction', 'transaction-results.txt')]:
                    path = output / result_name
                    path.unlink(missing_ok=True)
                    unity(name, ['-executeMethod', name + 'Validation.Run'], 420)
                    result = path.read_text()
                    if not result.startswith('PASS'):
                        raise RuntimeError(name + ': probe failed')
                    summary['steps'][name]['result'] = result.splitlines()[0]
            elif step == 'build':
                (output / 'build-result.json').unlink(missing_ok=True)
                unity('build', ['-quit', '-buildTarget', args.target, '-executeMethod', 'RepositoryValidation.BuildSmoke'], 1200)
                built = json.loads((output / 'build-result.json').read_text())
                built['input_sha256'] = input_sha
                (output / 'build-result.json').write_text(json.dumps(built, indent=2) + '\n')
                summary['steps']['build']['result'] = built
                if not built['passed']:
                    raise RuntimeError('Build report failed')
            elif step == 'smoke':
                report = json.loads((output / 'build-result.json').read_text())
                if report.get('input_sha256') != input_sha:
                    raise RuntimeError('Source changed since build; rerun build before smoke')
                if report['target'] != args.target or not report['passed']:
                    raise RuntimeError('Build first for the requested target')
                if (os.name == 'nt') != (args.target == 'StandaloneWindows64'):
                    raise RuntimeError('Runtime smoke must run on the matching OS; cross-build does not count as player smoke')
                smoke_path = output / 'player-smoke.json'
                smoke_path.unlink(missing_ok=True)
                command('smoke', [report['player'], '-batchmode', '-nographics', '-logFile', output / 'player-smoke.log',
                                  '--hus-build-smoke', '--hus-smoke-report', smoke_path], 120)
                smoke = json.loads(smoke_path.read_text())
                summary['steps']['smoke']['result'] = smoke
                if not smoke['passed']:
                    raise RuntimeError('Runtime smoke failed')
        summary['passed'] = True
    except (subprocess.TimeoutExpired, OSError, RuntimeError, ValueError, ET.ParseError) as error:
        summary['error'] = str(error)
        print('FAIL: ' + str(error), file=sys.stderr, flush=True)
    finally:
        (output / 'validation-summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    return 0 if summary['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
