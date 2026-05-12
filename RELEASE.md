# Release Process

## Versioning

SensitiveFlow follows [Semantic Versioning](https://semver.org).

While in preview (pre-v1), the version format is:

```
MAJOR.MINOR.PATCH-preview.N
```

| Version           | Meaning                                                |
|-------------------|--------------------------------------------------------|
| `1.0.0-preview.1` | First preview with initial feature set                 |
| `1.0.0-preview.2` | Bug fixes, metadata, or doc updates — no API changes   |
| `1.0.0-preview.3` | Hardening: DB compatibility, fail-fast validation, DX  |
| `1.0.0`           | First stable release                                   |

**While in preview:**
- Bug fixes / metadata / docs → bump preview number (e.g., `preview.1` → `preview.2`)
- New features or breaking changes → bump preview number (e.g., `preview.2` → `preview.3`)
- Do **not** republish the same version — NuGet rejects overwrites

## How to publish a release

The examples below use `<NEW_VERSION>` as a placeholder for the version you're shipping
(e.g. `1.0.0` for the stable release or `1.0.1` for the next patch).

### 1. Ensure everything is merged to `main`

```bash
git checkout main
git pull origin main
```

### 2. Update the version

Edit `Directory.Build.props`:

```xml
<Version><NEW_VERSION></Version>
```

### 3. Update CHANGELOG.md

Move changes from `[Unreleased]` to the new version section.

### 4. Commit and tag

```bash
git add Directory.Build.props CHANGELOG.md
git commit -m "release: v<NEW_VERSION>"
git tag v<NEW_VERSION>
git push origin main
git push origin v<NEW_VERSION>
```

### 5. Create GitHub Release

1. Go to **Releases → Draft a new release**
2. Choose the tag: `v<NEW_VERSION>`
3. Title: `SensitiveFlow v<NEW_VERSION>`
4. Description: copy the relevant section from `CHANGELOG.md`
5. Check **"Set as a pre-release"** *only* if the tag still carries a `-preview.N` suffix
6. Publish

The `release.yml` workflow will automatically build, test (including container tests), pack, and push to NuGet.

## Current version

`1.0.0-preview.3` — latest published preview. Next release: `1.0.0` (first stable).
