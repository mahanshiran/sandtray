"""Isolated TLS v4 integration: run with backend test Python (PyJWT installed)."""
import os
import socket
import ssl
import struct
import subprocess
import sys
import tempfile
import time
import uuid
import jwt
from test_protocol import send, receive

KEY = 'test-only-relay-signing-key-32-bytes-minimum'


def string(value):
    value = value.encode()
    count = len(value)
    prefix = bytearray()
    while count >= 128:
        prefix.append((count & 127) | 128)
        count >>= 7
    return bytes(prefix) + bytes([count]) + value


def ticket(account, purpose, room=None):
    now = int(time.time())
    claims = dict(iss='sandtray-api', aud='sandtray-relay', sub=str(account),
                  iat=now, nbf=now, exp=now+60, jti=uuid.uuid4().hex, purpose=purpose)
    if room is not None:
        claims['room'] = room
    return jwt.encode(claims, KEY, algorithm='HS256')


def run(dll):
    clean = {k: v for k, v in os.environ.items() if not k.startswith('RELAY_')}
    for config in ({}, {'RELAY_TICKET_SIGNING_KEY': KEY}):
        rejected = subprocess.run(['dotnet', dll, '0'], env={**clean, **config}, capture_output=True, timeout=5)
        assert rejected.returncode != 0, 'Unconfigured relay must fail closed'
    with tempfile.TemporaryDirectory(prefix='sandtray-tls-test-') as directory:
        pem, private, pfx = [os.path.join(directory, name) for name in ('cert.pem', 'key.pem', 'cert.pfx')]
        subprocess.run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '1',
                        '-keyout', private, '-out', pem, '-subj', '/CN=localhost',
                        '-addext', 'subjectAltName=DNS:localhost'], check=True, capture_output=True)
        subprocess.run(['openssl', 'pkcs12', '-export', '-out', pfx, '-inkey', private,
                        '-in', pem, '-passout', 'pass:local-test'], check=True, capture_output=True)
        with socket.socket() as reserve:
            reserve.bind(('127.0.0.1', 0))
            port = reserve.getsockname()[1]
        process = subprocess.Popen(['dotnet', dll, str(port)], stdout=subprocess.DEVNULL,
            env={**os.environ, 'RELAY_TLS_CERTIFICATE': pfx, 'RELAY_TLS_PASSWORD': 'local-test',
                 'RELAY_TICKET_SIGNING_KEY': KEY, 'RELAY_REPLAY_STORE': os.path.join(directory, 'replay.json')})
        context = ssl.create_default_context(cafile=pem)

        def connect():
            raw = socket.create_connection(('127.0.0.1', port), timeout=3)
            return context.wrap_socket(raw, server_hostname='localhost')

        def hello(client):
            send(client, 24, struct.pack('<I', 4))
            assert receive(client) == b'\x19\x04\x00\x00\x00'

        try:
            for _ in range(50):
                try:
                    with connect():
                        break
                except ConnectionRefusedError:
                    time.sleep(.1)
            host_ticket = ticket(1, 'host')
            # Chain and hostname validation must fail before protocol traffic.
            for verifier, hostname in ((ssl.create_default_context(), 'localhost'), (context, 'wrong.example')):
                with socket.create_connection(('127.0.0.1', port), timeout=3) as raw:
                    try:
                        verifier.wrap_socket(raw, server_hostname=hostname)
                    except ssl.SSLCertVerificationError:
                        pass
                    else:
                        raise AssertionError('Invalid certificate was trusted')
            # Reject oversized frames from the prefix alone, without waiting for body allocation.
            with connect() as oversized:
                oversized.sendall(struct.pack('<I', 10000000))
                assert receive(oversized) == b'\x19\x04\x00\x00\x00'
                assert oversized.recv(1) == b''
            with connect() as oversized:
                hello(oversized)
                oversized.sendall(struct.pack('<I', 10000000))
                assert oversized.recv(1) == b''
            with connect() as host:
                hello(host)
                send(host, 10, string(host_ticket))
                created = receive(host)
                assert created[0] == 11
                room = created[2:].decode()
                assert len(room) == 6 and all(c in 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789' for c in room)
                with connect() as replay:
                    hello(replay)
                    send(replay, 10, string(host_ticket))
                    assert replay.recv(1) == b''
                with connect() as invalid:
                    hello(invalid)
                    send(invalid, 12, string(ticket(2, 'join', 'ZZZZZZ')) + string(room))
                    assert invalid.recv(1) == b''
                with connect() as client:
                    hello(client)
                    send(client, 12, string(ticket(2, 'join', room)) + string(room))
                    assert receive(client)[:2] == b'\x0d\x01'
                    # Real Unity join: register, assign role, send targeted snapshots.
                    identity = uuid.uuid4().hex
                    registration = b'\x02' + string(identity) + string('TLS participant')
                    send(client, 8, registration)
                    assert receive(host) == b'\x08' + registration
                    send(host, 1, b'\xff' + string(identity) + string('Invalid role'))
                    send(host, 27)
                    assert receive(host) == b'\x1c'
                    assignment = b'\x00' + string(identity) + string('TLS participant')
                    send(host, 33, struct.pack('<q', 1) + assignment)
                    assert receive(client) == b'\x21' + struct.pack('<q', 1) + assignment
                    for kind in (2, 15, 23):
                        snapshot = b'test-board-data' * 2048
                        send(host, 29, string(identity) + bytes([kind]) + snapshot)
                        assert receive(client) == bytes([kind]) + snapshot
                    with connect() as duplicate:
                        hello(duplicate)
                        send(duplicate, 12, string(ticket(2, 'join', room)) + string(room))
                        assert receive(duplicate)[:2] == b'\x0d\x00'
                    with connect() as another_account:
                        hello(another_account)
                        send(another_account, 12, string(ticket(3, 'join', room)) + string(room))
                        assert receive(another_account)[:2] == b'\x0d\x00', 'Room admitted a second joiner'
                    send(client, 27)
                    assert receive(client) == b'\x1c'
                    send(host, 30, string(identity))
                    assert receive(client) == b'\x1f'
                    assert client.recv(1) == b''
                    assert receive(host)[0] == 26
                with connect() as replacement:
                    hello(replacement)
                    send(replacement, 12, string(ticket(3, 'join', room)) + string(room))
                    assert receive(replacement)[:2] == b'\x0d\x01', 'Joiner slot was not released'
                    replacement_identity = uuid.uuid4().hex
                    replacement_registration = b'\x02' + string(replacement_identity) + string('Replacement')
                    send(replacement, 8, replacement_registration)
                    assert receive(host) == b'\x08' + replacement_registration
                assert receive(host)[0] == 26
                with connect() as returned:
                    hello(returned)
                    send(returned, 12, string(ticket(2, 'join', room)) + string(room))
                    assert receive(returned)[:2] == b'\x0d\x01'
                    registration = b'\x02' + string(identity) + string('Returned observer')
                    send(returned, 8, registration)
                    assert receive(host) == b'\x08' + registration
                    send(returned, 16, b'blocked-edit')
                    # Allowed selection acts as an ordering barrier for the rejected edit.
                    send(returned, 14, b'barrier')
                    assert receive(host) == b'\x0ebarrier'
                    assignment = b'\x00' + string(identity) + string('Returned observer')
                    send(host, 33, struct.pack('<q', 2) + assignment)
                    assert receive(returned)[0] == 33
                    def edit(revision, sequence):
                        return string(identity) + struct.pack('<qqB', revision, sequence, 16) + b'approved-edit'
                    def rejected(payload):
                        send(returned, 34, payload)
                        send(returned, 14, b'barrier')
                        assert receive(host) == b'\x0ebarrier'
                    rejected(edit(2, 1))  # Not synchronized yet.
                    for kind in (35, 36):
                        send(host, 29, string(identity) + bytes([kind]) + struct.pack('<q', 9))
                        assert receive(returned) == bytes([kind]) + struct.pack('<q', 9)
                        if kind == 35:
                            send(returned, 37, struct.pack('<q', 9))
                            rejected(edit(2, 1))  # Cannot acknowledge before completion.
                    send(returned, 37, struct.pack('<q', 8))
                    rejected(edit(2, 1))  # Stale snapshot acknowledgement.
                    send(returned, 37, struct.pack('<q', 9))
                    send(host, 29, string(identity) + bytes([35]) + struct.pack('<q', 8))
                    send(host, 27)
                    assert receive(host) == b'\x1c'
                    send(returned, 27)
                    assert receive(returned) == b'\x1c'  # Stale begin was not forwarded.
                    rejected(edit(1, 1))  # Old editing grant.
                    send(returned, 34, edit(2, 1))
                    assert receive(host) == b'\x22' + edit(2, 1)
                    rejected(edit(2, 1))  # Duplicate operation.
                    send(host, 33, struct.pack('<q', 3) + assignment)
                    assert receive(returned)[0] == 33
                    rejected(edit(2, 2))  # Old queued edit after a fresh grant.
                    send(returned, 34, edit(3, 1))
                    assert receive(host) == b'\x22' + edit(3, 1)
                    paused = b'\x02' + string(identity) + string('Paused observer')
                    send(host, 33, struct.pack('<q', 4) + paused)
                    assert receive(returned)[0] == 33
                    rejected(edit(4, 1))  # A current revision alone cannot grant editing.
                with connect() as legacy:
                    send(legacy, 24, struct.pack('<I', 2))
                    assert receive(legacy) == b'\x19\x04\x00\x00\x00'
                    assert legacy.recv(1) == b''
            process.terminate()
            process.wait(timeout=5)
            process = subprocess.Popen(['dotnet', dll, str(port)], stdout=subprocess.DEVNULL,
                env={**os.environ, 'RELAY_TLS_CERTIFICATE': pfx, 'RELAY_TLS_PASSWORD': 'local-test',
                     'RELAY_TICKET_SIGNING_KEY': KEY, 'RELAY_REPLAY_STORE': os.path.join(directory, 'replay.json')})
            for _ in range(50):
                try:
                    with connect() as replay_after_restart:
                        hello(replay_after_restart)
                        send(replay_after_restart, 10, string(host_ticket))
                        assert replay_after_restart.recv(1) == b''
                        break
                except ConnectionRefusedError:
                    time.sleep(.1)
            else:
                raise AssertionError('Relay failed to restart')
            print('AUTHENTICATED_TLS_RELAY_CHECKS_PASSED (including durable replay after process restart)')
        finally:
            process.terminate()
            process.wait(timeout=5)


if __name__ == '__main__':
    run(sys.argv[1])
