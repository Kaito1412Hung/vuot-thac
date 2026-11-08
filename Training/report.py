"""Read actual demonstrations and TensorBoard events; never synthesize scores."""
import argparse
import hashlib
import importlib.metadata
import json
from pathlib import Path
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

parser = argparse.ArgumentParser()
parser.add_argument('--run-id', default='suoirung-ppo-v5')
parser.add_argument('--demos', default='Demos-v4')
parser.add_argument('--config', default='config/boat_ppo.yaml')
parser.add_argument('--schema', type=int, default=4)
args = parser.parse_args()
root = Path(__file__).resolve().parent
report = {'run_id': args.run_id, 'seed': 42, 'unity_ml_agents': '4.2.0',
          'observation_schema': args.schema,
          'config_sha256': hashlib.sha256((root / args.config).read_bytes()).hexdigest(),
          'expert': 'BoatAIInput FSM; BC warm start followed by PPO',
          'versions': {name: importlib.metadata.version(name) for name in ['mlagents', 'torch', 'numpy', 'onnx']},
          'demonstrations': [], 'metrics': {}}
from mlagents.trainers.demo_loader import load_demonstration
for path in sorted((root / args.demos).glob('*.demo')):
    spec, pairs, steps = load_demonstration(str(path))
    report['demonstrations'].append({'file': path.name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        'steps': steps, 'observations': [list(obs.shape) for obs in spec.observation_specs],
        'continuous_actions': spec.action_spec.continuous_size})
directory = root / 'results' / args.run_id / 'SuoiRungBoat'
if directory.exists():
    events = EventAccumulator(str(directory), size_guidance={'scalars': 0}).Reload()
    for tag in events.Tags()['scalars']:
        values = events.Scalars(tag)
        report['metrics'][tag] = [{'step': v.step, 'value': v.value} for v in values]
model = root / 'results' / args.run_id / 'SuoiRungBoat.onnx'
if model.exists():
    import onnx
    network = onnx.load(str(model))
    onnx.checker.check_model(network)
    report['model'] = {'file': str(model.relative_to(root)), 'sha256': hashlib.sha256(model.read_bytes()).hexdigest(),
                       'bytes': model.stat().st_size, 'nodes': len(network.graph.node)}
destination = root / 'Reports'
destination.mkdir(exist_ok=True)
output = destination / (args.run_id + '.json')
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(str(output))
for name, values in report['metrics'].items():
    if values:
        print(name, values[-1])
