# Code signing policy

## Current status

This project is preparing to apply to the SignPath Foundation program. It is not currently enrolled, sponsored, approved, or signed by SignPath.
Current preview binaries are unsigned. Do not interpret this document as a signing endorsement or as a request to disable Windows protections.

## Proposed roles

- Maintainer, committer and reviewer: [g-kari](https://github.com/g-kari)
- Proposed release/signing approver: [g-kari](https://github.com/g-kari)

The maintainer must enable and retain MFA on GitHub and the signing service before enrollment, and review contributions and changes to CI/build scripts.
The roles above describe the intended policy; they do not assert that SignPath permissions have already been configured.

## Artifacts

Only this project's own application and icon are eligible for this project's signing request.
Minecraft, Java, MOD packs, playit, user worlds, credentials, and external binaries are excluded from the signing artifact.
Product metadata identifies Astral Server Manager; all product/file versions match within a build.
Only CI artifacts built from a reviewed repository revision should be submitted. Each signing request requires manual approval.

## Future attribution

After actual approval and enrollment, the project home and release pages will include the required statement:

> Free code signing provided by SignPath.io, certificate by SignPath Foundation.

This attribution is a future requirement, not a claim of current support.

## Privacy

[Privacy policy](PRIVACY.md). This program will not transfer information to other networked systems unless specifically requested by the user or operator.

## Requirements

[SignPath Foundation terms](https://signpath.org/terms.html). Approval is discretionary; a new project is not guaranteed acceptance.
