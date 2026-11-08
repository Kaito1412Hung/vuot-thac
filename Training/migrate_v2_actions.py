"""Warm-start migration, not a training run: retain learned steering weights.

v1/v2 decoded throttle as (action + 1)/2. v3 uses the full signed action
directly, so migrate the Gaussian throttle mean/std by the same affine map.
All subsequent v3 scores must come from actual training and Unity evaluation.
"""
import math
from pathlib import Path
import torch

root = Path(__file__).resolve().parent
source = root / 'results/suoirung-ppo-v2/SuoiRungBoat/checkpoint.pt'
target = root / 'results/suoirung-v2-action-migration/SuoiRungBoat/checkpoint.pt'
checkpoint = torch.load(source, map_location='cpu')
policy = checkpoint['Policy']
policy['action_model._continuous_distribution.mu.weight'][1].mul_(.5)
policy['action_model._continuous_distribution.mu.bias'][1].mul_(.5).add_(.5)
policy['action_model._continuous_distribution.log_sigma'][0, 1] -= math.log(2)
target.parent.mkdir(parents=True, exist_ok=True)
torch.save(checkpoint, target)
print(target)
