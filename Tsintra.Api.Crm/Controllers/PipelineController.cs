using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Models;
using Tsintra.Domain.Interfaces;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PipelineController : ControllerBase
    {
        private readonly IPipelineRepository _pipelineRepository;
        private readonly ILogger<PipelineController> _logger;

        public PipelineController(IPipelineRepository pipelineRepository, ILogger<PipelineController> logger)
        {
            _pipelineRepository = pipelineRepository;
            _logger = logger;
        }

        #region Pipeline Endpoints

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Pipeline>>> GetAllPipelines()
        {
            try
            {
                var pipelines = await _pipelineRepository.GetAllPipelinesAsync();
                return Ok(pipelines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання воронок продажів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Pipeline>> GetPipeline(Guid id)
        {
            try
            {
                var pipeline = await _pipelineRepository.GetPipelineByIdAsync(id);
                if (pipeline == null)
                {
                    return NotFound();
                }
                return Ok(pipeline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання воронки продажів з ID: {PipelineId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Pipeline>> CreatePipeline(Pipeline pipeline)
        {
            try
            {
                pipeline.CreatedAt = DateTime.UtcNow;
                var createdPipeline = await _pipelineRepository.AddPipelineAsync(pipeline);
                return CreatedAtAction(nameof(GetPipeline), new { id = createdPipeline.Id }, createdPipeline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка створення воронки продажів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePipeline(Guid id, Pipeline pipeline)
        {
            try
            {
                if (id != pipeline.Id)
                {
                    return BadRequest("Невідповідність ID воронки продажів");
                }

                pipeline.UpdatedAt = DateTime.UtcNow;
                var success = await _pipelineRepository.UpdatePipelineAsync(pipeline);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка оновлення воронки продажів з ID: {PipelineId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePipeline(Guid id)
        {
            try
            {
                var success = await _pipelineRepository.DeletePipelineAsync(id);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка видалення воронки продажів з ID: {PipelineId}", id);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion

        #region Pipeline Stage Endpoints

        [HttpGet("{pipelineId}/stages")]
        public async Task<ActionResult<IEnumerable<PipelineStage>>> GetStagesByPipeline(Guid pipelineId)
        {
            try
            {
                var stages = await _pipelineRepository.GetStagesByPipelineIdAsync(pipelineId);
                return Ok(stages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання етапів для воронки з ID: {PipelineId}", pipelineId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("stages/{stageId}")]
        public async Task<ActionResult<PipelineStage>> GetStage(Guid stageId)
        {
            try
            {
                var stage = await _pipelineRepository.GetStageByIdAsync(stageId);
                if (stage == null)
                {
                    return NotFound();
                }
                return Ok(stage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання етапу з ID: {StageId}", stageId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPost("{pipelineId}/stages")]
        public async Task<ActionResult<PipelineStage>> CreateStage(Guid pipelineId, PipelineStage stage)
        {
            try
            {
                if (pipelineId != stage.PipelineId)
                {
                    return BadRequest("Невідповідність ID воронки продажів");
                }

                var createdStage = await _pipelineRepository.AddStageAsync(stage);
                return CreatedAtAction(nameof(GetStage), new { stageId = createdStage.Id }, createdStage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка створення етапу для воронки з ID: {PipelineId}", pipelineId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("stages/{stageId}")]
        public async Task<IActionResult> UpdateStage(Guid stageId, PipelineStage stage)
        {
            try
            {
                if (stageId != stage.Id)
                {
                    return BadRequest("Невідповідність ID етапу");
                }

                var success = await _pipelineRepository.UpdateStageAsync(stage);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка оновлення етапу з ID: {StageId}", stageId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpDelete("stages/{stageId}")]
        public async Task<IActionResult> DeleteStage(Guid stageId)
        {
            try
            {
                var success = await _pipelineRepository.DeleteStageAsync(stageId);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка видалення етапу з ID: {StageId}", stageId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPost("{pipelineId}/stages/reorder")]
        public async Task<IActionResult> ReorderStages(Guid pipelineId, [FromBody] List<Guid> stageIds)
        {
            try
            {
                var success = await _pipelineRepository.ReorderStagesAsync(pipelineId, stageIds);
                if (!success)
                {
                    return BadRequest("Помилка при зміні порядку етапів");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка зміни порядку етапів воронки з ID: {PipelineId}", pipelineId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion

        #region Deal Endpoints

        [HttpGet("deals")]
        public async Task<ActionResult<IEnumerable<Deal>>> GetDeals([FromQuery] Guid? pipelineId, [FromQuery] Guid? stageId, [FromQuery] Guid? customerId, [FromQuery] Guid? userId)
        {
            try
            {
                IEnumerable<Deal> deals;

                if (pipelineId.HasValue)
                {
                    deals = await _pipelineRepository.GetDealsByPipelineIdAsync(pipelineId.Value);
                }
                else if (stageId.HasValue)
                {
                    deals = await _pipelineRepository.GetDealsByStageIdAsync(stageId.Value);
                }
                else if (customerId.HasValue)
                {
                    deals = await _pipelineRepository.GetDealsByCustomerIdAsync(customerId.Value);
                }
                else if (userId.HasValue)
                {
                    deals = await _pipelineRepository.GetDealsByUserIdAsync(userId.Value);
                }
                else
                {
                    return BadRequest("Потрібно вказати хоча б один параметр фільтрації: pipelineId, stageId, customerId або userId");
                }

                return Ok(deals);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання угод");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("deals/{dealId}")]
        public async Task<ActionResult<Deal>> GetDeal(Guid dealId)
        {
            try
            {
                var deal = await _pipelineRepository.GetDealByIdAsync(dealId);
                if (deal == null)
                {
                    return NotFound();
                }
                return Ok(deal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання угоди з ID: {DealId}", dealId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPost("deals")]
        public async Task<ActionResult<Deal>> CreateDeal(Deal deal)
        {
            try
            {
                deal.CreatedAt = DateTime.UtcNow;
                var createdDeal = await _pipelineRepository.AddDealAsync(deal);
                return CreatedAtAction(nameof(GetDeal), new { dealId = createdDeal.Id }, createdDeal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка створення угоди");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("deals/{dealId}")]
        public async Task<IActionResult> UpdateDeal(Guid dealId, Deal deal)
        {
            try
            {
                if (dealId != deal.Id)
                {
                    return BadRequest("Невідповідність ID угоди");
                }

                var success = await _pipelineRepository.UpdateDealAsync(deal);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка оновлення угоди з ID: {DealId}", dealId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpDelete("deals/{dealId}")]
        public async Task<IActionResult> DeleteDeal(Guid dealId)
        {
            try
            {
                var success = await _pipelineRepository.DeleteDealAsync(dealId);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка видалення угоди з ID: {DealId}", dealId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("deals/{dealId}/move")]
        public async Task<IActionResult> MoveDealToStage(Guid dealId, [FromQuery] Guid stageId)
        {
            try
            {
                var success = await _pipelineRepository.MoveDealToStageAsync(dealId, stageId);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка переміщення угоди з ID: {DealId} до етапу з ID: {StageId}", dealId, stageId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPut("deals/{dealId}/status")]
        public async Task<IActionResult> UpdateDealStatus(Guid dealId, [FromBody] DealStatus status)
        {
            try
            {
                var success = await _pipelineRepository.UpdateDealStatusAsync(dealId, status);
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка оновлення статусу угоди з ID: {DealId}", dealId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion

        #region Deal Activity Endpoints

        [HttpGet("deals/{dealId}/activities")]
        public async Task<ActionResult<IEnumerable<DealActivity>>> GetDealActivities(Guid dealId)
        {
            try
            {
                var activities = await _pipelineRepository.GetActivitiesByDealIdAsync(dealId);
                return Ok(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання активностей для угоди з ID: {DealId}", dealId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpPost("deals/{dealId}/activities")]
        public async Task<ActionResult<DealActivity>> AddDealActivity(Guid dealId, DealActivity activity)
        {
            try
            {
                if (dealId != activity.DealId)
                {
                    return BadRequest("Невідповідність ID угоди");
                }

                activity.Timestamp = DateTime.UtcNow;
                var createdActivity = await _pipelineRepository.AddActivityAsync(activity);
                return Ok(createdActivity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка додавання активності для угоди з ID: {DealId}", dealId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion
    }
} 