/****** Object:  StoredProcedure [dbo].[sp_InsertResultsFromCopy]    Script Date: 1/26/2021 12:53:26 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:      <Author, , Name>
-- Create Date: <Create Date, , >
-- Description: <Description, , >
-- =============================================
ALTER PROCEDURE [dbo].[sp_InsertResultsFromCopy]
(
	@SourceFile varchar(250),
    @DestinationFile varchar(250),
    @HasCompleted bit,
    @NumberOfBytes bigint,
    @OperationId varchar(250),
    @ElapsedTime int,
	@ElapsedTimeJustForWaitCompletion int
)
AS
BEGIN
    -- SET NOCOUNT ON added to prevent extra result sets from
    -- interfering with SELECT statements.
    SET NOCOUNT ON

	if (@ElapsedTime=0)
		begin
			insert into CopyResultLog
			(SourceFile,DestinationFile,HasCompleted,NumberOfBytes,OperationId,CopyElapsedTimeInMs,ElapsedTimeJustForWaitCompletion)
			values
			(@SourceFile,@DestinationFile,@HasCompleted,@NumberOfBytes,@OperationId,@ElapsedTime,0)
		end
	else
		begin
		--TODO: Check if the record exist before update. It should always exist but need to re check. 
			update CopyResultLog set CopyElapsedTimeInMs=@ElapsedTime, HasCompleted = @HasCompleted, UpdateTime = GetDate(), NumberOfBytes=@NumberOfBytes,
			ElapsedTimeJustForWaitCompletion=@ElapsedTimeJustForWaitCompletion where
			OperationId = @OperationId
		end 
END

GO

/****** Object:  Table [dbo].[CopyResultLog]    Script Date: 1/26/2021 12:53:01 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CopyResultLog](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[CreationTime] [datetime] NOT NULL,
	[UpdateTime] [datetime] NULL,
	[SourceFile] [varchar](250) NULL,
	[DestinationFile] [varchar](250) NULL,
	[HasCompleted] [bit] NULL,
	[NumberOfBytes] [bigint] NULL,
	[OperationId] [varchar](250) NULL,
	[CopyElapsedTimeInMs] [int] NULL,
	[ElapsedTimeJustForWaitCompletion] [int] NULL,
 CONSTRAINT [PK_CopyResultLog] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[CopyResultLog] ADD  CONSTRAINT [DF_CopyResultLog_CreationTime]  DEFAULT (getdate()) FOR [CreationTime]
GO


SET ANSI_PADDING ON
GO

/****** Object:  Index [IX_CopyResultLog]    Script Date: 1/26/2021 1:04:06 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_CopyResultLog] ON [dbo].[CopyResultLog]
(
	[OperationId] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO




