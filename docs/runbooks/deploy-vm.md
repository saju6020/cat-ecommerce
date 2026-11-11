# Deploying PurrCare to the Google Cloud VM

Owner decision 2026-10-18. Everything runs on one e2-medium VM: PostgreSQL, ClamAV, the API, the website and Caddy (HTTPS). Uploaded files go to a private Cloud Storage bucket.

## 1. What runs where

```
browser --https--> Caddy :443 --/api/*, /health--> api :5000 --> postgres :5432 (volume pgdata)
                         \--everything else----> web :3000      \-> clamav :3310 (every upload is scanned)
                                                                 \-> gs://<files bucket> (VM service account, no key file)
```

- **Address.** The site has a single address, `SITE_HOST`.
  - Until a domain is pointed at the VM, it is `<ip-with-dashes>.sslip.io`.
  - The website and the API share that origin, so there is no CORS and no cross-site cookie.
- **Source and settings.** The source is at `/opt/purrcare/src`, a `git archive` of the branch being deployed.
  - Settings and secrets are in `/opt/purrcare/.env` (mode 600, written once by `make-env.sh`).
  - Secrets are never printed, never committed, and never baked into an image.
- **Database roles.** The API connects as `catecommerce_app`. Migrations run as `catecommerce_migrator` (see `deploy/database/roles.sql`). The API refuses to start as a superuser or a table owner.
- **Files bucket.** It must keep uniform access on, public access prevention enforced, versioning off and soft-delete **off**. The API checks all four at startup and refuses to start otherwise.

## 2. First deployment

On your PC:
1. Build the release data: `catecommerce_release.dump`. It holds:
   - the catalogue, stock and settings;
   - the admin `e2e.admin@purrcare.test` only;
   - no customers, orders or bookings.
2. Copy the photos to the bucket under the same paths: `gcloud storage cp -r <LocalStoreRoot>/ProductImage gs://<files bucket>/`.
3. Build the API: `deploy/vm/scripts/publish-api.sh`. Copy `deploy/vm/out/api-build.tar.gz` to `/opt/purrcare/` on the VM.
   - The API uses the private `Platform.Infrastructure.*` packages. Their feed (gitlab.bracits.com) can't be reached from the VM, so the API is built on a developer machine.
   - The VM only packages the result, and the website is built on the VM.

On the VM:
1. Install Docker Engine and the compose plugin.
2. Unpack the source into `/opt/purrcare/src`.
3. Write the settings: `deploy/vm/scripts/make-env.sh <site-host> <files-bucket> <backup-bucket>`.
4. Build the images: `deploy/vm/scripts/build.sh`. This builds the API, the migrator and the website; the website is built for `https://<site-host>`.
5. Set up the database: `deploy/vm/scripts/init-db.sh /opt/purrcare/catecommerce_release.dump`. This step:
   - restores the dump;
   - creates the roles;
   - hands every object to the migrator;
   - runs the migrations.
6. Start everything: `deploy/vm/scripts/compose.sh up -d`. Caddy gets the certificate within a minute. ClamAV needs up to 5 minutes on first start to load its virus database; uploads are refused until then.
7. Add the nightly backup to root's crontab (see the header of `scripts/backup-db.sh`).

## 3. Updating to a new version

1. Unpack the new source into `/opt/purrcare/src`. Copy a fresh `api-build.tar.gz` from `publish-api.sh` to `/opt/purrcare/`.
2. Run `deploy/vm/scripts/build.sh`.
3. Run `deploy/vm/scripts/migrate.sh` (safe when there is no new migration).
4. Run `deploy/vm/scripts/compose.sh up -d api web`.

**Rollback:**
- Before building, tag the running images: `docker tag purrcare-api:latest purrcare-api:previous`, and the same for `purrcare-web`.
- To roll back, tag `previous` back to `latest` and run `compose.sh up -d api web`.
- A migration that already ran is not undone. Restore the database from a backup if it must be.

## 4. Checks

- `curl https://<site-host>/health` shows `Healthy`.
- `compose.sh logs api | grep -i "startup"` shows no failed guard.
- `compose.sh ps` shows every service running and `healthy` where it has a health check.
- `free -m` shows memory headroom.

## 5. Backups (interim)

- `scripts/backup-db.sh` dumps the database every night.
  - It keeps 7 days in `/opt/purrcare/backups`.
  - It copies each dump to `gs://<backup bucket>/db/`.
- To restore: `pg_restore` into an empty database, re-run `roles.sql`, then start the API.
- The full point-in-time setup in `deploy/backup` (pgBackRest, WAL archiving, two repositories) replaces this once its off-site repository is configured. See `backup-and-restore.md`.

## 6. Not yet available

- **Email and SMS.** No provider exists, so password resets and order emails are not sent.
  - The admin signs in with the password they already have.
  - New staff passwords can't be set by email yet.
- **Domain.** When one is bought:
  1. Point its A record at the VM's static IP.
  2. Change `SITE_HOST` in `.env`.
  3. Rebuild the website (`build.sh`); the address is built into it.
  4. Run `compose.sh up -d`.

## 7. Product photos straight from the bucket (ADR-370)

- The API hands the browser signed `storage.googleapis.com` links to small WebP copies (`.w480.webp` for cards, `.w1200.webp` for the product page). Other files still go through `/api/files/...`.
- The VM's account `purrcare-vm` signs those links through Google's IAM signing service. This needs two things, both set up on 2026-10-18:
  - the IAM Service Account Credentials API is enabled;
  - `purrcare-vm` holds `roles/iam.serviceAccountTokenCreator` on itself.
  If either is missing, photos fall back to `/api/files/...` and the API log warns.
- Copies are made at upload. At every start the API also makes any copy that is missing.
- To switch direct serving off, set `DIRECT_SERVE_PURPOSES=none` in `.env` and run `compose.sh up -d api`.


## 8. Shut down (2026-11-11) and bringing it back

**Current state:** shut down on the owner's request.
- The VM `purrcare`, its disk and the static IP `purrcare-ip` (34.180.8.45) are deleted.
- What is kept:
  - snapshot `purrcare-shutdown-20261111` (asia-south1, 30 GB disk, about 4.4 GB stored);
  - the photos and backups buckets (the test bucket `purrcare-files-test-448913` was deleted the same day, on the owner's request);
  - the firewall rules;
  - the `purrcare-vm` service account and its roles.
- Before shutdown, the database was dumped to `purrcare-backups-448913` (`catecommerce-20261111T103458Z.dump`).
- A copy of `/opt/purrcare/.env` is in the git-ignored `gcp-key/purrcare-prod.env` on the owner's PC.
- Cost while off is about $0.3–0.5 a month for the snapshot, plus a few cents for the buckets.

**To bring it back (about 30–40 min):**
1. Create a disk from the snapshot (pd-balanced, 30 GB, asia-south1-a).
2. Create an e2-medium VM that boots from that disk. Use service account `purrcare-vm` with the `cloud-platform` scope, and the network tags the firewall rule `purrcare-allow-web` targets.
3. Reserve a new static IP and attach it. The old 34.180.8.45 is gone.
4. On the VM, set `SITE_HOST=<new-ip-with-dashes>.sslip.io` in `/opt/purrcare/.env`.
5. Run `build.sh` to rebuild the web image, because the API address is built into it.
6. Run `compose.sh up -d`. Caddy gets a new certificate on its own.
7. Check `https://<site-host>/health`.
8. Add the user's SSH key again if it isn't on the snapshot. It should be: `~purrcare/.ssh/authorized_keys` was on the disk.
