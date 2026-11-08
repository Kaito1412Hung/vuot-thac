"""Regression: compare BC against the SDK's actual environment action transform."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest
import torch
from mlagents.trainers.torch_entities.agent_action import AgentAction

spec = importlib.util.spec_from_file_location('vuotthac_trainer', Path(__file__).with_name('learn.py'))
trainer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(trainer)


class BCDomainTests(unittest.TestCase):
    def context(self):
        action_spec = SimpleNamespace(continuous_size=2, discrete_size=0)
        return SimpleNamespace(policy=SimpleNamespace(
            behavior_spec=SimpleNamespace(action_spec=action_spec),
            actor=SimpleNamespace(action_model=SimpleNamespace(clip_action=True))))

    def test_expert_equal_to_sdk_environment_action_has_zero_loss(self):
        raw = torch.tensor([[-6., -3.], [-1.5, 0.], [1.5, 3.], [6., -1.5]])
        selected = AgentAction(raw, None)
        expert = AgentAction(torch.from_numpy(selected.to_action_tuple(clip=True).continuous), None)
        loss = trainer.unity_domain_bc_loss(self.context(), selected, None, expert)
        self.assertEqual(loss.item(), 0)

    def test_gradient_moves_raw_policy_toward_expert_command(self):
        raw = torch.zeros((1, 2), requires_grad=True)
        selected = AgentAction(raw, None)
        expert = AgentAction(torch.tensor([[1., -1.]]), None)
        trainer.unity_domain_bc_loss(self.context(), selected, None, expert).backward()
        self.assertLess(raw.grad[0, 0].item(), 0)
        self.assertGreater(raw.grad[0, 1].item(), 0)


if __name__ == '__main__':
    unittest.main()
