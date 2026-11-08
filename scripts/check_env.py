"""Verify the RL environment: Python, PyTorch/CUDA, Gymnasium, SB3, and a tiny PPO run."""

import sys

import gymnasium
import stable_baselines3 as sb3
import torch

print("python     :", sys.version.split()[0], f"({sys.executable})")
print("torch      :", torch.__version__)
print("cuda avail :", torch.cuda.is_available())

if torch.cuda.is_available():
    print("gpu        :", torch.cuda.get_device_name(0))
    print("capability :", torch.cuda.get_device_capability(0))
    cap = torch.cuda.get_device_capability(0)
    x = torch.randn(1024, 1024, device="cuda")
    y = x @ x
    print("matmul     : ok", float(y.sum()))
    if cap[0] < 12:
        print("WARNING: expected Blackwell (12, x) for RTX 5060 Ti")
else:
    print("WARNING: CUDA not available, training will be slow")

print("gymnasium  :", gymnasium.__version__)
print("sb3        :", sb3.__version__)

from stable_baselines3 import PPO
from stable_baselines3.common.env_checker import check_env

env = gymnasium.make("CartPole-v1")
check_env(env.unwrapped, warn=True)

device = "cuda" if torch.cuda.is_available() else "cpu"
model = PPO(
    "MlpPolicy",
    env,
    verbose=0,
    n_steps=256,
    batch_size=64,
    device=device,
)
model.learn(total_timesteps=512)
print("ppo smoke  : ok (device=%s)" % device)
print("ALL OK")
