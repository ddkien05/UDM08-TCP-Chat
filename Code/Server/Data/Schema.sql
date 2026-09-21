PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Users (
    UserId       INTEGER PRIMARY KEY AUTOINCREMENT,
    Username     TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    DisplayName  TEXT NOT NULL,
    AvatarUrl    TEXT,
    CreatedAt    TEXT NOT NULL DEFAULT (datetime('now')),
    IsOnline     INTEGER NOT NULL DEFAULT 0
);