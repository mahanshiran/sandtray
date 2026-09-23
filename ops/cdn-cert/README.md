# CDN certificate renewal

`assets.sandtraypro.com` uses Aliyun CDN in front of the Hong Kong OSS bucket.
The production server obtains an ECDSA certificate from Let's Encrypt with a
Cloudflare DNS-01 challenge and deploys it with acme.sh's `ali_cdn` hook.

Production paths:

- acme.sh: `/root/.acme.sh`
- credentials: `/etc/sandtray/cdn-cert.env` (root-only, mode `0600`)
- renewal command: `/usr/local/sbin/sandtray-cdn-cert-renew`
- systemd timer: `sandtray-cdn-cert-renew.timer`

The timer runs once per day with a randomized delay. acme.sh only renews when
the certificate enters its CA-provided renewal window, then automatically runs
the saved Aliyun CDN deployment hook.

Useful checks:

```bash
systemctl list-timers sandtray-cdn-cert-renew.timer
systemctl show sandtray-cdn-cert-renew.service -p Result -p ExecMainStatus
journalctl -u sandtray-cdn-cert-renew.service
```

Do not commit or copy `/etc/sandtray/cdn-cert.env` into the repository. Rotate
the Cloudflare token and Aliyun AccessKey if that file is ever exposed.
