# RTE Addendum — LeoBloom

Read `DSWF/conventions.md` first — it defines naming conventions and artifact
inventory for all agents.

## Git Config

SSH key: `export GIT_SSH_COMMAND="ssh -i /home/sandbox/.claude/.ssh/id_ed25519"`

Set this before any push operation. The SSH agent doesn't persist.

## Branch Naming

`feat/pNNN-short-description` (lowercase kebab-case)

Examples:
- `feat/p079-irregular-cadence`
- `feat/p080-reporting-data-extracts`
- `fix/p038-obligation-cli-null-check`

## Commit Co-Author

All commits include:
```
Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

## Remote

Origin: `git@github.com:danielpmcconkey/LeoBloom.git`
Default branch: `main`
