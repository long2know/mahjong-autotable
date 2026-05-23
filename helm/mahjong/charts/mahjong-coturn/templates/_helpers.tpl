{{- define "mahjong-coturn.fullname" -}}
mahjong-coturn
{{- end }}

{{- define "mahjong-coturn.labels" -}}
app.kubernetes.io/name: mahjong-coturn
app.kubernetes.io/part-of: mahjong-autotable
app.kubernetes.io/component: turn
app.kubernetes.io/managed-by: {{ .Release.Service | quote }}
app.kubernetes.io/instance: {{ .Release.Name | quote }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "mahjong-coturn.selectorLabels" -}}
app.kubernetes.io/name: mahjong-coturn
app.kubernetes.io/component: turn
{{- end }}
