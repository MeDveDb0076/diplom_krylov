using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskManager.Models
{
    /// <summary>
    /// Справочник ролей пользователей
    /// </summary>
    [Table("Роль")]
    public class Role
    {
        [Key]
        [Column("Наименование")]
        [StringLength(20)]
        public string Name { get; set; } = string.Empty;

        // Навигационное свойство
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }

    /// <summary>
    /// Справочник статусов задач
    /// </summary>
    [Table("Статус")]
    public class Status
    {
        [Key]
        [Column("Наименование")]
        [StringLength(20)]
        public string Name { get; set; } = string.Empty;

        // Навигационное свойство
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }

    /// <summary>
    /// Пользователь системы
    /// </summary>
    [Table("Пользователь")]
    public class User
    {
        [Key]
        [Column("Код")]
        public int Id { get; set; }

        [Required]
        [Column("РольПользователя")]
        [StringLength(20)]
        [ForeignKey(nameof(Role))]
        public string RoleName { get; set; } = string.Empty;

        [Required]
        [Column("Имя")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("Email")]
        [StringLength(50)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("ПарольHash")]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        // Навигационные свойства
        public virtual Role? Role { get; set; }
        public virtual ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
    }

    /// <summary>
    /// Проект
    /// </summary>
    [Table("Проект")]
    public class Project
    {
        [Key]
        [Column("Код")]
        public int Id { get; set; }

        [Required]
        [Column("Название")]
        [StringLength(50)]
        public string Title { get; set; } = string.Empty;

        [Column("Описание")]
        public string? Description { get; set; }

        [Column("КодМенеджера")]
        public int? ManagerId { get; set; }

        [Column("ДатаНачала")]
        public DateTime? StartDate { get; set; }

        [Column("ДатаОкончания")]
        public DateTime? EndDate { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        // Навигационные свойства
        [ForeignKey(nameof(ManagerId))]
        public virtual User? Manager { get; set; }
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }

    /// <summary>
    /// Задача (центральная сущность)
    /// </summary>
    [Table("Задача")]
    public class TaskItem
    {
        [Key]
        [Column("Код")]
        public int Id { get; set; }

        [Required]
        [Column("КодПроекта")]
        public int ProjectId { get; set; }

        [Column("КодИсполнителя")]
        public int? AssigneeId { get; set; }

        [Required]
        [Column("НаименованиеСтатуса")]
        [StringLength(20)]
        [ForeignKey(nameof(Status))]
        public string StatusName { get; set; } = "К выполнению";

        [Required]
        [Column("Название")]
        [StringLength(50)]
        public string Title { get; set; } = string.Empty;

        [Column("Описание")]
        public string? Description { get; set; }

        [Column("ДатаОкончания")]
        public DateTime? DueDate { get; set; }

        [Required]
        [Column("Приоритет")]
        [Range(1, 4, ErrorMessage = "Приоритет должен быть от 1 до 4")]
        public int Priority { get; set; } = 2;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        // Навигационные свойства
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        [ForeignKey(nameof(AssigneeId))]
        public virtual User? Assignee { get; set; }

        [ForeignKey(nameof(StatusName))]
        public virtual Status? Status { get; set; }

        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        // Метод для получения названия приоритета
        public string GetPriorityName() => Priority switch
        {
            1 => "Низкий",
            2 => "Средний",
            3 => "Высокий",
            4 => "Критичный",
            _ => "Неизвестно"
        };
    }

    /// <summary>
    /// Комментарий к задаче
    /// </summary>
    [Table("Комментарий")]
    public class Comment
    {
        [Key]
        [Column("Код")]
        public int Id { get; set; }

        [Required]
        [Column("КодЗадачи")]
        public int TaskId { get; set; }

        [Required]
        [Column("КодПользователя")]
        public int UserId { get; set; }

        [Required]
        [Column("Текст")]
        public string Text { get; set; } = string.Empty;

        [Column("ДатаСоздания")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        // Навигационные свойства
        [ForeignKey(nameof(TaskId))]
        public virtual TaskItem? Task { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User? Author { get; set; }
    }

    /// <summary>
    /// Журнал аудита изменений
    /// </summary>
    [Table("AuditLog")]
    public class AuditLog
    {
        [Key]
        [Column("Код")]
        public int Id { get; set; }

        [Required]
        [Column("UserId")]
        public int UserId { get; set; }

        [Required]
        [Column("Action")]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [Column("EntityType")]
        [StringLength(50)]
        public string EntityType { get; set; } = string.Empty;

        [Required]
        [Column("EntityId")]
        public int EntityId { get; set; }

        [Column("OldValue")]
        public string? OldValue { get; set; }

        [Column("NewValue")]
        public string? NewValue { get; set; }

        [Column("Timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Навигационное свойство
        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }
    }

    /// <summary>
    /// Модель для аутентификации
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Некорректный формат Email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Пароль обязателен")]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Запомнить меня")]
        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// Модель для пагинации
    /// </summary>
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }

    /// <summary>
    /// Модель фильтрации задач
    /// </summary>
    public class TaskFilterViewModel
    {
        public int? ProjectId { get; set; }
        public string? StatusName { get; set; }
        public int? AssigneeId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SearchTerm { get; set; }
    }
}
