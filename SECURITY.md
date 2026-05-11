# Security Policy

## Supported Versions

SensitiveFlow is currently in preview. Security fixes are provided for the latest published preview version.

| Version | Supported |
| ------- | --------- |
| latest preview | Yes |
| older previews | No |

## Reporting a Vulnerability

If you discover a security vulnerability in SensitiveFlow, please do **not** open a public issue.

Instead, report it privately by email:

trabalhomateusjs521@gmail.com

Please include:

- affected package
- affected version
- reproduction steps
- expected behavior
- actual behavior
- potential impact
- suggested fix, if available

I will try to acknowledge valid reports as soon as possible and coordinate a fix before public disclosure.

## Scope

Security-sensitive areas include:

- log redaction failures
- accidental exposure of raw sensitive values
- audit trail integrity issues
- pseudonymization/token store weaknesses
- unsafe defaults
- analyzer/code fix behavior that may introduce leaks

## Non-goals

SensitiveFlow helps reduce accidental exposure of sensitive data, but it does not guarantee legal compliance or complete data protection by itself. You are responsible for how you use these primitives in your application.
