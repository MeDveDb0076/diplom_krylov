using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ITaskService _taskService;
        private readonly IProjectService _projectService;
        private readonly ICommentService _commentService;
        private readonly IUserService _userService;
        private readonly ILogger<TaskController> _logger;

        public TaskController(
            ITaskService taskService,
            IProjectService projectService,
            ICommentService commentService,
            IUserService userService,
            ILogger<TaskController> logger)
        {
            _taskService = taskService;
            _projectService = projectService;
            _commentService = commentService;
            _userService = userService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        private string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        private bool IsInRole(string role)
        {
            return User.IsInRole(role);
        }

        /// <summary>
        /// Список задач с пагинацией и фильтрацией
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(TaskFilterViewModel filter)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetUserRole();

            // Ограничение доступа: Клиент видит только задачи своего проекта
            if (userRole == "Клиент")
            {
                // Получаем проекты клиента (реализуется через сервис)
                // Для упрощения - показываем все задачи с фильтром
            }

            var result = await _taskService.GetPagedAsync(filter, currentUserId);
            
            // Получаем списки для фильтров
            var projects = await _projectService.GetAllAsync();
            var statuses = await _taskService.GetAllStatusesAsync();
            var users = await _userService.GetAllAsync();

            ViewBag.Projects = projects;
            ViewBag.Statuses = statuses;
            ViewBag.Users = users;
            ViewBag.CurrentUserRole = userRole;

            return View(result);
        }

        /// <summary>
        /// Детали задачи + комментарии
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var task = await _taskService.GetByIdAsync(id);
            if (task == null)
            {
                return NotFound();
            }

            var comments = await _commentService.GetByTaskIdAsync(id);
            var statuses = await _taskService.GetAllStatusesAsync();
            var developers = await _userService.GetByRoleAsync("Разработчик");
            
            var currentUserId = GetCurrentUserId();
            var userRole = GetUserRole();

            // Проверка прав доступа
            if (!HasTaskAccess(task, currentUserId, userRole))
            {
                return Forbid();
            }

            ViewBag.Comments = comments;
            ViewBag.Statuses = statuses;
            ViewBag.Developers = developers;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.CurrentUserRole = userRole;
            ViewBag.CanEdit = CanEditTask(userRole);
            ViewBag.CanChangeStatus = CanChangeStatus(userRole);

            return View(task);
        }

        /// <summary>
        /// Создание задачи (только Менеджер и Администратор)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Администратор,Менеджер")]
        public async Task<IActionResult> Create(int? projectId)
        {
            var projects = await _projectService.GetAllAsync();
            var developers = await _userService.GetByRoleAsync("Разработчик");
            var statuses = await _taskService.GetAllStatusesAsync();

            ViewBag.Projects = projects;
            ViewBag.Developers = developers;
            ViewBag.Statuses = statuses;
            ViewBag.SelectedProjectId = projectId;

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Администратор,Менеджер")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskItem task)
        {
            if (!ModelState.IsValid)
            {
                var projects = await _projectService.GetAllAsync();
                var developers = await _userService.GetByRoleAsync("Разработчик");
                var statuses = await _taskService.GetAllStatusesAsync();

                ViewBag.Projects = projects;
                ViewBag.Developers = developers;
                ViewBag.Statuses = statuses;

                return View(task);
            }

            try
            {
                // Валидация: задача не может быть сразу в статусе "В работе" без исполнителя
                if (task.StatusName == "В работе" && task.AssigneeId == null)
                {
                    ModelState.AddModelError("", "Задача должна иметь исполнителя для статуса 'В работе'");
                    return View(task);
                }

                task.CreatedAt = DateTime.UtcNow;
                var taskId = await _taskService.CreateAsync(task);

                _logger.LogInformation("Задача {TaskId} создана пользователем {UserId}", taskId, GetCurrentUserId());

                return RedirectToAction(nameof(Details), new { id = taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании задачи");
                ModelState.AddModelError("", "Произошла ошибка при создании задачи");
                
                var projects = await _projectService.GetAllAsync();
                var developers = await _userService.GetByRoleAsync("Разработчик");
                var statuses = await _taskService.GetAllStatusesAsync();

                ViewBag.Projects = projects;
                ViewBag.Developers = developers;
                ViewBag.Statuses = statuses;

                return View(task);
            }
        }

        /// <summary>
        /// Обновление статуса задачи
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetUserRole();

            if (!CanChangeStatus(userRole))
            {
                return StatusCode(403, "Недостаточно прав для изменения статуса");
            }

            try
            {
                var task = await _taskService.GetByIdAsync(id);
                if (task == null)
                {
                    return NotFound();
                }

                // Проверка доступа
                if (!HasTaskAccess(task, currentUserId, userRole))
                {
                    return Forbid();
                }

                await _taskService.UpdateStatusAsync(id, status, currentUserId);

                _logger.LogInformation("Статус задачи {TaskId} изменён с {OldStatus} на {NewStatus} пользователем {UserId}", 
                    id, task.StatusName, status, currentUserId);

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Недопустимая смена статуса задачи {TaskId}", id);
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении статуса задачи {TaskId}", id);
                TempData["Error"] = "Произошла ошибка при обновлении статуса";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Добавление комментария к задаче
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int id, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                TempData["Error"] = "Текст комментария не может быть пустым";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUserId = GetCurrentUserId();

            try
            {
                var comment = new Comment
                {
                    TaskId = id,
                    UserId = currentUserId,
                    Text = text.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                await _commentService.CreateAsync(comment);

                _logger.LogInformation("Комментарий добавлен к задаче {TaskId} пользователем {UserId}", id, currentUserId);

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении комментария к задаче {TaskId}", id);
                TempData["Error"] = "Произошла ошибка при добавлении комментария";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>
        /// Удаление комментария
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId, int taskId)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetUserRole();
            var isAdmin = IsInRole("Администратор");

            var result = await _commentService.DeleteAsync(commentId, currentUserId, isAdmin);

            if (result)
            {
                _logger.LogInformation("Комментарий {CommentId} удалён пользователем {UserId}", commentId, currentUserId);
            }
            else
            {
                _logger.LogWarning("Попытка удаления комментария {CommentId} без прав пользователем {UserId}", commentId, currentUserId);
            }

            return RedirectToAction(nameof(Details), new { id = taskId });
        }

        /// <summary>
        /// API: получение задач по проекту (для AJAX)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetByProject(int projectId)
        {
            var tasks = await _taskService.GetByProjectIdAsync(projectId);
            return Json(tasks.Select(t => new
            {
                t.Id,
                t.Title,
                t.StatusName,
                t.Priority,
                PriorityName = t.GetPriorityName(),
                DueDate = t.DueDate?.ToString("dd.MM.yyyy"),
                AssigneeName = t.Assignee?.Name ?? "Не назначен"
            }));
        }

        /// <summary>
        /// Назначение исполнителя
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Администратор,Менеджер")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignUser(int taskId, int? assigneeId)
        {
            var currentUserId = GetCurrentUserId();

            try
            {
                await _taskService.AssignUserAsync(taskId, assigneeId, currentUserId);
                _logger.LogInformation("Исполнитель задачи {TaskId} изменён на {AssigneeId} пользователем {UserId}", 
                    taskId, assigneeId, currentUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при назначении исполнителя задачи {TaskId}", taskId);
            }

            return RedirectToAction(nameof(Details), new { id = taskId });
        }

        #region Helper Methods

        private bool HasTaskAccess(TaskItem task, int currentUserId, string userRole)
        {
            // Администратор и Менеджер имеют доступ ко всем задачам
            if (userRole == "Администратор" || userRole == "Менеджер")
            {
                return true;
            }

            // Разработчик видит только назначенные ему задачи
            if (userRole == "Разработчик")
            {
                return task.AssigneeId == currentUserId;
            }

            // Клиент видит только задачи своих проектов (упрощённо - все)
            if (userRole == "Клиент")
            {
                return true; // Здесь можно добавить проверку по проекту
            }

            return false;
        }

        private bool CanEditTask(string userRole)
        {
            return userRole == "Администратор" || userRole == "Менеджер";
        }

        private bool CanChangeStatus(string userRole)
        {
            return userRole == "Администратор" || userRole == "Менеджер" || userRole == "Разработчик";
        }

        #endregion
    }
}
