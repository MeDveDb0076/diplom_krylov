# Система управления задачами «Менеджер задач»
## для ООО «Военторг-Тверь»

## 📁 Структура проекта

```
TaskManager/
├── Controllers/          # MVC контроллеры
│   ├── TaskController.cs       # CRUD задачи + комментарии
│   ├── ProjectController.cs    # CRUD проекты
│   └── CommentController.cs    # Управление комментариями
├── Models/               # Модели данных
│   └── Entities.cs             # Role, Status, User, Project, TaskItem, Comment, AuditLog
├── Views/                # Razor представления
│   ├── Task/
│   │   ├── Index.cshtml        # Список задач (пагинация, фильтры)
│   │   └── Details.cshtml      # Детали задачи + комментарии
│   ├── Project/
│   ├── Comment/
│   ├── Account/
│   └── Shared/
├── Services/             # Бизнес-логика
│   ├── UserService.cs          # Аутентификация, пользователи
│   ├── TaskService.cs          # Задачи, статусы, аудит
│   ├── ProjectService.cs       # Проекты
│   └── CommentService.cs       # Комментарии
├── Data/                 # Доступ к данным (ADO.NET)
│   └── DbConnectionFactory.cs  # Singleton подключение к БД
├── Database/             # SQL скрипты
│   └── CreateDatabase.sql      # Создание БД, таблиц, индексов
├── Helpers/              # Вспомогательные классы
├── wwwroot/              # Статические файлы
│   ├── css/
│   ├── js/
│   └── images/
├── Logs/                 # Файлы логов Serilog
├── Program.cs            # Точка входа, DI, настройки
├── appsettings.json      # Конфигурация (connection string, Serilog)
└── TaskManager.csproj    # Проект .NET 8
```

## 🔹 Технологический стек

| Компонент | Технология |
|-----------|------------|
| Backend | C#, ASP.NET Core 8, MVC |
| Data Access | ADO.NET (без EF Core) |
| СУБД | Microsoft SQL Server |
| Аутентификация | Cookie-based, BCrypt |
| Логирование | Serilog (Console + File) |
| Frontend | HTML5, CSS3 (Bootstrap 5), Vanilla JS |

## 🔹 Роли и права доступа

| Роль | Доступ |
|------|--------|
| **Администратор** | Полный доступ: управление пользователями, ролями, справочниками, модерация |
| **Менеджер** | Создание проектов/задач, назначение исполнителей, контроль сроков, изменение статусов |
| **Разработчик** | Просмотр назначенных задач, обновление статуса, добавление комментариев |
| **Клиент** | Просмотр хода проекта (только чтение) |

## 🔹 Быстрый старт

### 1. Создание базы данных

```sql
-- Выполнить скрипт в SQL Server Management Studio
sqlcmd -S localhost -i Database/CreateDatabase.sql
```

### 2. Настройка подключения

В `appsettings.json` изменить connection string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=TaskManager;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

### 3. Запуск приложения

```bash
dotnet restore
dotnet run
```

### 4. Вход в систему

- **Email:** admin@saltpepper.ru
- **Пароль:** Admin123!

## 🔹 Основные возможности

### Задачи
- ✅ CRUD операции с задачами
- ✅ Пагинация (20/50/100 записей)
- ✅ Фильтрация по проекту, статусу, исполнителю
- ✅ Валидация переходов статусов
- ✅ Мягкое удаление (IsDeleted)
- ✅ Аудит изменений

### Статусы (строгий порядок переходов)
```
«К выполнению» → «В работе» → «На проверке» → «Завершено»
```

### Приоритеты
1. Низкий
2. Средний
3. Высокий
4. Критичный

## 🔹 API Endpoints

| Метод | URL | Описание |
|-------|-----|----------|
| GET | `/Task/Index` | Список задач с пагинацией |
| GET | `/Task/Details/{id}` | Детали задачи + комментарии |
| POST | `/Task/Create` | Создание задачи |
| POST | `/Task/UpdateStatus/{id}` | Обновление статуса |
| POST | `/Task/AddComment/{id}` | Добавить комментарий |
| GET | `/Task/GetByProject/{projectId}` | AJAX: задачи проекта |
| POST | `/Task/AssignUser` | Назначить исполнителя |

## 🔹 Безопасность

- ✅ Параметризованные SQL-запросы (защита от SQL-инъекций)
- ✅ Хеширование паролей BCrypt
- ✅ Cookie аутентификация с HttpOnly
- ✅ [Authorize] атрибуты на контроллерах
- ✅ Проверка ролей в каждом методе
- ✅ CSRF защита ([ValidateAntiForgeryToken])

## 🔹 Логирование

Логи сохраняются в `/Logs/log-YYYYMMDD.txt` с ротацией по дням.

Формат записи:
```
2024-01-15 10:30:45.123 +00:00 [INF] Задача 42 создана пользователем 5
```

## 🔹 Индексы БД

```sql
IX_Задача_Статус_Дата    -- Оптимизация фильтрации по статусу и дате
IX_Задача_Проект         -- Ускорение JOIN с проектами
IX_Задача_Исполнитель    -- Поиск задач по исполнителю
IX_Комментарий_Задача    -- Получение комментариев задачи
```

---
**Версия:** 1.0  
**Дата:** 2024  
**Команда:** СОЛТ ЭНД ПЕППЕР
