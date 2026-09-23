"""Run inside the deployed API container; no database records are read or written."""
import os
os.environ.setdefault('DJANGO_SETTINGS_MODULE', 'sandtray_api.production_settings')
import django
django.setup()
import socket
import ssl
import struct
import uuid
import time
import jwt
from django.conf import settings


def ticket(identity, room=None):
    now = int(time.time())
    claims = {'iss': 'sandtray-api', 'aud': 'sandtray-relay', 'sub': str(identity),
              'iat': now, 'nbf': now, 'exp': now + 60, 'jti': uuid.uuid4().hex,
              'purpose': 'host' if room is None else 'join'}
    if room is not None:
        claims['room'] = room
    return jwt.encode(claims, settings.RELAY_TICKET_SIGNING_KEY, algorithm='HS256')


def string(value):
    raw = value.encode()
    length = len(raw)
    prefix = bytearray()
    while length >= 128:
        prefix.append((length & 127) | 128)
        length >>= 7
    return bytes(prefix) + bytes([length]) + raw


def send(s, kind, data=b''):
    s.sendall(struct.pack('<I', len(data)+1) + bytes([kind]) + data)


def exact(s, n):
    value = b''
    while len(value) < n:
        chunk = s.recv(n-len(value))
        if not chunk:
            raise EOFError()
        value += chunk
    return value


def receive(s):
    n = struct.unpack('<I', exact(s, 4))[0]
    assert 0 < n < 4096
    return exact(s, n)


def connect():
    raw = socket.create_connection(('api.sandtraypro.com', 7777), timeout=10)
    tls = ssl.create_default_context().wrap_socket(raw, server_hostname='api.sandtraypro.com')
    send(tls, 24, struct.pack('<I', 4))
    assert receive(tls) == b'\x19\x04\x00\x00\x00'
    return tls


# The host-only probe verifies TLS, protocol, ticket signing, room creation and
# durable replay rejection. It deliberately does not join: a real join starts
# billable hosting and must reference real authorized accounts.
credential = ticket(9223372036854775806)
with connect() as host:
    send(host, 10, string(credential))
    created = receive(host)
    assert created[0] == 11
    with connect() as replay:
        send(replay, 10, string(credential))
        assert replay.recv(1) == b''
print('DEPLOYED_V4_TLS_HOST_REPLAY_CHECKS_PASSED (no database access or billable join)')
