using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    public interface IPipelineRepository
    {
        // Pipeline methods
        Task<IEnumerable<Pipeline>> GetAllPipelinesAsync();
        Task<Pipeline> GetPipelineByIdAsync(Guid id);
        Task<Pipeline> AddPipelineAsync(Pipeline pipeline);
        Task<bool> UpdatePipelineAsync(Pipeline pipeline);
        Task<bool> DeletePipelineAsync(Guid id);

        // Pipeline stage methods
        Task<IEnumerable<PipelineStage>> GetStagesByPipelineIdAsync(Guid pipelineId);
        Task<PipelineStage> GetStageByIdAsync(Guid id);
        Task<PipelineStage> AddStageAsync(PipelineStage stage);
        Task<bool> UpdateStageAsync(PipelineStage stage);
        Task<bool> DeleteStageAsync(Guid id);
        Task<bool> ReorderStagesAsync(Guid pipelineId, List<Guid> stageIds);

        // Deal methods
        Task<IEnumerable<Deal>> GetDealsByPipelineIdAsync(Guid pipelineId);
        Task<IEnumerable<Deal>> GetDealsByStageIdAsync(Guid stageId);
        Task<IEnumerable<Deal>> GetDealsByCustomerIdAsync(Guid customerId);
        Task<IEnumerable<Deal>> GetDealsByUserIdAsync(Guid userId);
        Task<IEnumerable<Deal>> GetDealsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<Deal> GetDealByIdAsync(Guid id);
        Task<Deal> AddDealAsync(Deal deal);
        Task<bool> UpdateDealAsync(Deal deal);
        Task<bool> DeleteDealAsync(Guid id);
        Task<bool> MoveDealToStageAsync(Guid dealId, Guid stageId);
        Task<bool> UpdateDealStatusAsync(Guid dealId, DealStatus status);

        // Deal activity methods
        Task<IEnumerable<DealActivity>> GetActivitiesByDealIdAsync(Guid dealId);
        Task<DealActivity> AddActivityAsync(DealActivity activity);
    }
} 