param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunId='suoirung-ppo-v5',[switch]$Resume,[string]$InitializeFrom='',[string]$Config='Training/config/boat_ppo.yaml')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$env:TEMP=Join-Path $PSScriptRoot '.tmp';$env:TMP=$env:TEMP
$env:PIP_CACHE_DIR=Join-Path $PSScriptRoot '.cache/pip'
$env:TORCH_HOME=Join-Path $PSScriptRoot '.cache/torch'
$env:HF_HOME=Join-Path $PSScriptRoot '.cache/huggingface'
$env:OMP_NUM_THREADS='2';$env:MKL_NUM_THREADS='2'
New-Item -ItemType Directory -Force -Path $env:TEMP,$env:PIP_CACHE_DIR,$env:TORCH_HOME,$env:HF_HOME | Out-Null
$python=Join-Path $PSScriptRoot '.python310/python.exe'
if(!(Test-Path -LiteralPath $python)){$python=Join-Path $PSScriptRoot '.venv/Scripts/python.exe'}
if(!(Test-Path -LiteralPath $python)){throw 'Chưa có Python trainer. Dùng Python 3.10.1–3.10.12 tạo Training/.venv rồi cài Training/requirements.txt; xem PHASE3.md.'}
$trainerArgs=@('Training/learn.py',$Config,'--run-id',$RunId,'--results-dir','Training/results','--base-port','5004','--seed','42','--torch-device','cpu','--timeout-wait','180','--max-lifetime-restarts','0')
if($Resume){$trainerArgs+='--resume'}
if($InitializeFrom){$trainerArgs+=@('--initialize-from',$InitializeFrom)}
& $python @trainerArgs 2>&1 | Tee-Object -FilePath (Join-Path $PSScriptRoot ($RunId+'.log')) -Append
exit $LASTEXITCODE
