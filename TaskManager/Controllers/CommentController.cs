using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [Authorize]
    public class CommentController : Controller
    {
        private readonly ICommentService _commentService;
        private readonly ILogger<CommentController> _logger;

        public CommentController(
            ICommentService commentService,
            ILogger<CommentController> logger)
        {
            _commentService = commentService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }

        private bool IsAdmin()
        {
            return User.IsInRole("Администратор");
        }

        /// <summary>
        /// Удаление комментария
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int taskId)
        {
            var currentUserId = GetCurrentUserId();
            var isAdmin = IsAdmin();

            try
            {
                var result = await _commentService.DeleteAsync(id, currentUserId, isAdmin);

                if (result)
                {
                    _logger.LogInformation("Комментарий {CommentId} удалён пользователем {UserId}", id, currentUserId);
                }
                else
                {
                    _logger.LogWarning("Попытка удаления комментария {CommentId} без прав пользователем {UserId}", id, currentUserId);
                    TempData["Error"] = "У вас нет прав для удаления этого комментария";
                }

                return RedirectToAction(nameof(TaskController.Details), "Task", new { id = taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении комментария {CommentId}", id);
                TempData["Error"] = "Произошла ошибка при удалении комментария";
                return RedirectToAction(nameof(TaskController.Details), "Task", new { id = taskId });
            }
        }
    }
}
