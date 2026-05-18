-- =============================================
-- Скрипт создания базы данных "Менеджер задач"
-- для digital-агентства «СОЛТ ЭНД ПЕППЕР»
-- =============================================

USE [master]
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'TaskManager')
BEGIN
    CREATE DATABASE [TaskManager]
END
GO

USE [TaskManager]
GO

-- =============================================
-- Таблица Роль (справочник)
-- =============================================
IF OBJECT_ID(N'[dbo].[Роль]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Роль] (
        [Наименование] [nvarchar](20) NOT NULL,
        CONSTRAINT [PK_Роль] PRIMARY KEY CLUSTERED ([Наименование] ASC)
    )
    
    -- Заполнение справочника ролей
    INSERT INTO [dbo].[Роль] ([Наименование]) VALUES (N'Администратор')
    INSERT INTO [dbo].[Роль] ([Наименование]) VALUES (N'Менеджер')
    INSERT INTO [dbo].[Роль] ([Наименование]) VALUES (N'Разработчик')
    INSERT INTO [dbo].[Роль] ([Наименование]) VALUES (N'Клиент')
END
GO

-- =============================================
-- Таблица Статус (справочник)
-- =============================================
IF OBJECT_ID(N'[dbo].[Статус]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Статус] (
        [Наименование] [nvarchar](20) NOT NULL,
        CONSTRAINT [PK_Статус] PRIMARY KEY CLUSTERED ([Наименование] ASC)
    )
    
    -- Заполнение справочника статусов (строгий порядок переходов)
    INSERT INTO [dbo].[Статус] ([Наименование]) VALUES (N'К выполнению')
    INSERT INTO [dbo].[Статус] ([Наименование]) VALUES (N'В работе')
    INSERT INTO [dbo].[Статус] ([Наименование]) VALUES (N'На проверке')
    INSERT INTO [dbo].[Статус] ([Наименование]) VALUES (N'Завершено')
END
GO

-- =============================================
-- Таблица Пользователь
-- =============================================
IF OBJECT_ID(N'[dbo].[Пользователь]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Пользователь] (
        [Код] [int] IDENTITY(1,1) NOT NULL,
        [РольПользователя] [nvarchar](20) NOT NULL,
        [Имя] [nvarchar](50) NOT NULL,
        [Email] [nvarchar](50) NOT NULL,
        [ПарольHash] [nvarchar](255) NOT NULL,
        [CreatedAt] [datetime] NOT NULL DEFAULT GETDATE(),
        [IsDeleted] [bit] NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Пользователь] PRIMARY KEY CLUSTERED ([Код] ASC),
        CONSTRAINT [FK_Пользователь_Роль] FOREIGN KEY([РольПользователя]) 
            REFERENCES [dbo].[Роль]([Наименование]) ON DELETE RESTRICT
    )
    
    CREATE INDEX IX_Пользователь_Email ON [dbo].[Пользователь]([Email])
    CREATE INDEX IX_Пользователь_Роль ON [dbo].[Пользователь]([РольПользователя])
END
GO

-- =============================================
-- Таблица Проект
-- =============================================
IF OBJECT_ID(N'[dbo].[Проект]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Проект] (
        [Код] [int] IDENTITY(1,1) NOT NULL,
        [Название] [nvarchar](50) NOT NULL,
        [Описание] [nvarchar](max) NULL,
        [КодМенеджера] [int] NULL,
        [ДатаНачала] [date] NULL,
        [ДатаОкончания] [date] NULL,
        [CreatedAt] [datetime] NOT NULL DEFAULT GETDATE(),
        [IsDeleted] [bit] NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Проект] PRIMARY KEY CLUSTERED ([Код] ASC),
        CONSTRAINT [FK_Проект_Менеджер] FOREIGN KEY([КодМенеджера]) 
            REFERENCES [dbo].[Пользователь]([Код]) ON DELETE SET NULL
    )
    
    CREATE INDEX IX_Проект_Менеджер ON [dbo].[Проект]([КодМенеджера])
END
GO

-- =============================================
-- Таблица Задача (центральная)
-- =============================================
IF OBJECT_ID(N'[dbo].[Задача]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Задача] (
        [Код] [int] IDENTITY(1,1) NOT NULL,
        [КодПроекта] [int] NOT NULL,
        [КодИсполнителя] [int] NULL,
        [НаименованиеСтатуса] [nvarchar](20) NOT NULL,
        [Название] [nvarchar](50) NOT NULL,
        [Описание] [nvarchar](max) NULL,
        [ДатаОкончания] [date] NULL,
        [Приоритет] [int] NOT NULL,
        [CreatedAt] [datetime] NOT NULL DEFAULT GETDATE(),
        [UpdatedAt] [datetime] NULL,
        [IsDeleted] [bit] NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Задача] PRIMARY KEY CLUSTERED ([Код] ASC),
        CONSTRAINT [FK_Задача_Проект] FOREIGN KEY([КодПроекта]) 
            REFERENCES [dbo].[Проект]([Код]) ON DELETE CASCADE,
        CONSTRAINT [FK_Задача_Исполнитель] FOREIGN KEY([КодИсполнителя]) 
            REFERENCES [dbo].[Пользователь]([Код]) ON DELETE SET NULL,
        CONSTRAINT [FK_Задача_Статус] FOREIGN KEY([НаименованиеСтатуса]) 
            REFERENCES [dbo].[Статус]([Наименование]) ON DELETE RESTRICT
    )
    
    -- Индексы для производительности
    CREATE INDEX IX_Задача_Статус_Дата ON [dbo].[Задача]([НаименованиеСтатуса], [ДатаОкончания])
    CREATE INDEX IX_Задача_Проект ON [dbo].[Задача]([КодПроекта])
    CREATE INDEX IX_Задача_Исполнитель ON [dbo].[Задача]([КодИсполнителя])
    CREATE INDEX IX_Задача_IsDeleted ON [dbo].[Задача]([IsDeleted])
END
GO

-- =============================================
-- Таблица Комментарий
-- =============================================
IF OBJECT_ID(N'[dbo].[Комментарий]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Комментарий] (
        [Код] [int] IDENTITY(1,1) NOT NULL,
        [КодЗадачи] [int] NOT NULL,
        [КодПользователя] [int] NOT NULL,
        [Текст] [nvarchar](max) NOT NULL,
        [ДатаСоздания] [datetime] NOT NULL DEFAULT GETDATE(),
        [IsDeleted] [bit] NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Комментарий] PRIMARY KEY CLUSTERED ([Код] ASC),
        CONSTRAINT [FK_Комментарий_Задача] FOREIGN KEY([КодЗадачи]) 
            REFERENCES [dbo].[Задача]([Код]) ON DELETE CASCADE,
        CONSTRAINT [FK_Комментарий_Пользователь] FOREIGN KEY([КодПользователя]) 
            REFERENCES [dbo].[Пользователь]([Код]) ON DELETE CASCADE
    )
    
    CREATE INDEX IX_Комментарий_Задача ON [dbo].[Комментарий]([КодЗадачи])
    CREATE INDEX IX_Комментарий_Пользователь ON [dbo].[Комментарий]([КодПользователя])
END
GO

-- =============================================
-- Таблица AuditLog (для аудита изменений)
-- =============================================
IF OBJECT_ID(N'[dbo].[AuditLog]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditLog] (
        [Код] [int] IDENTITY(1,1) NOT NULL,
        [UserId] [int] NOT NULL,
        [Action] [nvarchar](50) NOT NULL,
        [EntityType] [nvarchar](50) NOT NULL,
        [EntityId] [int] NOT NULL,
        [OldValue] [nvarchar](max) NULL,
        [NewValue] [nvarchar](max) NULL,
        [Timestamp] [datetime] NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([Код] ASC),
        CONSTRAINT [FK_AuditLog_User] FOREIGN KEY([UserId]) 
            REFERENCES [dbo].[Пользователь]([Код]) ON DELETE CASCADE
    )
    
    CREATE INDEX IX_AuditLog_Entity ON [dbo].[AuditLog]([EntityType], [EntityId])
    CREATE INDEX IX_AuditLog_Timestamp ON [dbo].[AuditLog]([Timestamp])
END
GO

-- =============================================
-- Создание тестового пользователя (Администратор)
-- Пароль: Admin123! (хеш BCrypt)
-- =============================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Пользователь] WHERE [Email] = N'admin@saltpepper.ru')
BEGIN
    INSERT INTO [dbo].[Пользователь] ([РольПользователя], [Имя], [Email], [ПарольHash])
    VALUES (N'Администратор', N'Администратор', N'admin@saltpepper.ru', 
            N'$2a$11$rWzX8vK9JqL5mN3pQ7sR2OxYzH4fG6tU8wV1jK0iL9cM2dE5bA7nF')
END
GO

PRINT 'База данных TaskManager успешно создана!'
