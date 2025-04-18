using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Tsintra.Domain.Models.Analytics;

namespace Tsintra.Api.Crm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IPipelineRepository _pipelineRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly ICommunicationRepository _communicationRepository;
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            IPipelineRepository pipelineRepository,
            ICustomerRepository customerRepository,
            IOrderRepository orderRepository,
            ICommunicationRepository communicationRepository,
            IAnalyticsService analyticsService,
            ILogger<AnalyticsController> logger)
        {
            _pipelineRepository = pipelineRepository;
            _customerRepository = customerRepository;
            _orderRepository = orderRepository;
            _communicationRepository = communicationRepository;
            _analyticsService = analyticsService;
            _logger = logger;
        }

        #region Sales Analytics

        [HttpGet("sales/overview")]
        public async Task<ActionResult<SalesOverview>> GetSalesOverview([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            try
            {
                var overview = await _analyticsService.GetSalesOverviewAsync(startDate, endDate);
                return Ok(overview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання огляду продажів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("sales/trends")]
        public async Task<ActionResult<SalesTrends>> GetSalesTrends(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate, 
            [FromQuery] TrendPeriod period = TrendPeriod.Monthly)
        {
            try
            {
                var trends = await _analyticsService.GetSalesTrendsAsync(startDate, endDate, period);
                return Ok(trends);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання трендів продажів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("sales/forecast")]
        public async Task<ActionResult<SalesForecast>> GetSalesForecast([FromQuery] int months = 3)
        {
            try
            {
                var forecast = await _analyticsService.GetSalesForecastAsync(months);
                return Ok(forecast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання прогнозу продажів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("sales/conversion")]
        public async Task<ActionResult<PipelineConversion>> GetPipelineConversion([FromQuery] Guid? pipelineId = null)
        {
            try
            {
                var conversion = await _analyticsService.GetPipelineConversionAsync(pipelineId);
                return Ok(conversion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання конверсії воронки продажів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion

        #region Customer Analytics

        [HttpGet("customers/segmentation")]
        public async Task<ActionResult<CustomerSegmentation>> GetCustomerSegmentation()
        {
            try
            {
                var segmentation = await _analyticsService.GetCustomerSegmentationAsync();
                return Ok(segmentation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання сегментації клієнтів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("customers/lifetime-value")]
        public async Task<ActionResult<CustomerLifetimeValue>> GetCustomerLifetimeValue([FromQuery] Guid? customerId = null)
        {
            try
            {
                var ltv = await _analyticsService.GetCustomerLifetimeValueAsync(customerId);
                return Ok(ltv);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання життєвої цінності клієнта");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("customers/retention")]
        public async Task<ActionResult<CustomerRetention>> GetCustomerRetention()
        {
            try
            {
                var retention = await _analyticsService.GetCustomerRetentionAsync();
                return Ok(retention);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання утримання клієнтів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("customers/acquisition")]
        public async Task<ActionResult<CustomerAcquisition>> GetCustomerAcquisition(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                var acquisition = await _analyticsService.GetCustomerAcquisitionAsync(startDate, endDate);
                return Ok(acquisition);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання даних про залучення клієнтів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion

        #region Team Performance

        [HttpGet("team/performance")]
        public async Task<ActionResult<TeamPerformance>> GetTeamPerformance(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                var performance = await _analyticsService.GetTeamPerformanceAsync(startDate, endDate);
                return Ok(performance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання показників ефективності команди");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("team/user/{userId}")]
        public async Task<ActionResult<UserPerformance>> GetUserPerformance(
            Guid userId,
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                var performance = await _analyticsService.GetUserPerformanceAsync(userId, startDate, endDate);
                return Ok(performance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання показників ефективності користувача");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("team/metrics")]
        public async Task<ActionResult<List<TeamMetric>>> GetTeamMetrics([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var metrics = await _analyticsService.GetTeamMetricsAsync(startDate, endDate);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання метрик команди");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion

        #region Financial Analytics

        [HttpGet("financials/overview")]
        public async Task<ActionResult<FinancialOverview>> GetFinancialOverview(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                var overview = await _analyticsService.GetFinancialOverviewAsync(startDate, endDate);
                return Ok(overview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання фінансового огляду");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("financials/products")]
        public async Task<ActionResult<ProductFinancials>> GetProductFinancials(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate,
            [FromQuery] int topCount = 10)
        {
            try
            {
                var financials = await _analyticsService.GetProductFinancialsAsync(startDate, endDate, topCount);
                return Ok(financials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання фінансових показників по продуктах");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("financials/profit-loss")]
        public async Task<ActionResult<ProfitLossStatement>> GetProfitLossStatement(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            try
            {
                var statement = await _analyticsService.GetProfitLossStatementAsync(startDate, endDate);
                return Ok(statement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання звіту про прибутки та збитки");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion

        #region Dashboard

        [HttpGet("dashboard")]
        public async Task<ActionResult<DashboardData>> GetDashboardData()
        {
            try
            {
                var dashboardData = await _analyticsService.GetDashboardDataAsync();
                return Ok(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання даних для дашборда");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("kpi")]
        public async Task<ActionResult<KeyPerformanceIndicators>> GetKeyPerformanceIndicators(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                var kpi = await _analyticsService.GetKeyPerformanceIndicatorsAsync(startDate, endDate);
                return Ok(kpi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання ключових показників ефективності");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion

        #region Reports

        [HttpGet("reports")]
        public async Task<ActionResult<List<ReportDefinition>>> GetAvailableReports()
        {
            try
            {
                var reports = await _analyticsService.GetAvailableReportsAsync();
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання списку доступних звітів");
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        [HttpGet("reports/{reportId}")]
        public async Task<ActionResult<ReportData>> GenerateReport(
            string reportId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] Dictionary<string, string> parameters)
        {
            try
            {
                var reportData = await _analyticsService.GenerateReportAsync(reportId, startDate, endDate, parameters);
                return Ok(reportData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка генерації звіту: {ReportId}", reportId);
                return StatusCode(500, "Внутрішня помилка сервера");
            }
        }

        #endregion
    }
} 