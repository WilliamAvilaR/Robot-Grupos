USE [DataColorApp]
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OtpChallenge')
BEGIN
    CREATE TABLE [dbo].[OtpChallenge] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [AccountId] NVARCHAR(100) NOT NULL,
        [CorrelationId] NVARCHAR(200) NULL,
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        [Code] NVARCHAR(10) NULL,
        [ExpiresAt] DATETIME2 NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UsedAt] DATETIME2 NULL,
        INDEX IX_OtpChallenge_AccountId ([AccountId]),
        INDEX IX_OtpChallenge_Status ([Status]),
        INDEX IX_OtpChallenge_ExpiresAt ([ExpiresAt])
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ScrapeResult')
BEGIN
    CREATE TABLE [dbo].[ScrapeResult] (
        [Id] UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        [AccountId] NVARCHAR(100) NOT NULL,
        [Url] NVARCHAR(2000) NOT NULL,
        [CapturedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [PayloadJson] NVARCHAR(MAX) NULL,
        [ContentHash] NVARCHAR(64) NULL,
        [HtmlSnapshotPath] NVARCHAR(500) NULL,
        INDEX IX_ScrapeResult_AccountId ([AccountId]),
        INDEX IX_ScrapeResult_CapturedAt ([CapturedAt]),
        INDEX IX_ScrapeResult_ContentHash ([ContentHash])
    );
END
GO
