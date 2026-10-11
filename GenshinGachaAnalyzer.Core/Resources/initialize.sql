PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;
PRAGMA synchronous = FULL;
BEGIN IMMEDIATE;
CREATE TABLE IF NOT EXISTS accounts (
    uid TEXT PRIMARY KEY NOT NULL,
    nickname TEXT,
    lang TEXT,
    last_updated TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS records (
    id TEXT PRIMARY KEY NOT NULL,
    uid TEXT NOT NULL REFERENCES accounts(uid),
    gacha_type TEXT NOT NULL CHECK(gacha_type IN ('100','200','301','302','400','500')),
    item_id TEXT NOT NULL,
    name TEXT,
    item_type TEXT,
    rank_type TEXT,
    count TEXT,
    time TEXT NOT NULL,
    source TEXT NOT NULL CHECK(source IN ('游戏获取','UIGF导入'))
);
CREATE INDEX IF NOT EXISTS ix_records_uid_pool_time ON records(uid, gacha_type, time, id);
-- 数据原则在数据库层同样生效：禁止逐条修改、删除；明确确认后的整账号清空由专用维护事务执行。
CREATE TRIGGER IF NOT EXISTS immutable_records_update BEFORE UPDATE ON records
BEGIN SELECT RAISE(ABORT, '抽卡记录不可修改'); END;
CREATE TRIGGER IF NOT EXISTS immutable_records_delete BEFORE DELETE ON records
BEGIN SELECT RAISE(ABORT, '抽卡记录不可删除'); END;
PRAGMA user_version = 1;
COMMIT;

