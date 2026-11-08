"""Calibrate the v4 BC warm start; actual v5 training/evaluation follow this step.

V4 learned raw means near expert commands although inference divides means by
three. Multiply both mean rows by three; keep the existing exploration std.
This is a documented calibration of trained weights, not a separate train run.
"""
from pathlib import Path
import torch

root = Path(__file__).resolve().parent
source = root / 'results/suoirung-ppo-v4/SuoiRungBoat/checkpoint.pt'
target = root / 'results/suoirung-v4-bc-domain-correction/SuoiRungBoat/checkpoint.pt'
checkpoint = torch.load(source, map_location='cpu')
for name in ['action_model._continuous_distribution.mu.weight', 'action_model._continuous_distribution.mu.bias']:
    checkpoint['Policy'][name].mul_(3)
target.parent.mkdir(parents=True, exist_ok=True)
torch.save(checkpoint, target)
print(target)
