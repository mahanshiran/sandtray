"""Create an ephemeral test room on a deployed relay; close all sockets afterwards.

Usage: python3 RelayServer/smoke_remote.py HOST [PORT]
Does not access existing rooms or user boards.
"""
import socket
import struct
import sys
import uuid
from test_protocol import send, receive


def run(hostname, port):
    def connect():
        sock = socket.create_connection((hostname, port), timeout=8)
        try:
            send(sock, 24, struct.pack('<I', 2))
            assert receive(sock) == b'\x19\x02\x00\x00\x00', 'Protocol v2 handshake failed'
            return sock
        except BaseException:
            sock.close()
            raise

    with connect() as host:
        send(host, 10)
        room = receive(host)
        assert room[0] == 11
        with connect() as client:
            send(client, 12, room[1:])
            assert receive(client)[:2] == b'\x0d\x01'
            token = uuid.uuid4().hex.encode()
            request = b'\x00' + bytes([len(token)]) + token + b'\x05Probe'
            send(client, 8, request)
            assert receive(host) == b'\x08' + request
            send(client, 19, b'blocked')
            send(client, 14, b'barrier')
            assert receive(host) == b'\x0ebarrier'
            send(host, 1, request)
            assert receive(client) == b'\x01' + request
            send(client, 19, b'approved')
            assert receive(host) == b'\x13approved'
            send(host, 32, b'\x00\x00\x05Probe')
            assert receive(client) == b'\x20\x00\x00\x05Probe'
            send(host, 29, bytes([len(token)]) + token + b'\x02snapshot')
            assert receive(client) == b'\x02snapshot'
            send(host, 30, bytes([len(token)]) + token)
            assert receive(client) == b'\x1f'
            assert client.recv(1) == b''
        assert receive(host) == b'\x1a' + bytes([len(token)]) + token
        send(host, 27)
        assert receive(host) == b'\x1c'
    print('PASS: live v2 handshake, host/join, observer rejection, approved editing, status, targeted snapshot, removal, heartbeat')


if __name__ == '__main__':
    run(sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 7777)
