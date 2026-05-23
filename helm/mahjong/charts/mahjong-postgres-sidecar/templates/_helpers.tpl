{{- define "mahjong-postgres.fullname" -}}
mahjong-postgres
{{- end }}

{{- define "mahjong-postgres.labels" -}}
app.kubernetes.io/name: mahjong-postgres
app.kubernetes.io/part-of: mahjong-autotable
app.kubernetes.io/component: database
app.kubernetes.io/managed-by: {{ .Release.Service | quote }}
app.kubernetes.io/instance: {{ .Release.Name | quote }}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "mahjong-postgres.selectorLabels" -}}
app.kubernetes.io/name: mahjong-postgres
app.kubernetes.io/component: database
{{- end }}
