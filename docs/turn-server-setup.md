# TURN / STUN server — production setup

Phase K Wave 2 (Apone, DevOps).

This runbook covers production provisioning of the coturn-based TURN
server that the Voice / video lane (Bishop's `WebRtcVoiceHub` +
Hicks's `voice.ts` client) negotiates ICE candidates through. The
k8s manifests live at:

* `infra/k8s/base/turn-server.yaml` — coturn 4.6 Deployment +
  ConfigMap + LoadBalancer Service + ExternalSecret stub.
* `infra/k8s/overlays/turn/` — dedicated Kustomize overlay that fills
  in the production realm + external-ip + repoints the ExternalSecret
  at the real `aws-secrets-manager-prod` store. Apply with
  `kubectl apply -k infra/k8s/overlays/turn`.
* `infra/k8s/overlays/{prod,staging}/turn-server-patch.yaml` +
  `turnserver-{prod,staging}.conf` — convenience templates if the
  operator prefers to roll TURN into the existing prod/staging overlay
  bundles rather than as a stand-alone overlay.

## 1. Pre-requisites (operator action; DevOps does NOT touch production secrets)

### 1.1 Provision SSM parameters

The production SSM key family is `/mahjong/prod/turn/*`. Provision the
three keys out-of-band:

```bash
aws ssm put-parameter \
    --name /mahjong/prod/turn/realm \
    --type SecureString \
    --value 'turn.mahjong.example.com'

aws ssm put-parameter \
    --name /mahjong/prod/turn/username \
    --type SecureString \
    --value '<rotate quarterly — see §6>'

aws ssm put-parameter \
    --name /mahjong/prod/turn/password \
    --type SecureString \
    --value '<rotate quarterly — see §6>'
```

### 1.2 IAM

The `aws-secrets-manager-prod` ClusterSecretStore (defined in your ESO
bundle) MUST be bound to an IAM role with the following minimum scope:

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Action": ["ssm:GetParameter", "ssm:GetParameters"],
    "Resource": "arn:aws:ssm:*:*:parameter/mahjong/prod/turn/*"
  }]
}
```

### 1.3 DNS

After applying the overlay, the LoadBalancer Service receives an
external IP. Provision a public A-record for the realm:

```
turn.mahjong.example.com.   60 IN A <LB-public-IP>
```

WebRTC clients dial `stun:turn.mahjong.example.com:3478` and
`turn:turn.mahjong.example.com:3478?transport=udp` against this
record.

### 1.4 TLS (Phase L follow-up)

Phase K Wave 2 ships TURN over UDP/TCP only. The `turns:` port (5349)
is exposed but the base manifest does NOT mount a TLS certificate.
Phase L will add:

* cert-manager Certificate resource issuing a Let's Encrypt cert for
  `turn.mahjong.example.com`.
* coturn `cert=/etc/coturn/tls/tls.crt` + `pkey=/etc/coturn/tls/tls.key`
  config knobs.
* mTLS optional knob for the API ↔ TURN signalling lane.

Today, clients that require `turns:` (corporate firewalls blocking
:3478) will fail until Phase L. The plain `turn:` lane covers the
public-internet case.

## 2. Apply the overlay

```bash
# Verify the SSM keys are reachable from the cluster.
kubectl -n mahjong-prod get clustersecretstore aws-secrets-manager-prod \
    -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}'
# Expect: True

# Apply the dedicated TURN overlay.
kubectl apply -k infra/k8s/overlays/turn

# Capture the LB external IP — then come back and patch
# `external-ip=…` in the configmap (operator step §3).
kubectl -n mahjong-prod get service turn-server \
    -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

## 3. Fill in the LB public IP

The base ConfigMap ships `external-ip=STUB_REPLACE_IN_OVERLAY`. The
overlay's `configmap-patch.yaml` ships `external-ip=REPLACE_WITH_LB_PUBLIC_IP`.
Both block coturn startup until replaced.

Edit `infra/k8s/overlays/turn/configmap-patch.yaml`, replace
`REPLACE_WITH_LB_PUBLIC_IP` with the captured LB IP, then re-apply:

```bash
kubectl apply -k infra/k8s/overlays/turn
kubectl -n mahjong-prod rollout restart deployment turn-server
```

Confirm the rollout succeeded:

```bash
kubectl -n mahjong-prod logs -l app.kubernetes.io/name=turn-server --tail=20
# Expect: "Listening on 0.0.0.0:3478" + no "STUB_REPLACE_IN_OVERLAY" lines.
```

## 4. ICE-server URL contract (Bishop's `/api/turn`)

The Wave 3 `/api/turn` endpoint should return an ICE-server list of
the shape:

```json
{
  "iceServers": [
    { "urls": ["stun:turn.mahjong.example.com:3478"] },
    {
      "urls": [
        "turn:turn.mahjong.example.com:3478?transport=udp",
        "turn:turn.mahjong.example.com:3478?transport=tcp"
      ],
      "username": "<HMAC time-limited username — see §5>",
      "credential": "<HMAC time-limited credential>"
    }
  ],
  "ttl": 3600
}
```

`ttl` (seconds) should match the HMAC time-limited credential expiry
so clients refresh the ICE list before their credential expires.

## 5. HMAC time-limited credentials (Wave 3 migration)

Phase K Wave 2 ships `lt-cred-mech` (long-term username + password,
static credentials in SSM). Wave 3 migrates to coturn's
`use-auth-secret` mode where:

* coturn shares a single HMAC secret with the API (via SSM).
* `/api/turn` mints per-session usernames of the form
  `<expiry-unix-timestamp>:<session-id>` and the matching credential
  is `HMAC-SHA1(shared-secret, username) | base64`.
* coturn validates the HMAC on the fly; no per-user state.

Migration steps (Wave 3, NOT Wave 2):

1. Provision `/mahjong/prod/turn/auth_secret` in SSM with a 64-byte
   random value.
2. Patch the overlay ConfigMap: replace `lt-cred-mech` with
   `use-auth-secret` + `static-auth-secret-file=/etc/coturn/secrets/auth_secret`.
3. Extend the ExternalSecret to mount the auth_secret as
   `/etc/coturn/secrets/auth_secret`.
4. Bishop's `/api/turn` mints credentials as described above.

The Wave-2 static-credential mode is acceptable for Phase L bringup
(small user base, manual rotation tolerable). Migrate to HMAC before
Phase M (scale-out).

## 6. Quarterly rotation

Per `docs/secret-rotation.md`, TURN credentials rotate quarterly
(same cadence as OAuth client secrets). Procedure:

1. Generate a new password (e.g. `openssl rand -base64 32`).
2. `aws ssm put-parameter --name /mahjong/prod/turn/password --type
   SecureString --value '…' --overwrite`.
3. ESO refresh-interval is 1h (per the ExternalSecret spec); within
   one hour the Secret resource picks up the new value.
4. `kubectl -n mahjong-prod rollout restart deployment turn-server`
   forces a fresh `--user-file` read. (coturn does NOT auto-reload
   the file.)
5. Verify with a fresh ICE negotiation from a test client.

During rotation, existing WebRTC sessions remain valid until their
ICE relay candidate ages out (default: relay lifetime is 600 s). A
single coordinated restart works for our user base; if the user base
grows beyond ~10k concurrent voice sessions, switch to the HMAC
time-limited mode (§5) for zero-downtime rotation.

## 7. Observability

coturn writes structured logs to stdout (`simple-log` + `log-file=stdout`
in the ConfigMap). They appear under the `turn-server` Deployment in
the cluster logs. Key log shapes to alert on:

* `User <user> rejected: wrong credential` — credential mismatch
  (likely a stale client cache; check rotation overlap window).
* `0: ERROR: failed to bind UDP socket` — port conflict; check the
  Service has `externalTrafficPolicy: Local` and the LB target ports
  match the container ports.
* `0: WARN: realm mismatch` — operator forgot to update the
  ConfigMap after rotating `/mahjong/prod/turn/realm`.

## 8. Cross-references

* `docs/secret-rotation.md` — broader rotation cadence policy.
* `docs/kubernetes.md` — base + overlays architecture.
* `infra/k8s/overlays/turn/` — the dedicated overlay applied by §2.
* `.github/workflows/multi-arch-runtime.yml` — runtime gate for the
  API; does NOT exercise the TURN lane (load-test only).
