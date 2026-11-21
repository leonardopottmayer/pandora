# Releasing

Pandora follows the [devkit workflow](https://github.com/leonardopottmayer/devkit/tree/main/plugins/workflow/handbook)
at **stage 3**: work goes to `dev` through pull requests, and merging `dev` into `main` **is** the
release. A bot computes the version from the commit messages, writes it, tags it and publishes the
GitHub Release; the tag then builds the Docker images that prod runs. Nobody types a version number.

This page has Pandora's specifics and every command used to set the repository up, so it can be
redone from scratch. The rules behind them are in the devkit handbook (`branching.md`, `releasing.md`,
`security.md`).

## The flow

```
feat/x ──PR (squash)──▶ dev ──PR (merge commit)──▶ main ──▶ bot: VERSION, CHANGELOG.md, tag vX.Y.Z, GitHub Release
             │                                       │
             │ push to dev                           │ tag vX.Y.Z
             ▼                                       ▼
   images :dev  ──▶ staging                 images :X.Y.Z, :latest ──▶ prod (pinned :X.Y.Z)

dev ◀── PR (merge commit, auto-merged) ── main   (bot: brings the version bump back)
```

1. Every change goes through a branch from `dev` and a pull request to `dev`
   (`gh pr create --base dev`), squash-merged once CI is green.
2. Each push to `dev` publishes the `:dev` images. **Staging runs `:dev`**, so it shows what the next
   release will ship: check it there before releasing.
3. To release, open the pull request `dev` → `main`. The **Release preview** comment says which version
   it will release. Merge it with a **merge commit**.
4. The release bot commits `chore(release): X.Y.Z` (the `VERSION` file and `CHANGELOG.md`) to `main`,
   tags `vX.Y.Z`, publishes the GitHub Release, and opens the `main` → `dev` pull request, which merges
   itself once CI passes.
5. The tag builds the images `:X.Y.Z` and `:latest`. Pin prod to `:X.Y.Z` and deploy it
   ([deployment](deployment.md#day-2-operations)), running `migris apply prod` first when the release has
   migrations.

## Pandora's differences from the devkit templates

| Devkit | Pandora | Why |
|---|---|---|
| Version in `Directory.Build.props` | Version in the root **`VERSION`** file. The bot writes it (`prepareCmd: echo X.Y.Z > VERSION`) | One file read by the backend, the desktop and the image builds (lockstep) |
| `global.json` pins the SDK | No `global.json`; CI installs `10.0.x` | The development PC only has a newer SDK, which still builds `net10.0` |
| pnpm | npm, in `client-web/` (`.nvmrc` there too) | That is what the web client uses |
| Stage 4 publishes packages | Docker images on GHCR, built by `build-images.yml` from the release tag | Pandora is an app, not a library |
| — | The CI restore needs `NUGET_GITHUB_TOKEN` (repository **and** Dependabot secret) | The `Pottmayer.Tars.*` packages live on GitHub Packages |
| — | Dependabot ignores `SixLabors.ImageSharp` 3.2 and later | They fail the Release build without a license key |
| — | The desktop is left out of the releases: `desktop-release.yml` runs only by hand (draft) | Decided for 1.0; to revisit when the desktop is shipped |

## Workflows

| File | Runs on | Does |
|---|---|---|
| `ci-dotnet.yml` | pull requests and pushes to `dev`/`main` | restore (with the Tars feed), Release build, all backend tests (integration ones with Testcontainers). Check **`Build & Test (.NET)`** |
| `ci-node.yml` | same | `npm ci`, lint, build, tests of `client-web`. Check **`Build & Test (Node)`** |
| `release-preview.yml` | pull requests to `main` | comments the version the merge will release |
| `release.yml` | push to `main` | semantic-release with the release bot's token, then the `main` → `dev` pull request |
| `build-images.yml` | push to `dev`, tag `v*`, manual | images on GHCR: `:dev`, or `:X.Y.Z` + `:latest`; always `:sha-<commit>` |
| `desktop-release.yml` | manual only | a draft desktop release ([client-desktop](../../client-desktop/README.md#release)) |

## One-time repository setup

Done once, on 2026-11-21. Commands run in Git Bash from the repository folder (`gh` fills
`{owner}/{repo}`); the ones that handle a secret are run by the owner in their own terminal.

### 1. Merge, security and ruleset settings

```bash
# Squash for feature PRs, merge commits for dev → main, no rebase, delete merged branches, auto-merge for the bot.
gh repo edit --enable-squash-merge --enable-merge-commit --enable-rebase-merge=false --delete-branch-on-merge --enable-auto-merge

# Dependabot alerts and security updates, secret scanning with push protection.
gh api -X PUT repos/{owner}/{repo}/vulnerability-alerts
gh api -X PUT repos/{owner}/{repo}/automated-security-fixes
gh api -X PATCH repos/{owner}/{repo} -f "security_and_analysis[secret_scanning][status]=enabled" -f "security_and_analysis[secret_scanning_push_protection][status]=enabled"
```

Rulesets for `main` and `dev`, from the devkit template (`repo-setup/templates/ruleset.json`) with
`BRANCH` set to the branch and both CI checks required. The files live outside the repository:

```bash
for b in main dev; do
  sed -e "s/BRANCH/$b/g" \
      -e 's/{ "context": "CHECK" }/{ "context": "Build \& Test (.NET)" }, { "context": "Build \& Test (Node)" }/' \
      ruleset.json > /tmp/ruleset-$b.json
  gh api repos/{owner}/{repo}/rulesets --method POST --input /tmp/ruleset-$b.json
done
gh api repos/{owner}/{repo}/rulesets --jq '.[].name'
```

Each ruleset: changes only through a pull request (zero approvals), both checks green, no force push,
no deletion. They were created after the CI adoption PR merged into `dev` (its checks had to exist
first): `main` is ruleset 27805250, `dev` 27805252. To check one:

```bash
gh api repos/{owner}/{repo}/rulesets/<id> --jq '{name, include: .conditions.ref_name.include, checks: [.rules[]|select(.type=="required_status_checks")|.parameters.required_status_checks[].context], rules: [.rules[].type], bypass: .bypass_actors}'
```

### 2. CI secrets

`NUGET_GITHUB_TOKEN` (PAT, `read:packages`) as a repository secret and as a Dependabot secret
([deployment](deployment.md#1-ci-token-restore-of-the-private-tars-packages)).

### 3. The release bot

The account's release bot (a GitHub App, Contents and Pull requests read/write, no webhook) already
serves devkit and tars. For Pandora:

1. On the App's page → *Install App* → the account → add **pandora** to the selected repositories.
2. Its Client ID as a variable (not a secret):
   ```bash
   gh variable set RELEASE_APP_CLIENT_ID --body "<client id>"
   ```
3. Its private key as a secret, by the owner, in PowerShell (the `.pem` never goes through chat):
   ```powershell
   Get-Content "path\to\the-key.pem" -Raw | gh secret set RELEASE_APP_PRIVATE_KEY
   gh secret list
   ```
4. The bot bypasses the `main` ruleset (only `main`; it reaches `dev` through its pull request).
   `<APP_ID>` is the App's numeric ID from its settings page (not the Client ID): `5465863`.
   ```bash
   id=$(gh api repos/{owner}/{repo}/rulesets --jq '.[] | select(.name == "main") | .id')
   gh api repos/{owner}/{repo}/rulesets/$id --jq '{name, target, enforcement, conditions, rules, bypass_actors: (.bypass_actors + [{actor_id: <APP_ID>, actor_type: "Integration", bypass_mode: "always"}])}' > /tmp/main-ruleset.json
   gh api -X PUT repos/{owner}/{repo}/rulesets/$id --input /tmp/main-ruleset.json
   ```

### 4. First version

Pandora had no tags before 1.0, so the first automated release is **1.0.0** (semantic-release starts
from `1.0.0` when there is no previous tag). The `VERSION` file said `0.1.0` until then.

## Routine

```bash
# a change
git switch dev && git pull
git switch -c feat/something
# ...commit...
git push -u origin feat/something
gh pr create --base dev
gh pr merge --squash          # once CI is green

# a release
gh pr create --base main --head dev --title "Release $(date +%F)" --body "Release of the changes on dev."
gh pr view --comments         # the release preview: which version
gh pr merge --merge           # merge commit, never squash
gh run watch                  # the Release run
gh release view               # the result
gh pr list --base dev --head main   # the bot's sync PR: closes by itself
```

Never create tags, edit `VERSION` or edit `CHANGELOG.md` by hand: the bot computes the next version from
the last tag.
