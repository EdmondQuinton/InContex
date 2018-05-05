USE [$(DatabaseName)];

GO

IF NOT EXISTS (SELECT * FROM sys.filegroups WHERE name='MOFG_01') 
BEGIN
	ALTER DATABASE [$(DatabaseName)] ADD FILEGROUP MOFG_01 CONTAINS MEMORY_OPTIMIZED_DATA;
	ALTER DATABASE [$(DatabaseName)] ADD FILE (NAME='MOF_01', FILENAME='$(DefaultDataPath)$(DefaultFilePrefix)_MOF_01') TO FILEGROUP MOFG_01;
END

GO
CREATE TYPE dbo.VariableAnalogueTypeVSPK   AS TABLE  
(  
	[VariableID] [int] NOT NULL,
	[SampleDateTimeUTC] [datetime2](7) NOT NULL,
	[PreviousSampleDateTimeUTC] [datetime2](7) NULL,
	[Value] [float] NOT NULL,
	[DeltaValue] [float] NULL,
	[StatusGood] [bit] NOT NULL,
	[StatusCode] [int] NOT NULL
	PRIMARY KEY NONCLUSTERED ([VariableID], [SampleDateTimeUTC])  
)  
WITH  
    (MEMORY_OPTIMIZED = ON);  

GO

CREATE TYPE dbo.VariableAnalogueTypeVSHS   AS TABLE  
(  
	[VariableID] [int] NOT NULL,
	[SampleDateTimeUTC] [datetime2](7) NOT NULL,
	[PreviousSampleDateTimeUTC] [datetime2](7) NULL,
	[Value] [float] NOT NULL,
	[DeltaValue] [float] NULL,
	[StatusGood] [bit] NOT NULL,
	[StatusCode] [int] NOT NULL
	INDEX IX_VS HASH ([VariableID], [SampleDateTimeUTC])
      WITH ( BUCKET_COUNT = 1000 )  
)  
WITH  
    (MEMORY_OPTIMIZED = ON);  

GO

CREATE TYPE dbo.VariableAnalogueTypeSVPK   AS TABLE  
(  
	[VariableID] [int] NOT NULL,
	[SampleDateTimeUTC] [datetime2](7) NOT NULL,
	[PreviousSampleDateTimeUTC] [datetime2](7) NULL,
	[Value] [float] NOT NULL,
	[DeltaValue] [float] NULL,
	[StatusGood] [bit] NOT NULL,
	[StatusCode] [int] NOT NULL
	PRIMARY KEY NONCLUSTERED ([SampleDateTimeUTC], [VariableID])  
)  
WITH  
    (MEMORY_OPTIMIZED = ON);  

GO

CREATE TYPE dbo.VariableAnalogueTypeSVHS   AS TABLE  
(  
	[VariableID] [int] NOT NULL,
	[SampleDateTimeUTC] [datetime2](7) NOT NULL,
	[PreviousSampleDateTimeUTC] [datetime2](7) NULL,
	[Value] [float] NOT NULL,
	[DeltaValue] [float] NULL,
	[StatusGood] [bit] NOT NULL,
	[StatusCode] [int] NOT NULL
	INDEX IX_VS HASH ([SampleDateTimeUTC], [VariableID])
      WITH ( BUCKET_COUNT = 1000 )  
)  
WITH  
    (MEMORY_OPTIMIZED = ON);  

GO




