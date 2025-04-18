using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tsintra.Domain.Models.Analytics;

namespace Tsintra.Domain.Interfaces
{
    public interface IAnalyticsService
    {
        #region Sales Analytics

        Task<SalesOverview> GetSalesOverviewAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<SalesTrends> GetSalesTrendsAsync(DateTime startDate, DateTime endDate, TrendPeriod period);
        Task<SalesForecast> GetSalesForecastAsync(int months);
        Task<PipelineConversion> GetPipelineConversionAsync(Guid? pipelineId = null);

        #endregion

        #region Customer Analytics

        Task<CustomerSegmentation> GetCustomerSegmentationAsync();
        Task<CustomerLifetimeValue> GetCustomerLifetimeValueAsync(Guid? customerId = null);
        Task<CustomerRetention> GetCustomerRetentionAsync();
        Task<CustomerAcquisition> GetCustomerAcquisitionAsync(DateTime startDate, DateTime endDate);

        #endregion

        #region Team Performance

        Task<TeamPerformance> GetTeamPerformanceAsync(DateTime startDate, DateTime endDate);
        Task<UserPerformance> GetUserPerformanceAsync(Guid userId, DateTime startDate, DateTime endDate);
        Task<List<TeamMetric>> GetTeamMetricsAsync(DateTime? startDate = null, DateTime? endDate = null);

        #endregion

        #region Financial Analytics

        Task<FinancialOverview> GetFinancialOverviewAsync(DateTime startDate, DateTime endDate);
        Task<ProductFinancials> GetProductFinancialsAsync(DateTime startDate, DateTime endDate, int topCount = 10);
        Task<ProfitLossStatement> GetProfitLossStatementAsync(DateTime startDate, DateTime endDate);

        #endregion

        #region Dashboard and Reports

        Task<DashboardData> GetDashboardDataAsync();
        Task<KeyPerformanceIndicators> GetKeyPerformanceIndicatorsAsync(DateTime startDate, DateTime endDate);
        Task<List<ReportDefinition>> GetAvailableReportsAsync();
        Task<ReportData> GenerateReportAsync(string reportId, DateTime startDate, DateTime endDate, Dictionary<string, string> parameters);

        #endregion
    }
} 