"""Local integration checks: python3 RelayServer/test_protocol.py <built dll>."""
import socket
import struct
import subprocess
import sys
import time
import uuid
import os
import threading


def send(sock, kind, payload=b''):
    sock.sendall(struct.pack('<I', len(payload) + 1) + bytes([kind]) + payload)


def receive(sock):
    def exact(count):
        data = b''
        while len(data) < count:
            part = sock.recv(count - len(data))
            if not part:
                raise EOFError('Connection closed')
            data += part
        return data
    return exact(struct.unpack('<I', exact(4))[0])


def run(dll):
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0))
        port = probe.getsockname()[1]
    process = subprocess.Popen(['dotnet', dll, str(port)], stdout=subprocess.DEVNULL,
                               env={**os.environ, 'RELAY_IDLE_TIMEOUT_MS': '1200', 'RELAY_ALLOW_INSECURE_TEST_MODE': '1'})
    def connect():
        return socket.create_connection(('127.0.0.1', port), timeout=2)
    try:
        for _ in range(50):
            try:
                with connect():
                    break
            except ConnectionRefusedError:
                time.sleep(.1)
        for kind, data in [(10, b''), (12, b'\x06ABCDEF'), (24, struct.pack('<I', 1)), (24, b'x')]:
            with connect() as client:
                send(client, kind, data)
                assert receive(client) == b'\x19\x02\x00\x00\x00'
                assert client.recv(1) == b'', 'Incompatible client stayed connected'
        with connect() as host:
            send(host, 24, struct.pack('<I', 2))
            assert receive(host) == b'\x19\x02\x00\x00\x00'
            send(host, 10)
            created = receive(host)
            assert created[0] == 11
            with connect() as client:
                send(client, 24, struct.pack('<I', 2))
                assert receive(client)[0] == 25
                send(client, 12, created[1:])
                assert receive(client)[:2] == b'\x0d\x01'
                send(host, 14, b'probe')
                assert receive(client) == b'\x0eprobe'
                send(client, 14, b'return')
                assert receive(host) == b'\x0ereturn'
                token = uuid.uuid4().hex.encode()
                role_request = b'\x00' + bytes([len(token)]) + token + b'\x04Test'
                send(client, 8, role_request)
                assert receive(host) == b'\x08' + role_request
                # Observer edits and forged host messages must never reach the host.
                send(client, 19, b'blocked')
                send(client, 1, role_request)
                send(client, 14, b'barrier')
                assert receive(host) == b'\x0ebarrier'
                send(host, 1, role_request)
                assert receive(client) == b'\x01' + role_request
                with connect() as other:
                    send(other, 24, struct.pack('<I', 2)); receive(other)
                    send(other, 12, created[1:])
                    assert receive(other)[:2] == b'\x0d\x00', 'Second joiner entered an occupied room'
                for kind in (23, 2, 15):
                    send(host, 29, bytes([len(token)]) + token + bytes([kind]) + b'snapshot')
                    assert receive(client) == bytes([kind]) + b'snapshot'
                status = b'\x00\x00\x04Test'
                send(host, 32, status)
                assert receive(client) == b'\x20' + status
                send(client, 32, b'forged-status')
                send(client, 29, bytes([len(token)]) + token + b'\x02forged')
                send(client, 14, b'after-forgery')
                assert receive(host) == b'\x0eafter-forgery'
                send(client, 19, b'allowed')
                assert receive(host) == b'\x13allowed'
                send(host, 1, b'\x02' + role_request[1:])
                assert receive(client)[0] == 1
                send(client, 19, b'paused')
                send(client, 14, b'barrier2')
                assert receive(host) == b'\x0ebarrier2'
            assert receive(host) == b'\x1a' + bytes([len(token)]) + token
            # Same participant returns on a fresh connection: no inherited editing grant.
            with connect() as returning:
                send(returning, 24, struct.pack('<I', 2)); receive(returning)
                send(returning, 12, created[1:]); assert receive(returning)[:2] == b'\x0d\x01'
                send(returning, 8, role_request); assert receive(host) == b'\x08' + role_request
                send(returning, 19, b'stale-edit')
                send(returning, 14, b'reconnected'); assert receive(host) == b'\x0ereconnected'
                send(host, 1, role_request); assert receive(returning) == b'\x01' + role_request
                send(returning, 19, b'reapproved'); assert receive(host) == b'\x13reapproved'
                # Participant cannot remove itself or another participant with a host-only packet.
                send(returning, 30, bytes([len(token)]) + token)
                send(returning, 14, b'remove-forgery'); assert receive(host) == b'\x0eremove-forgery'
                send(host, 30, bytes([len(token)]) + token)
                assert receive(returning) == b'\x1f'
                assert returning.recv(1) == b''
            assert receive(host)[0] == 26
            send(host, 27); assert receive(host) == b'\x1c'
        print('PASS: host removes editor with explicit notification; forged removal blocked; host stays connected')
        print('PASS: returning participant requires fresh editing approval')
        print('PASS: catalog/board/paint snapshots reach only the target; participant snapshot forgery rejected')
        print('PASS: host session status reaches joiner; participant-forged status is rejected')
        print('PASS: room rejects a second joiner and accepts a replacement after departure')
        print('PASS: legacy host/join, wrong/malformed versions rejected; v2 host/join and bidirectional forwarding')
        print('PASS: observer/forged-host edits blocked, approved edits forwarded, revoked edits blocked, leave notification')
        with connect() as host:
            send(host, 24, struct.pack('<I', 2)); receive(host)
            send(host, 10); created = receive(host)
            with connect() as silent:
                send(silent, 24, struct.pack('<I', 2)); receive(silent)
                send(silent, 12, created[1:]); receive(silent)
                send(silent, 8, role_request); receive(host)
                # Keep host healthy while the peer silently stops sending anything.
                departed = False
                for _ in range(8):
                    time.sleep(.25)
                    send(host, 27)
                    packet = receive(host)
                    if packet[0] == 26:
                        departed = True
                        packet = receive(host)
                    assert packet == b'\x1c'
                assert departed, 'Silent participant was not removed'
                assert silent.recv(1) == b''
            with connect() as replacement:
                send(replacement, 24, struct.pack('<I', 2)); receive(replacement)
                send(replacement, 12, created[1:])
                assert receive(replacement)[:2] == b'\x0d\x01', 'Timed-out joiner kept the slot'
            # The healthy host is still able to exchange heartbeats.
            send(host, 27); assert receive(host) == b'\x1c'
        with connect() as silent_host:
            send(silent_host, 24, struct.pack('<I', 2)); receive(silent_host)
            send(silent_host, 10); receive(silent_host)
            assert silent_host.recv(1) == b'', 'Silent host was not disconnected'
        print('PASS: heartbeat keeps host alive, silent peer removed, silent host disconnected')
        with connect() as host:
            send(host, 24, struct.pack('<I', 2)); receive(host)
            send(host, 10); created = receive(host)
            stop = threading.Event()
            def broadcast():
                while not stop.wait(.005):
                    send(host, 14, b'background-broadcast')
            writer = threading.Thread(target=broadcast)
            writer.start()
            try:
                for _ in range(20):
                    with connect() as joining:
                        send(joining, 24, struct.pack('<I', 2)); receive(joining)
                        send(joining, 12, created[1:])
                        assert receive(joining)[:2] == b'\x0d\x01', 'Broadcast arrived before join confirmation'
                        join_token = uuid.uuid4().hex.encode()
                        request = b'\x02' + bytes([len(join_token)]) + join_token + b'\x05Other'
                        send(joining, 8, request)
                        assert receive(host) == b'\x08' + request
                    assert receive(host) == b'\x1a' + bytes([len(join_token)]) + join_token
            finally:
                stop.set(); writer.join(timeout=2)
        print('PASS: join confirmation precedes concurrent host broadcasts (20 joins)')
    finally:
        process.terminate()
        process.wait(timeout=5)


if __name__ == '__main__':
    run(sys.argv[1])
