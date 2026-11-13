-- Additive update for JWT sign-in (SDS 3.2, III.1.1). Run against the WPSM database used by the application.
-- One row per issued refresh token; only the SHA-256 hash is stored. Users.RefreshToken* columns are no longer used.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
CREATE TABLE dbo.RefreshTokens (
    RefreshTokenId    INT IDENTITY(1,1) CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId            INT          NOT NULL CONSTRAINT FK_RefreshTokens_User REFERENCES dbo.Users(UserId),
    TokenHash         CHAR(64)     NOT NULL CONSTRAINT UQ_RefreshTokens_Hash UNIQUE,
    ExpiresAt         DATETIME2(0) NOT NULL,
    AbsoluteExpiresAt DATETIME2(0) NOT NULL,
    RevokedAt         DATETIME2(0) NULL,
    ReplacedByTokenId INT          NULL CONSTRAINT FK_RefreshTokens_ReplacedBy REFERENCES dbo.RefreshTokens(RefreshTokenId),
    CreatedAt         DATETIME2(0) NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedByIp       NVARCHAR(45) NULL,
    CONSTRAINT CK_RefreshTokens_Expiry CHECK (ExpiresAt <= AbsoluteExpiresAt)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RefreshTokens_User' AND object_id=OBJECT_ID('dbo.RefreshTokens'))
    CREATE INDEX IX_RefreshTokens_User ON dbo.RefreshTokens(UserId, RevokedAt);
GO
