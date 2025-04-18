using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Linq;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;

namespace Tsintra.Persistence.Repositories
{
    public class PipelineRepository : IPipelineRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<PipelineRepository> _logger;

        public PipelineRepository(IConfiguration configuration, ILogger<PipelineRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration), "Database connection string 'DefaultConnection' not found.");
            _logger = logger;
        }

        private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        private async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
        }

        private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, parameters);
        }

        private async Task<int> ExecuteAsync(string sql, object parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(sql, parameters);
        }

        #region Pipeline methods

        public async Task<IEnumerable<Pipeline>> GetAllPipelinesAsync()
        {
            try
            {
                const string sql = "SELECT * FROM Pipelines ORDER BY Name";
                return await QueryAsync<Pipeline>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all pipelines");
                throw;
            }
        }

        public async Task<Pipeline> GetPipelineByIdAsync(Guid id)
        {
            try
            {
                const string sql = "SELECT * FROM Pipelines WHERE Id = @Id";
                return await QueryFirstOrDefaultAsync<Pipeline>(sql, new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pipeline by id {Id}", id);
                throw;
            }
        }

        public async Task<Pipeline> AddPipelineAsync(Pipeline pipeline)
        {
            try
            {
                pipeline.Id = pipeline.Id == Guid.Empty ? Guid.NewGuid() : pipeline.Id;
                pipeline.CreatedAt = DateTime.UtcNow;
                pipeline.UpdatedAt = DateTime.UtcNow;

                const string sql = @"
                    INSERT INTO Pipelines (Id, Name, Description, IsActive, CreatedAt, UpdatedAt)
                    VALUES (@Id, @Name, @Description, @IsActive, @CreatedAt, @UpdatedAt)
                    RETURNING *";

                return await QueryFirstOrDefaultAsync<Pipeline>(sql, pipeline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding pipeline");
                throw;
            }
        }

        public async Task<bool> UpdatePipelineAsync(Pipeline pipeline)
        {
            try
            {
                pipeline.UpdatedAt = DateTime.UtcNow;

                const string sql = @"
                    UPDATE Pipelines
                    SET Name = @Name, Description = @Description, IsActive = @IsActive, 
                        UpdatedAt = @UpdatedAt
                    WHERE Id = @Id";

                return await ExecuteAsync(sql, pipeline) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pipeline {Id}", pipeline.Id);
                throw;
            }
        }

        public async Task<bool> DeletePipelineAsync(Guid id)
        {
            try
            {
                const string sql = "DELETE FROM Pipelines WHERE Id = @Id";
                return await ExecuteAsync(sql, new { Id = id }) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pipeline {Id}", id);
                throw;
            }
        }

        #endregion

        #region Pipeline stage methods

        public async Task<IEnumerable<PipelineStage>> GetStagesByPipelineIdAsync(Guid pipelineId)
        {
            try
            {
                const string sql = "SELECT * FROM PipelineStages WHERE PipelineId = @PipelineId ORDER BY [Order]";
                return await QueryAsync<PipelineStage>(sql, new { PipelineId = pipelineId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stages by pipeline id {PipelineId}", pipelineId);
                throw;
            }
        }

        public async Task<PipelineStage> GetStageByIdAsync(Guid id)
        {
            try
            {
                const string sql = "SELECT * FROM PipelineStages WHERE Id = @Id";
                return await QueryFirstOrDefaultAsync<PipelineStage>(sql, new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stage by id {Id}", id);
                throw;
            }
        }

        public async Task<PipelineStage> AddStageAsync(PipelineStage stage)
        {
            try
            {
                stage.Id = stage.Id == Guid.Empty ? Guid.NewGuid() : stage.Id;
                
                // Get max order for this pipeline
                const string orderSql = @"
                    SELECT COALESCE(MAX([Order]), 0) + 1 
                    FROM PipelineStages 
                    WHERE PipelineId = @PipelineId";
                
                using var connection = CreateConnection();
                stage.Order = await connection.ExecuteScalarAsync<int>(orderSql, new { PipelineId = stage.PipelineId });

                const string sql = @"
                    INSERT INTO PipelineStages (Id, PipelineId, Name, Description, [Order], Probability, IsWon, IsLost)
                    VALUES (@Id, @PipelineId, @Name, @Description, @Order, @Probability, @IsWon, @IsLost)
                    RETURNING *";

                return await QueryFirstOrDefaultAsync<PipelineStage>(sql, stage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding stage");
                throw;
            }
        }

        public async Task<bool> UpdateStageAsync(PipelineStage stage)
        {
            try
            {
                const string sql = @"
                    UPDATE PipelineStages
                    SET Name = @Name, Description = @Description, [Order] = @Order,
                        Probability = @Probability, IsWon = @IsWon, IsLost = @IsLost
                    WHERE Id = @Id";

                return await ExecuteAsync(sql, stage) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stage {Id}", stage.Id);
                throw;
            }
        }

        public async Task<bool> DeleteStageAsync(Guid id)
        {
            try
            {
                const string sql = "DELETE FROM PipelineStages WHERE Id = @Id";
                return await ExecuteAsync(sql, new { Id = id }) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting stage {Id}", id);
                throw;
            }
        }

        public async Task<bool> ReorderStagesAsync(Guid pipelineId, List<Guid> stageIds)
        {
            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                for (int i = 0; i < stageIds.Count; i++)
                {
                    const string sql = @"
                        UPDATE PipelineStages
                        SET [Order] = @Order
                        WHERE Id = @Id AND PipelineId = @PipelineId";

                    await connection.ExecuteAsync(sql, new 
                    { 
                        Order = i + 1, 
                        Id = stageIds[i], 
                        PipelineId = pipelineId 
                    }, transaction);
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error reordering stages for pipeline {PipelineId}", pipelineId);
                throw;
            }
        }

        #endregion

        #region Deal methods

        public async Task<IEnumerable<Deal>> GetDealsByPipelineIdAsync(Guid pipelineId)
        {
            try
            {
                const string sql = @"
                    SELECT d.*, ps.Name as StageName
                    FROM Deals d
                    JOIN PipelineStages ps ON d.StageId = ps.Id
                    WHERE d.PipelineId = @PipelineId
                    ORDER BY d.CreatedAt DESC";

                return await QueryAsync<Deal>(sql, new { PipelineId = pipelineId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deals by pipeline id {PipelineId}", pipelineId);
                throw;
            }
        }

        public async Task<IEnumerable<Deal>> GetDealsByStageIdAsync(Guid stageId)
        {
            try
            {
                const string sql = @"
                    SELECT * FROM Deals 
                    WHERE StageId = @StageId
                    ORDER BY CreatedAt DESC";

                return await QueryAsync<Deal>(sql, new { StageId = stageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deals by stage id {StageId}", stageId);
                throw;
            }
        }

        public async Task<IEnumerable<Deal>> GetDealsByCustomerIdAsync(Guid customerId)
        {
            try
            {
                const string sql = @"
                    SELECT d.*, ps.Name as StageName
                    FROM Deals d
                    JOIN PipelineStages ps ON d.StageId = ps.Id
                    WHERE d.CustomerId = @CustomerId
                    ORDER BY d.CreatedAt DESC";

                return await QueryAsync<Deal>(sql, new { CustomerId = customerId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deals by customer id {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<IEnumerable<Deal>> GetDealsByUserIdAsync(Guid userId)
        {
            try
            {
                const string sql = @"
                    SELECT d.*, ps.Name as StageName
                    FROM Deals d
                    JOIN PipelineStages ps ON d.StageId = ps.Id
                    WHERE d.AssignedToUserId = @UserId
                    ORDER BY d.CreatedAt DESC";

                return await QueryAsync<Deal>(sql, new { UserId = userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deals by user id {UserId}", userId);
                throw;
            }
        }

        public async Task<IEnumerable<Deal>> GetDealsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                const string sql = @"
                    SELECT d.*, ps.Name as StageName
                    FROM Deals d
                    JOIN PipelineStages ps ON d.StageId = ps.Id
                    WHERE d.CreatedAt BETWEEN @StartDate AND @EndDate
                    ORDER BY d.CreatedAt DESC";

                return await QueryAsync<Deal>(sql, new { StartDate = startDate, EndDate = endDate });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deals by date range: {StartDate} - {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<Deal> GetDealByIdAsync(Guid id)
        {
            try
            {
                const string sql = @"
                    SELECT d.*, ps.Name as StageName
                    FROM Deals d
                    JOIN PipelineStages ps ON d.StageId = ps.Id
                    WHERE d.Id = @Id";

                return await QueryFirstOrDefaultAsync<Deal>(sql, new { Id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deal by id {Id}", id);
                throw;
            }
        }

        public async Task<Deal> AddDealAsync(Deal deal)
        {
            try
            {
                deal.Id = deal.Id == Guid.Empty ? Guid.NewGuid() : deal.Id;
                deal.CreatedAt = DateTime.UtcNow;

                const string sql = @"
                    INSERT INTO Deals (Id, Name, CustomerId, PipelineId, StageId, Status, Value, 
                        ExpectedCloseDate, AssignedToUserId, Notes, CreatedAt, ClosedAt)
                    VALUES (@Id, @Name, @CustomerId, @PipelineId, @StageId, @Status, @Value, 
                        @ExpectedCloseDate, @AssignedToUserId, @Notes, @CreatedAt, @ClosedAt)
                    RETURNING *";

                var result = await QueryFirstOrDefaultAsync<Deal>(sql, deal);

                // Add initial activity
                await AddActivityAsync(new DealActivity
                {
                    DealId = result.Id,
                    Type = DealActivityType.StatusChange,
                    Title = "Deal created",
                    Description = "Deal created",
                    Timestamp = deal.CreatedAt,
                    UserId = deal.AssignedToUserId ?? Guid.Empty
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding deal");
                throw;
            }
        }

        public async Task<bool> UpdateDealAsync(Deal deal)
        {
            try
            {
                const string sql = @"
                    UPDATE Deals
                    SET Name = @Name, CustomerId = @CustomerId, PipelineId = @PipelineId, 
                        StageId = @StageId, Status = @Status, Value = @Value, 
                        ExpectedCloseDate = @ExpectedCloseDate, AssignedToUserId = @AssignedToUserId, 
                        Notes = @Notes, ClosedAt = @ClosedAt
                    WHERE Id = @Id";

                return await ExecuteAsync(sql, deal) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating deal {Id}", deal.Id);
                throw;
            }
        }

        public async Task<bool> DeleteDealAsync(Guid id)
        {
            try
            {
                const string sql = "DELETE FROM Deals WHERE Id = @Id";
                return await ExecuteAsync(sql, new { Id = id }) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting deal {Id}", id);
                throw;
            }
        }

        public async Task<bool> MoveDealToStageAsync(Guid dealId, Guid stageId)
        {
            try
            {
                const string sql = @"
                    UPDATE Deals
                    SET StageId = @StageId
                    WHERE Id = @DealId";

                var result = await ExecuteAsync(sql, new 
                { 
                    DealId = dealId, 
                    StageId = stageId
                }) > 0;

                if (result)
                {
                    // Get the stage name
                    var stage = await GetStageByIdAsync(stageId);
                    
                    // Add activity
                    await AddActivityAsync(new DealActivity
                    {
                        DealId = dealId,
                        Type = DealActivityType.StageChange,
                        Title = "Stage changed",
                        Description = $"Deal moved to stage: {stage.Name}",
                        Timestamp = DateTime.UtcNow,
                        UserId = Guid.Empty // Should be filled with actual user ID who made the change
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving deal {DealId} to stage {StageId}", dealId, stageId);
                throw;
            }
        }

        public async Task<bool> UpdateDealStatusAsync(Guid dealId, DealStatus status)
        {
            try
            {
                DateTime? closedAt = null;
                if (status == DealStatus.Won || status == DealStatus.Lost)
                    closedAt = DateTime.UtcNow;

                const string sql = @"
                    UPDATE Deals
                    SET Status = @Status, ClosedAt = @ClosedAt
                    WHERE Id = @DealId";

                var result = await ExecuteAsync(sql, new 
                { 
                    DealId = dealId, 
                    Status = status,
                    ClosedAt = closedAt
                }) > 0;

                if (result)
                {
                    // Add activity
                    await AddActivityAsync(new DealActivity
                    {
                        DealId = dealId,
                        Type = DealActivityType.StatusChange,
                        Title = "Status changed",
                        Description = $"Deal status changed to: {status}",
                        Timestamp = DateTime.UtcNow,
                        UserId = Guid.Empty // Should be filled with actual user ID who made the change
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating deal status {DealId} to {Status}", dealId, status);
                throw;
            }
        }

        #endregion

        #region Deal activity methods

        public async Task<IEnumerable<DealActivity>> GetActivitiesByDealIdAsync(Guid dealId)
        {
            try
            {
                const string sql = @"
                    SELECT * FROM DealActivities 
                    WHERE DealId = @DealId 
                    ORDER BY Timestamp DESC";

                return await QueryAsync<DealActivity>(sql, new { DealId = dealId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting activities by deal id {DealId}", dealId);
                throw;
            }
        }

        public async Task<DealActivity> AddActivityAsync(DealActivity activity)
        {
            try
            {
                activity.Id = activity.Id == Guid.Empty ? Guid.NewGuid() : activity.Id;
                if (activity.Timestamp == default)
                    activity.Timestamp = DateTime.UtcNow;

                const string sql = @"
                    INSERT INTO DealActivities (Id, DealId, UserId, Title, Description, Timestamp, Type)
                    VALUES (@Id, @DealId, @UserId, @Title, @Description, @Timestamp, @Type)
                    RETURNING *";

                return await QueryFirstOrDefaultAsync<DealActivity>(sql, activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding activity");
                throw;
            }
        }

        #endregion
    }
} 