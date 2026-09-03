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

CREATE TABLE IF NOT EXISTS Conversations (
    ConversationId INTEGER PRIMARY KEY AUTOINCREMENT,
    IsGroup        INTEGER NOT NULL DEFAULT 0,
    Name           TEXT,
    CreatedAt      TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS ConversationMembers (
    ConversationId INTEGER NOT NULL REFERENCES Conversations(ConversationId),
    UserId         INTEGER NOT NULL REFERENCES Users(UserId),
    JoinedAt       TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (ConversationId, UserId)
);

CREATE TABLE IF NOT EXISTS Messages (
    MessageId              INTEGER PRIMARY KEY AUTOINCREMENT,
    ConversationId         INTEGER NOT NULL REFERENCES Conversations(ConversationId),
    SenderId               INTEGER NOT NULL REFERENCES Users(UserId),
    Content                TEXT NOT NULL,
    ReplyToMessageId       INTEGER REFERENCES Messages(MessageId),
    ForwardedFromMessageId INTEGER REFERENCES Messages(MessageId),
    SentAt                 TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IX_Messages_ConversationId ON Messages(ConversationId, SentAt);