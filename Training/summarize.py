"""Aggregate only recorded race reports for the currently deployed model hash."""
import csv
import argparse
import hashlib
import json
from pathlib import Path
import re
import statistics

parser=argparse.ArgumentParser()
parser.add_argument('--allow-partial',action='store_true',help='Skip older model hashes and list the omitted reports explicitly')
parser.add_argument('--config',default='config/boat_ppo.yaml')
args=parser.parse_args()
root = Path(__file__).resolve().parent
reports = root / 'Reports'
model = json.loads((root.parent / 'Assets/_Game/Resources/AI/SuoiRungBoatInfo.json').read_text(encoding='utf-8'))
results = [];skipped=[]
paths = [reports / 'full-race-fsm.txt'] + sorted(reports.glob('full-race-ppo-d?-s?.txt'))
for path in paths:
    text = path.read_text(encoding='utf-8-sig')
    header = dict(re.findall(r'(\w+)=([^\s]+)', text.splitlines()[0]))
    if header['controller'] == 'PPO' and header.get('model_sha256') != model['sha256']:
        if args.allow_partial:
            skipped.append(path.name);continue
        raise SystemExit('Stale model report: ' + str(path))
    times = [float(v) for v in re.findall(r'time=([\d.]+)', text)]
    finished = [v == 'True' for v in re.findall(r'finished=(True|False)', text)]
    results.append({'report': path.name, 'controller': header['controller'], 'difficulty': int(header['difficulty']),
        'scenario': int(header['scenario']), 'finished': sum(finished), 'boats': len(finished),
        'mean_finish_seconds': round(statistics.mean(t for t, ok in zip(times, finished) if ok), 2) if any(finished) else None,
        'impacts': sum(int(v) for v in re.findall(r'impacts=(\d+)', text)),
        'recoveries': sum(int(v) for v in re.findall(r'recoveries=(\d+)', text))})
source_hashes = {}
for name in ['learn.py', args.config, 'requirements.lock.txt', 'correct_bc_scale.py']:
    source_hashes[name] = hashlib.sha256((root / name).read_bytes()).hexdigest()
summary = {'model': model, 'source_sha256': source_hashes, 'race_results': results,'omitted_older_model_reports':skipped,
    'limitations': ['Unity Editor only; Android not validated', 'One batch per difficulty; balance needs more trials',
                   'PPO is slower than FSM in the recorded normal batch', 'Checkpoint recoveries are included in totals']}
(reports / 'phase3-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
with (reports / 'phase3-races.csv').open('w', encoding='utf-8-sig', newline='') as fp:
    writer = csv.DictWriter(fp, fieldnames=list(results[0]))
    writer.writeheader()
    writer.writerows(results)
print(json.dumps(results, ensure_ascii=False, indent=2))
