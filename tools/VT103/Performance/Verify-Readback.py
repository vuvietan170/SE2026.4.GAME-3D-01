"""Independent post-process final-file read-back; does not implement a writer or touch raw samples."""
import csv, hashlib, json, sys
from pathlib import Path
root=Path(sys.argv[1]).resolve()
results=[]
for run in json.loads((root/'summary.json').read_text()):
    with (root/run['SamplesCsv'].replace('\\','/')).open(newline='',encoding='utf-8-sig') as f:
        rows=list(csv.DictReader(f))
    checked=0
    for path in {r['path'] for r in rows}:
        expected=max((r for r in rows if r['path']==path),key=lambda r:int(r['revision']))
        doc=json.loads(Path(path).read_text(encoding='utf-8'))
        revision=int(expected['revision']);client=int(expected['client']);request=expected['requestId']
        prefix=f'{{"scene":"Floor01_Prototype","fixtureRevision":{revision},"requestId":"{request}","syntheticPadding":"'
        payload=prefix+chr(ord('a')+client%26)*(32768-len(prefix)-2)+'"}'
        assert doc['formatVersion']==1 and doc['revision']==revision and doc['requestId']==request
        assert doc['payload']==payload and len(payload.encode('utf-8'))==32768
        assert doc['payloadFingerprint']==hashlib.sha256(payload.encode('utf-8')).hexdigest()
        acknowledged=[int(r['revision']) for r in rows if r['path']==path and r['status']=='Committed']
        assert acknowledged and doc['revision']>=max(acknowledged)
        checked+=1
    results.append(dict(scenario=run['Scenario'],round=run['Round'],filesChecked=checked,valid=True))
result=dict(passed=True,finalFilesChecked=sum(r['filesChecked'] for r in results),rounds=results,
            limitation='Read-back after normal benchmark exit, not crash recovery or a resumed DraftStore session.')
(root/'post-process-readback.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))