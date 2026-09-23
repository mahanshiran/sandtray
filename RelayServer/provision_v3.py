"""Run as root on the confirmed deployment host. Never print generated secrets."""
import os
from pathlib import Path
import secrets
import shutil
import subprocess

backup = Path('/root/RelayServer/backup-before-v3-20260912')
backup.mkdir(mode=0o700, exist_ok=False)
for name in ('RelayServer.dll', 'RelayServer.deps.json', 'RelayServer.runtimeconfig.json'):
    shutil.copy2(Path('/root/RelayServer/out') / name, backup / name)
shutil.copy2('/etc/sandtray/api.env', backup / 'api.env')
key = secrets.token_hex(48)
env = Path('/etc/sandtray/api.env')
lines = [line for line in env.read_text().splitlines() if not line.startswith('RELAY_TICKET_SIGNING_KEY=')]
env.write_text('\n'.join(lines) + '\nRELAY_TICKET_SIGNING_KEY=' + key + '\n')
os.chmod(env, 0o600)
relay_env = Path('/etc/sandtray/relay.env')
if relay_env.exists():
    raise RuntimeError('Existing relay configuration requires manual review')
relay_env.write_text('RELAY_TICKET_SIGNING_KEY=' + key + '\nRELAY_TLS_CERTIFICATE=/etc/sandtray/secrets/relay.pfx\n')
os.chmod(relay_env, 0o600)
subprocess.run(['openssl', 'pkcs12', '-export', '-out', '/etc/sandtray/secrets/relay.pfx',
    '-inkey', '/etc/letsencrypt/live/api.sandtraypro.com/privkey.pem',
    '-in', '/etc/letsencrypt/live/api.sandtraypro.com/fullchain.pem', '-passout', 'pass:'], check=True)
os.chmod('/etc/sandtray/secrets/relay.pfx', 0o600)
print('Backup and authentication configuration prepared; services unchanged.')
