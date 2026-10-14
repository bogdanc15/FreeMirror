# Security and privacy

Use FreeMirror on a trusted private network. The receiver is discoverable locally
and this version does not enable an access PIN by default. Do not forward its
ports to the internet. The controller uses TCP/UDP ports 55300–55302; Bonjour
handles local discovery. A separate upstream receiver process receives the stream.

FreeMirror has no account system, analytics, crash upload or automatic update
service. Setup downloads a pinned HTTPS GitHub release and verifies its SHA-256
before extraction. It does not execute downloaded content during setup.

The controller temporarily changes the upstream per-user arguments file and
restores it on normal stop. A forced termination or power loss can prevent that
restoration; keep a backup of customized UxPlay settings. Do not edit upstream
settings concurrently. The upstream receiver may separately store preferences,
pairing information or other local state according to its implementation.

Diagnostic command-line tests can create a result file and an image of the
controller. They remain local and are excluded from release packaging. Review
and redact any diagnostics before sharing. Do not attach a personal mirrored
screen or unrelated desktop capture to an issue.

Report vulnerabilities using GitHub private vulnerability reporting if enabled.
If unavailable, open a minimal issue requesting a private reporting channel;
do not publish exploit details, secrets or personal data in that issue.

The public source is exported from an explicit file allowlist with a fresh Git
history and a GitHub no-reply author address. Local development history and raw
investigation artifacts are not included. Automated scans reduce accidental
exposure but cannot guarantee the absence of every possible secret.
