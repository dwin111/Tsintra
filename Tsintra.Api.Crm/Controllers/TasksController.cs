using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Models;
using Tsintra.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Tsintra.Api.Crm.Services;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly ITaskRepository _taskRepository;
        private readonly ILogger<TasksController> _logger;

        public TasksController(ITaskRepository taskRepository, ILogger<TasksController> logger)
        {
            _taskRepository = taskRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CrmTask>>> GetAllTasks([FromQuery] Tsintra.Domain.Models.TaskStatus? status = null)
        {
            try
            {
                var tasks = status.HasValue 
                    ? await _taskRepository.GetTasksByStatusAsync(status.Value)
                    : await _taskRepository.GetAllAsync();
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання задач");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CrmTask>> GetTask(Guid id)
        {
            try
            {
                var task = await _taskRepository.GetByIdAsync(id);
                if (task == null)
                {
                    return NotFound();
                }
                return Ok(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання задачі з ID: {TaskId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<CrmTask>>> GetTasksByUser(Guid userId)
        {
            try
            {
                var tasks = await _taskRepository.GetTasksByUserAsync(userId);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання задач для користувача: {UserId}", userId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<IEnumerable<CrmTask>>> GetTasksByCustomer(Guid customerId)
        {
            try
            {
                var tasks = await _taskRepository.GetTasksByCustomerAsync(customerId);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання задач для клієнта: {CustomerId}", customerId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPost]
        public async Task<ActionResult<CrmTask>> CreateTask(CrmTask task)
        {
            try
            {
                var createdTask = await _taskRepository.AddAsync(task);
                return CreatedAtAction(nameof(GetTask), new { id = createdTask.Id }, createdTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка створення задачі");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(Guid id, CrmTask task)
        {
            try
            {
                if (id != task.Id)
                {
                    return BadRequest("Невідповідність ID задачі");
                }

                var success = await _taskRepository.UpdateAsync(task);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка оновлення задачі з ID: {TaskId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateTaskStatus(Guid id, [FromBody] Tsintra.Domain.Models.TaskStatus status)
        {
            try
            {
                var success = await _taskRepository.UpdateTaskStatusAsync(id, status);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка оновлення статусу задачі з ID: {TaskId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(Guid id)
        {
            try
            {
                var success = await _taskRepository.DeleteAsync(id);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка видалення задачі з ID: {TaskId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("due-today")]
        public async Task<ActionResult<IEnumerable<CrmTask>>> GetTasksDueToday()
        {
            try
            {
                var tasks = await _taskRepository.GetTasksDueTodayAsync();
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання задач на сьогодні");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("overdue")]
        public async Task<ActionResult<IEnumerable<CrmTask>>> GetOverdueTasks()
        {
            try
            {
                var tasks = await _taskRepository.GetOverdueTasksAsync();
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання прострочених задач");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }
    }
} 