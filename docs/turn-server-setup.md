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

### 1.4 TLS for `turns:` (Phase K Wave 3 — shipped)

Phase K Wave 2 deferred TLS to Phase L. **Phase K Wave 3 (Apone)
brought it forward** so corporate firewalls that block plain UDP/TCP
`:3478` can still negotiate ICE candidates via the `turns:` listener
on port `5349`.

Wave 3 additions:

* **Base manifest** ([`infra/k8s/base/turn-server.yaml`](../infra/k8s/base/turn-server.yaml))
  passes `--cert /etc/tls/tls.crt --pkey /etc/tls/tls.key` to coturn
  and mounts the `tls-cert-turn` Secret at `/etc/tls/`.
* **Production overlay** ([`infra/k8s/overlays/prod/turn-tls-secret.yaml`](../infra/k8s/overlays/prod/turn-tls-secret.yaml))
  ships an `ExternalSecret` bound to the `aws-secrets-manager-prod`
  ClusterSecretStore. It materialises the `tls-cert-turn` k8s Secret
  (`type: kubernetes.io/tls`) from SSM parameters
  `/mahjong/prod/turn/tls/{crt,key}`.

#### Operator action (pre-deploy)

1. Provision a public TLS cert for `turn.mahjong.example.com`. Two
   common paths:

   * **cert-manager + Let's Encrypt HTTP-01.** Issue against the
     LoadBalancer's IP-bound DNS A-record (see §1.3 below). Export
     the renewed `fullchain.pem` + `privkey.pem` from the
     cert-manager-managed Secret into SSM via a
     [renewal-hook script](https://cert-manager.io/docs/usage/certificate/)
     so the SSM-backed ExternalSecret stays current.
   * **ACM Public CA with cert export.** AWS ACM can issue public
     certs to non-AWS endpoints (operator-issued export enabled).
     Drop the exported `.crt` + `.key` into SSM directly.

2. Populate the SSM keys:

    ```bash
    aws ssm put-parameter --type SecureString \
        --name /mahjong/prod/turn/tls/crt --value "$(cat fullchain.pem)"
    aws ssm put-parameter --type SecureString \
        --name /mahjong/prod/turn/tls/key --value "$(cat privkey.pem)"
    ```

3. Verify the `aws-secrets-manager-prod` ClusterSecretStore's IAM
   role grants `ssm:GetParameter` on
   `arn:aws:ssm:*:*:parameter/mahjong/prod/turn/tls/*` (extra rule
   alongside the existing `/mahjong/prod/turn/*` Wave-2 grant).

4. Apply the ExternalSecret:

    ```bash
    kubectl -n mahjong-prod apply -f infra/k8s/overlays/prod/turn-tls-secret.yaml
    ```

   ESO materialises `tls-cert-turn` within 1 h (refreshInterval).
   Force-refresh immediately if you'd rather not wait:

    ```bash
    kubectl -n mahjong-prod annotate externalsecret turn-tls-cert \
        force-sync="$(date +%s)" --overwrite
    ```

5. Restart coturn so it binds the new mount:

    ```bash
    kubectl -n mahjong-prod rollout restart deployment turn-server
    ```

   Verify:

    ```bash
    kubectl -n mahjong-prod logs -l app.kubernetes.io/name=turn-server --tail=20
    # Expect: "TLS Listener opened on : 5349" + no "could not load
    # cert/key" errors.
    ```

#### Cert rotation

Let's Encrypt issues 90-day certs. Operator cadence: re-export +
re-put 60 days before expiry, ESO picks up within 1 h, `rollout
restart` binds the new file. cert-manager renewal-hook scripts
automate steps 1–2 → operator workflow becomes "monitor the
`tls-cert-turn` Secret's `tls.crt` for changes; restart on flip".

#### Phase L additions (deferred)

* mTLS for the API ↔ TURN signalling lane (optional knob).
* DTLS over UDP (`tls-listening-port` already configured; coturn
  exposes DTLS by default once `--cert/--pkey` is set, but client
  testing across browsers is a separate Phase L deliverable).

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

## 9. k8s deployment (Phase K Wave 6 — Apone)

Phase K Wave 2 + Wave 3 shipped the single-replica `turn-server.yaml`
and the TLS-on-5349 augmentation. **Wave 6** ships the
production-shape manifests as a parallel set under
`infra/k8s/base/coturn-*.yaml` (the W6 resources are named
`coturn-*` so they can be applied alongside the W2 `turn-server-*`
resources during the cutover; the W2 manifests stay in `base/` for
staging).

Files:

| File | Purpose |
|------|---------|
| `infra/k8s/base/coturn-deployment.yaml` | 2-replica Deployment + LoadBalancer Service + NetworkPolicy. |
| `infra/k8s/base/coturn-configmap.yaml` | `turnserver.conf` with `realm`, `listening-port=3478`, `tls-listening-port=5349`, `fingerprint`, `lt-cred-mech`, `use-auth-secret`. |
| `infra/k8s/base/coturn-secret.yaml` | `ExternalSecret` materialising `coturn-static-auth-secret` (the HMAC key Bishop's W3 `/api/turn` endpoint shares) from SSM `/mahjong/<env>/turn/auth_secret`. |

### 9.1 What's new in Wave 6

* **2 replicas, AZ-spread.** `podAntiAffinity` on
  `topology.kubernetes.io/zone` guarantees the two coturn pods land
  in different AZs; a single-AZ outage keeps the TURN data plane
  reachable. RollingUpdate strategy is pinned `maxSurge=1,
  maxUnavailable=0` so a refresh always spins a fresh pod and waits
  for `Ready` before evicting an old one (rolling restarts do drop
  in-flight ICE channels — be deliberate about rollouts).
* **HMAC-mode (`use-auth-secret`) by default.** W3 documented the
  migration; W6's `coturn-configmap.yaml` enables it out of the
  box. Bishop's `/api/turn` endpoint reads the SAME
  `/mahjong/<env>/turn/auth_secret` SSM parameter, so HMAC
  validation works symmetrically. A rotation flips both sides at
  once.
* **Wider relay port range (49152-65535)** matching the IANA
  ephemeral range. The W2 base used a narrow 49160-49200 (≈40
  ports) which caps concurrency. W6 opens the full range; the
  matching NetworkPolicy admits the same range so a future
  cluster-default-deny baseline doesn't silently break ICE.
* **NetworkPolicy `coturn-relay-ports`** admits UDP 3478,
  TCP 3478, TCP 5349, and UDP 49152-65535. Egress is wide open
  (a TURN server's job is to NAT-traverse to arbitrary public
  peers; restricting egress defeats the purpose).
* **NLB annotations** — Service is annotated for `aws-load-balancer-type=nlb`
  + `target-type=ip` so the W5-provisioned EKS cluster terminates
  the public IP with a low-overhead NLB rather than the default
  classic ELB. `externalTrafficPolicy: Local` preserves the
  client source IP (coturn needs the real IP to mint relay
  candidates).
* **`readOnlyRootFilesystem: true` + `runAsNonRoot: true`.**
  Coturn binds 3478 + 5349 — both >1024 — so it does NOT need
  `CAP_NET_BIND_SERVICE`. Defence-in-depth.

### 9.2 Apply runbook

Pre-requisite: SSM key seeded per §5.1 (HMAC secret).

```bash
# 1. Seed the HMAC secret (one-time per env).
openssl rand -base64 48 | \
    aws ssm put-parameter \
        --name /mahjong/prod/turn/auth_secret \
        --type SecureString \
        --value file:///dev/stdin

# 2. Apply the W6 base resources via the prod overlay (the prod
#    overlay patches `realm`, `external-ip`, and the
#    ClusterSecretStore name).
kubectl apply -k infra/k8s/overlays/prod/

# 3. Verify both replicas land in different AZs.
kubectl -n mahjong-prod get pods -l app.kubernetes.io/name=coturn \
    -o custom-columns=NAME:.metadata.name,NODE:.spec.nodeName,AZ:.spec.topology
# Expect: 2 pods, two different AZs.

# 4. Verify the NetworkPolicy applied.
kubectl -n mahjong-prod describe networkpolicy coturn-relay-ports
# Expect: ports 3478 UDP/TCP, 5349 TCP, 49152-65535 UDP.

# 5. Smoke-test from a client (use `turnutils_uclient` from the
#    coturn package on any laptop):
turnutils_uclient -v \
    -u "$(date -d '+1 hour' +%s):smoke" \
    -w "$(echo -n "$(date -d '+1 hour' +%s):smoke" | openssl dgst -sha1 -hmac "$(aws ssm get-parameter --name /mahjong/prod/turn/auth_secret --with-decryption --query Parameter.Value --output text)" -binary | base64)" \
    turn.mahjong.example.com 3478
# Expect: "RELAY-CONNECT created" within ~2 s.
```

### 9.3 Cutover from Wave 2 → Wave 6

The W2 single-replica `turn-server.yaml` resources (`turn-server`
Deployment + `turn-server` Service + `turn-server-config` ConfigMap)
stay in place for staging. The W6 `coturn-*` resources are
parallel-deployed in prod first; once verified, the operator deletes
the W2 resources in prod:

```bash
kubectl -n mahjong-prod delete deployment turn-server
kubectl -n mahjong-prod delete service turn-server
kubectl -n mahjong-prod delete configmap turn-server-config
# DNS A-record stays — both Services have the same public IP if the
# operator pre-binds via `loadBalancerIP` (single IP, two Services
# disallowed) OR cuts DNS over from the W2 LB to the W6 LB in a
# coordinated step. Recommended: blue-green via separate DNS
# records (`turn.mahjong.example.com` → W6 LB; W2 LB IP
# decommissioned after a 24h cool-down).
```

### 9.4 Cross-references

* `infra/k8s/base/coturn-deployment.yaml` — Deployment + Service +
  NetworkPolicy (W6 production shape).
* `infra/k8s/base/coturn-configmap.yaml` — turnserver.conf.
* `infra/k8s/base/coturn-secret.yaml` — ExternalSecret for
  `coturn-static-auth-secret`.
* `docs/admission-policy.md` — if cluster-default-deny lands, the
  W6 NetworkPolicy is the canonical TURN admit rule (else ICE
  silently breaks).
