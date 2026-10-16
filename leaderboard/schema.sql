PRAGMA journal_mode=WAL;
CREATE TABLE IF NOT EXISTS fights (
 id TEXT PRIMARY KEY,
 created TEXT NOT NULL,
 visibility TEXT NOT NULL CHECK(visibility IN ('private','unlisted','public')),
 verification TEXT NOT NULL DEFAULT 'unverified' CHECK(verification = 'unverified'),
 owner_hash TEXT NOT NULL,
 share_hash TEXT NOT NULL,
 payload TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS fights_public ON fights(visibility,created);
CREATE UNIQUE INDEX IF NOT EXISTS fights_owner ON fights(owner_hash);
