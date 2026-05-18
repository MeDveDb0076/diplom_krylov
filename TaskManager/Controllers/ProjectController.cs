using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly IProjectService _projectService;
        private readonly IUserService _userService;
        private readonly ITaskService _taskService;
        private readonly ILogger<ProjectController> _logger;

        public ProjectController(
            IProjectService projectService,
            IUserService userService,
            ITaskService taskService,
            ILogger<ProjectController> logger)
        {
            _projectService = projectService;
            _userService = userService;
            _taskService = taskService;
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

        /// <summary>
        /// Список проектов с пагинацией
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, string? searchTerm = null)
        {
            var userRole = GetUserRole();
            var currentUserId = GetCurrentUserId();

            PagedResult<Project> result;

            // Клиент видит только свои проекты (упрощённо - все)
            if (userRole == "Клиент")
            {
                result = await _projectService.GetPagedAsync(pageNumber, pageSize, searchTerm);
            }
            else
            {
                result = await _projectService.GetPagedAsync(pageNumber, pageSize, searchTerm);
            }

            ViewBag.CurrentUserRole = userRole;
            ViewBag.CanCreate = userRole == "Администратор" || userRole == "Менеджер";

            return View(result);
        }

        /// <summary>
        /// Детали проекта
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var project = await _projectService.GetByIdAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            var tasks = await _taskService.GetByProjectIdAsync(id);
            var teamMembers = await _projectService.GetTeamMembersAsync(id);

            ViewBag.Tasks = tasks;
            ViewBag.TeamMembers = teamMembers;
            ViewBag.CurrentUserRole = GetUserRole();
            ViewBag.CanEdit = GetUserRole() == "Администратор" || GetUserRole() == "Менеджер";

            return View(project);
        }

        /// <summary>
        /// Создание проекта (только Менеджер и Администратор)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Администратор,Менеджер")]
        public async Task<IActionResult> Create()
        {
            var managers = await _userService.GetByRoleAsync("Менеджер");
            ViewBag.Managers = managers;
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Администратор,Менеджер")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            if (!ModelState.IsValid)
            {
                var managers = await _userService.GetByRoleAsync("Менеджер");
                ViewBag.Managers = managers;
                return View(project);
            }

            try
            {
                project.CreatedAt = DateTime.UtcNow;
                var projectId = await _projectService.CreateAsync(project);

                _logger.LogInformation("Проект {ProjectId} создан пользователем {UserId}", projectId, GetCurrentUserId());

                return RedirectToAction(nameof(Details), new { id = projectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании проекта");
                ModelState.AddModelError("", "Произошла ошибка при создании проекта");
                
                var managers = await _userService.GetByRoleAsync("Менеджер");
                ViewBag.Managers = managers;
                
                return View(project);
            }
        }

        /// <summary>
        /// Редактирование проекта
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Администратор,Менеджер")]
        public async Task<IActionResult> Edit(int id)
        {
            var project = await _projectService.GetByIdAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            var managers = await _userService.GetByRoleAsync("Менеджер");
            ViewBag.Managers = managers;

            return View(project);
        }

        [HttpPost]
        [Authorize(Roles = "Администратор,Менеджер")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Project project)
        {
            if (!ModelState.IsValid)
            {
                var managers = await _userService.GetByRoleAsync("Менеджер");
                ViewBag.Managers = managers;
                return View(project);
            }

            try
            {
                project.Id = id;
                var result = await _projectService.UpdateAsync(project);

                if (result)
                {
                    _logger.LogInformation("Проект {ProjectId} обновлён пользователем {UserId}", id, GetCurrentUserId());
                    return RedirectToAction(nameof(Details), new { id });
                }

                ModelState.AddModelError("", "Проект не найден или уже удалён");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении проекта {ProjectId}", id);
                ModelState.AddModelError("", "Произошла ошибка при обновлении проекта");
            }

            var managers = await _userService.GetByRoleAsync("Менеджер");
            ViewBag.Managers = managers;
            
            return View(project);
        }

        /// <summary>
        /// Удаление проекта (мягкое)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Администратор,Менеджер")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _projectService.DeleteAsync(id);

                if (result)
                {
                    _logger.LogInformation("Проект {ProjectId} удалён пользователем {UserId}", id, GetCurrentUserId());
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении проекта {ProjectId}", id);
                TempData["Error"] = "Произошла ошибка при удалении проекта";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
