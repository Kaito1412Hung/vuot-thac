"""ML-Agents 1.1 trainer with BC continuous-action domain correction.

The stock BC module compares raw Gaussian samples against demonstrations,
while SimpleActor.get_action_and_stats/env_action and ONNX scale those samples
with clamp(-3, 3)/3. Compare in the actual Unity action domain instead.
PPO's log probabilities, buffers and export remain unchanged.
"""
from mlagents.torch_utils import torch
from mlagents.trainers.torch_entities.agent_action import AgentAction
from mlagents.trainers.torch_entities.components.bc.module import BCModule
from mlagents.trainers.learn import main

_original_loss = getattr(BCModule, '_vuot_thac_original_loss', BCModule._behavioral_cloning_loss)
BCModule._vuot_thac_original_loss = _original_loss


def unity_domain_bc_loss(self, selected_actions, log_probs, expert_actions):
    if self.policy.actor.action_model.clip_action:
        selected_actions = AgentAction(
            torch.clamp(selected_actions.continuous_tensor, -3, 3) / 3,
            selected_actions.discrete_list,
        )
    return _original_loss(self, selected_actions, log_probs, expert_actions)


BCModule._behavioral_cloning_loss = unity_domain_bc_loss
if __name__ == '__main__':
    print('[VuotThac] BC compares continuous commands in the Unity/export action domain: clamp(raw,-3,3)/3.', flush=True)
    main()
