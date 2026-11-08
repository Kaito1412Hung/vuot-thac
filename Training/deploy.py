"""Copy a real trainer export, checking schema and matching evaluation provenance."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import re
from pathlib import Path
import shutil
import onnx

parser = argparse.ArgumentParser()
parser.add_argument('--run-id', default='suoirung-ppo-v5')
parser.add_argument('--candidate', action='store_true', help='Import for evaluation, without claiming acceptance')
args = parser.parse_args()
training = Path(__file__).resolve().parent
run = training / 'results' / args.run_id
source = run / 'SuoiRungBoat.onnx'
if not source.exists():
    source = max((run / 'SuoiRungBoat').glob('SuoiRungBoat-*.onnx'), key=lambda p: int(p.stem.rsplit('-', 1)[1]))
model = onnx.load(str(source))
onnx.checker.check_model(model)
assert len(model.graph.input) == 1 and model.graph.input[0].type.tensor_type.shape.dim[-1].dim_value == 42
assert any(o.name == 'deterministic_continuous_actions' for o in model.graph.output)
assert next(o for o in model.graph.output if o.name == 'deterministic_continuous_actions').type.tensor_type.shape.dim[-1].dim_value == 2
digest = hashlib.sha256(source.read_bytes()).hexdigest()
evaluation = training / 'Reports/full-race-ppo.txt'
if not args.candidate:
    header = evaluation.read_text(encoding='utf-8-sig').splitlines()[0]
    if 'finished=6/6 ' not in header or 'model_sha256=' + digest not in header:
        raise SystemExit('Acceptance requires 6/6 finish from this exact model hash. Use --candidate to evaluate first.')
    rescues = [int(v) for v in re.findall(r'recoveries=(\d+)', evaluation.read_text(encoding='utf-8-sig'))]
    if len(rescues) != 6 or max(rescues) > 3 or sum(rescues) > 6:
        raise SystemExit('Too many checkpoint recoveries: acceptance allows at most 3 per boat and 6 total.')
steps = None
for checkpoint in (run / 'SuoiRungBoat').glob('SuoiRungBoat-*.onnx'):
    if hashlib.sha256(checkpoint.read_bytes()).hexdigest() == digest:
        steps = int(checkpoint.stem.rsplit('-', 1)[1])
destination = training.parent / 'Assets/_Game/Resources/AI'
destination.mkdir(parents=True, exist_ok=True)
shutil.copyfile(source, destination / 'SuoiRungBoat.onnx')
info = {'observation_schema': 4, 'validated': not args.candidate, 'run_id': args.run_id,
        'source_model': str(source.relative_to(training)).replace('\\', '/'), 'sha256': digest,
        'trained_steps': steps, 'observations': 42, 'continuous_actions': 2, 'hidden_layers': [128, 128],
        'method': 'FSM expert demonstrations + behavioral cloning + PPO; no FSM action source during inference',
        'imported_utc': datetime.now(timezone.utc).isoformat(), 'android_validated': False}
(destination / 'SuoiRungBoatInfo.json').write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(info, ensure_ascii=False))
