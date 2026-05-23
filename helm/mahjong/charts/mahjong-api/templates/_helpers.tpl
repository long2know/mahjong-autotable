{{/*
Phase K Wave 7 — Apone (DevOps).

Common helpers for the mahjong-api subchart. Pattern lifted from
`helm create` boilerplate + tightened for our naming conventions.

`mahjong-api.fullname` returns the release-qualified resource name
(e.g. `mahjong-api` or `<release>-mahjong-api`). We deliberately
SHORTEN to `mahjong-autotable` when the chart is installed without
a release-name prefix so the Service/Ingress names match the
existing Kustomize tree (zero-friction migration).
*/}}

{{- define "mahjong-api.name" -}}
mahjong-autotable
{{- end }}

{{- define "mahjong-api.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{ .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else -}}
mahjong-autotable
{{- end -}}
{{- end }}

{{- define "mahjong-api.labels" -}}
app.kubernetes.io/name: mahjong-autotable
app.kubernetes.io/part-of: mahjong-autotable
app.kubernetes.io/component: api
app.kubernetes.io/managed-by: {{ .Release.Service | quote }}
app.kubernetes.io/instance: {{ .Release.Name | quote }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "mahjong-api.selectorLabels" -}}
app.kubernetes.io/name: mahjong-autotable
app.kubernetes.io/component: api
{{- end }}

{{/*
Resolve the image reference. `tag` falls back to `.Chart.AppVersion`
when empty so the umbrella's appVersion is the chart-wide default.
*/}}
{{- define "mahjong-api.image" -}}
{{- $reg := .Values.image.registry | default "ghcr.io" -}}
{{- $repo := .Values.image.repository -}}
{{- $tag := .Values.image.tag | default .Chart.AppVersion -}}
{{ printf "%s/%s:%s" $reg $repo $tag }}
{{- end }}
