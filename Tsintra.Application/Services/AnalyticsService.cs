using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tsintra.Domain.Interfaces;
using Tsintra.Domain.Models;
using Tsintra.Domain.Models.Analytics;

namespace Tsintra.Application.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IProductRepository _productRepository;
        private readonly IPipelineRepository _pipelineRepository;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(
            IOrderRepository orderRepository,
            ICustomerRepository customerRepository,
            IProductRepository productRepository,
            IPipelineRepository pipelineRepository,
            ILogger<AnalyticsService> logger)
        {
            _orderRepository = orderRepository;
            _customerRepository = customerRepository;
            _productRepository = productRepository;
            _pipelineRepository = pipelineRepository;
            _logger = logger;
        }

        #region Sales Analytics

        public async Task<SalesOverview> GetSalesOverviewAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow.AddMonths(-1);
                endDate ??= DateTime.UtcNow;

                var orders = await _orderRepository.GetOrdersByDateRangeAsync(startDate.Value, endDate.Value);
                var deals = await _pipelineRepository.GetDealsByDateRangeAsync(startDate.Value, endDate.Value);

                var totalRevenue = orders.Sum(o => o.TotalAmount);
                var totalDeals = deals.Count();
                var completedDeals = deals.Count(d => d.Status == DealStatus.Won);
                var avgOrderValue = orders.Any() ? totalRevenue / orders.Count() : 0;
                var avgTimeToClose = deals.Where(d => d.ClosedAt.HasValue)
                    .Select(d => (d.ClosedAt.Value - d.CreatedAt).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average();
                var conversionRate = totalDeals > 0 ? (decimal)completedDeals / totalDeals * 100 : 0;

                // Найпопулярніші продукти
                var products = await _productRepository.GetAllAsync();
                var topProducts = orders
                    .SelectMany(o => o.Items)
                    .GroupBy(i => Guid.Parse(i.ProductId))
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        QuantitySold = g.Sum(i => i.Quantity),
                        Revenue = g.Sum(i => i.UnitPrice * i.Quantity)
                    })
                    .OrderByDescending(p => p.Revenue)
                    .Take(5)
                    .Select(p => new TopSellingProduct
                    {
                        ProductId = p.ProductId,
                        ProductName = products.FirstOrDefault(pr => pr.Id == p.ProductId)?.Name ?? "Невідомий продукт",
                        QuantitySold = p.QuantitySold,
                        Revenue = p.Revenue,
                        GrowthRate = 0 // Розрахунок GrowthRate вимагає даних за попередній період
                    })
                    .ToList();

                // Дохід за каналами продажів
                var revenueBySalesChannel = orders
                    .GroupBy(o => o.Source ?? "Невідомий канал")
                    .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount));

                return new SalesOverview
                {
                    TotalRevenue = totalRevenue,
                    TotalDeals = totalDeals,
                    CompletedDeals = completedDeals,
                    AverageOrderValue = avgOrderValue,
                    AverageTimeToClose = (decimal)avgTimeToClose,
                    ConversionRate = conversionRate,
                    TopProducts = topProducts,
                    RevenueBySalesChannel = revenueBySalesChannel
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання огляду продажів");
                throw;
            }
        }

        public async Task<SalesTrends> GetSalesTrendsAsync(DateTime startDate, DateTime endDate, TrendPeriod period)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var orders = await _orderRepository.GetOrdersByDateRangeAsync(startDate, endDate);
                
                var trends = new SalesTrends
                {
                    Period = period,
                    StartDate = startDate,
                    EndDate = endDate,
                    RevenueByPeriod = new List<TrendPoint>(),
                    OrdersByPeriod = new List<TrendPoint>(),
                    AverageOrderValueByPeriod = new List<TrendPoint>()
                };

                // У реальній реалізації тут буде логіка групування за періодами
                // та розрахунок трендів

                return trends;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання трендів продажів");
                throw;
            }
        }

        public async Task<SalesForecast> GetSalesForecastAsync(int months)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою прогнозування
                var forecast = new SalesForecast
                {
                    RevenueForecast = new List<ForecastPoint>(),
                    OrdersForecast = new List<ForecastPoint>(),
                    PredictedGrowthRate = 5, // Тестове значення
                    Confidence = 80 // Тестове значення
                };

                // У реальній реалізації тут буде логіка прогнозування 
                // на основі історичних даних

                return forecast;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання прогнозу продажів");
                throw;
            }
        }

        public async Task<PipelineConversion> GetPipelineConversionAsync(Guid? pipelineId = null)
        {
            try
            {
                Pipeline pipeline = null;
                IEnumerable<Pipeline> pipelines;
                
                if (pipelineId.HasValue)
                {
                    pipeline = await _pipelineRepository.GetPipelineByIdAsync(pipelineId.Value);
                    pipelines = new List<Pipeline> { pipeline };
                }
                else
                {
                    pipelines = await _pipelineRepository.GetAllPipelinesAsync();
                    pipeline = pipelines.FirstOrDefault(p => p.IsActive);
                }

                if (pipeline == null)
                {
                    return new PipelineConversion
                    {
                        PipelineId = null,
                        PipelineName = "Немає активних воронок",
                        StageConversions = new List<StageConversion>(),
                        OverallConversionRate = 0,
                        AverageTimeToConvert = 0
                    };
                }

                // В реальній імплементації тут буде повна логіка розрахунку конверсії
                var conversion = new PipelineConversion
                {
                    PipelineId = pipeline.Id,
                    PipelineName = pipeline.Name,
                    StageConversions = new List<StageConversion>(),
                    OverallConversionRate = 25, // Тестове значення
                    AverageTimeToConvert = 14 // Тестове значення
                };

                return conversion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання конверсії воронки продажів");
                throw;
            }
        }

        #endregion

        #region Customer Analytics

        public async Task<CustomerSegmentation> GetCustomerSegmentationAsync()
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var customers = await _customerRepository.GetAllAsync();
                var orders = await _orderRepository.GetAllAsync();

                var segmentation = new CustomerSegmentation
                {
                    TotalCustomers = customers.Count(),
                    Segments = new List<SegmentInfo>(),
                    SegmentTrends = new List<CustomerSegmentTrend>()
                };

                // У реальній реалізації тут буде логіка сегментації клієнтів
                // на основі їх поведінки, історії покупок, тощо

                return segmentation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання сегментації клієнтів");
                throw;
            }
        }

        public async Task<CustomerLifetimeValue> GetCustomerLifetimeValueAsync(Guid? customerId = null)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var ltv = new CustomerLifetimeValue
                {
                    CustomerId = customerId,
                    CustomerName = customerId.HasValue ? 
                        (await _customerRepository.GetByIdAsync(customerId.Value))?.Name ?? "Невідомий клієнт" :
                        "Всі клієнти",
                    AverageLTV = 1000, // Тестове значення
                    AveragePurchaseValue = 200, // Тестове значення
                    AveragePurchaseFrequency = 5, // Тестове значення
                    AverageCustomerLifespan = 12, // Тестове значення
                    ProfitMargin = 30, // Тестове значення
                    SegmentLTV = new List<SegmentLTV>()
                };

                // У реальній реалізації тут буде логіка розрахунку LTV
                // на основі історичних даних

                return ltv;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання життєвої цінності клієнта");
                throw;
            }
        }

        public async Task<CustomerRetention> GetCustomerRetentionAsync()
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var retention = new CustomerRetention
                {
                    OverallRetentionRate = 70, // Тестове значення
                    ChurnRate = 30, // Тестове значення
                    RetentionRates = new List<RetentionByPeriod>(),
                    RetentionBySegments = new List<RetentionBySegment>()
                };

                // У реальній реалізації тут буде логіка розрахунку утримання клієнтів
                // на основі історичних даних

                return retention;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання утримання клієнтів");
                throw;
            }
        }

        public async Task<CustomerAcquisition> GetCustomerAcquisitionAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var customers = await _customerRepository.GetAllAsync();
                var newCustomers = customers.Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate).ToList();

                var acquisition = new CustomerAcquisition
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalNewCustomers = newCustomers.Count,
                    AcquisitionCost = 5000, // Тестове значення
                    CostPerAcquisition = newCustomers.Count > 0 ? 5000 / newCustomers.Count : 0,
                    AcquisitionByPeriods = new List<AcquisitionByPeriod>(),
                    AcquisitionByChannels = new List<AcquisitionByChannel>()
                };

                // У реальній реалізації тут буде логіка розрахунку залучення клієнтів
                // за періодами та каналами

                return acquisition;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання даних про залучення клієнтів");
                throw;
            }
        }

        #endregion

        #region Team Performance

        public async Task<TeamPerformance> GetTeamPerformanceAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var deals = await _pipelineRepository.GetDealsByDateRangeAsync(startDate, endDate);
                
                var performance = new TeamPerformance
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalTeamMembers = 5, // Тестове значення
                    TotalRevenue = deals.Sum(d => d.Value),
                    AverageRevenuePerEmployee = deals.Sum(d => d.Value) / 5,
                    TopPerformers = new List<UserSummary>(),
                    KPIs = new Dictionary<string, decimal>()
                };

                // У реальній реалізації тут буде логіка розрахунку продуктивності команди
                // на основі фактичних даних

                return performance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання показників ефективності команди");
                throw;
            }
        }

        public async Task<UserPerformance> GetUserPerformanceAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var deals = await _pipelineRepository.GetDealsByUserIdAsync(userId);
                deals = deals.Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate).ToList();
                
                var performance = new UserPerformance
                {
                    UserId = userId,
                    UserName = "Користувач", // В реальності тут буде ім'я користувача
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalDeals = deals.Count(),
                    DealsWon = deals.Count(d => d.Status == DealStatus.Won),
                    DealsLost = deals.Count(d => d.Status == DealStatus.Lost),
                    WinRate = deals.Any() ? (decimal)deals.Count(d => d.Status == DealStatus.Won) / deals.Count() * 100 : 0,
                    TotalRevenue = deals.Where(d => d.Status == DealStatus.Won).Sum(d => d.Value),
                    AverageTimeToClose = 14, // Тестове значення
                    RecentDeals = new List<DealSummary>(),
                    PerformanceTrend = new List<TrendPoint>()
                };

                // У реальній реалізації тут буде логіка розрахунку продуктивності користувача
                // на основі фактичних даних

                return performance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання показників ефективності користувача");
                throw;
            }
        }

        public async Task<List<TeamMetric>> GetTeamMetricsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow.AddMonths(-1);
                endDate ??= DateTime.UtcNow;

                // Базова імплементація - буде розширена з повною логікою
                var metrics = new List<TeamMetric>
                {
                    new TeamMetric { 
                        MetricName = "Конверсія воронки продажів", 
                        Description = "Відсоток угод, що закінчилися успішно", 
                        Value = 25, 
                        Unit = "%", 
                        ChangeFromPrevious = 5, 
                        IsPositiveTrend = true 
                    },
                    new TeamMetric { 
                        MetricName = "Середній час закриття угоди", 
                        Description = "Середній час від створення до закриття угоди", 
                        Value = 14, 
                        Unit = "дні", 
                        ChangeFromPrevious = -2, 
                        IsPositiveTrend = true 
                    },
                    new TeamMetric { 
                        MetricName = "Середній чек", 
                        Description = "Середня сума угоди", 
                        Value = 5000, 
                        Unit = "грн", 
                        ChangeFromPrevious = 10, 
                        IsPositiveTrend = true 
                    }
                };

                // У реальній реалізації тут буде логіка розрахунку різних метрик команди
                // на основі фактичних даних

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання метрик команди");
                throw;
            }
        }

        #endregion

        #region Financial Analytics

        public async Task<FinancialOverview> GetFinancialOverviewAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var orders = await _orderRepository.GetOrdersByDateRangeAsync(startDate, endDate);
                
                var overview = new FinancialOverview
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalRevenue = orders.Sum(o => o.TotalAmount),
                    TotalCost = orders.Sum(o => o.TotalAmount) * 0.6m, // Тестове значення
                    GrossProfit = orders.Sum(o => o.TotalAmount) * 0.4m,
                    GrossProfitMargin = 40, // Тестове значення
                    OperatingExpenses = orders.Sum(o => o.TotalAmount) * 0.2m, // Тестове значення
                    NetProfit = orders.Sum(o => o.TotalAmount) * 0.2m,
                    NetProfitMargin = 20, // Тестове значення
                    RevenueTrend = new List<TrendPoint>(),
                    ProfitTrend = new List<TrendPoint>()
                };

                // У реальній реалізації тут буде логіка розрахунку фінансових показників
                // та тренди за періодами

                return overview;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання фінансового огляду");
                throw;
            }
        }

        public async Task<ProductFinancials> GetProductFinancialsAsync(DateTime startDate, DateTime endDate, int topCount = 10)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var orders = await _orderRepository.GetOrdersByDateRangeAsync(startDate, endDate);
                var products = await _productRepository.GetAllAsync();
                
                var financials = new ProductFinancials
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TopProducts = new List<ProductFinancialInfo>(),
                    WorstProducts = new List<ProductFinancialInfo>(),
                    ByCategory = new List<ProductCategoryFinancials>()
                };

                // У реальній реалізації тут буде логіка розрахунку фінансових показників
                // для продуктів на основі фактичних даних

                return financials;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання фінансових показників по продуктах");
                throw;
            }
        }

        public async Task<ProfitLossStatement> GetProfitLossStatementAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var orders = await _orderRepository.GetOrdersByDateRangeAsync(startDate, endDate);
                var totalRevenue = orders.Sum(o => o.TotalAmount);
                
                var statement = new ProfitLossStatement
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Revenue = totalRevenue,
                    CostOfGoodsSold = totalRevenue * 0.6m, // Тестове значення
                    GrossProfit = totalRevenue * 0.4m,
                    OperatingExpenses = new Dictionary<string, decimal>
                    {
                        { "Зарплата", totalRevenue * 0.15m },
                        { "Оренда", totalRevenue * 0.05m },
                        { "Маркетинг", totalRevenue * 0.1m },
                        { "Інше", totalRevenue * 0.05m }
                    },
                    TotalOperatingExpenses = totalRevenue * 0.35m,
                    OperatingIncome = totalRevenue * 0.05m,
                    OtherIncome = 0,
                    OtherExpenses = 0,
                    NetIncome = totalRevenue * 0.05m,
                    MonthlyProfitLoss = new List<TrendPoint>()
                };

                // У реальній реалізації тут буде логіка розрахунку звіту про прибутки та збитки
                // на основі фактичних даних

                return statement;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання звіту про прибутки та збитки");
                throw;
            }
        }

        #endregion

        #region Dashboard and Reports

        public async Task<DashboardData> GetDashboardDataAsync()
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var startDate = DateTime.UtcNow.AddMonths(-1);
                var endDate = DateTime.UtcNow;
                var orders = await _orderRepository.GetOrdersByDateRangeAsync(startDate, endDate);
                var deals = await _pipelineRepository.GetDealsByDateRangeAsync(startDate, endDate);
                
                var dashboardData = new DashboardData
                {
                    TotalRevenueMTD = orders.Sum(o => o.TotalAmount),
                    RevenueChangePercentage = 10, // Тестове значення
                    NewDealsCount = deals.Count(d => d.Status == DealStatus.Open),
                    DealsInProgressCount = 15, // Тестове значення
                    DealsWonCount = deals.Count(d => d.Status == DealStatus.Won),
                    ConversionRate = 25, // Тестове значення
                    TopProducts = new List<TopSellingProduct>(),
                    TopPerformers = new List<UserSummary>(),
                    RecentTrend = new List<TrendPoint>(),
                    RecentDeals = new List<DealSummary>()
                };

                // У реальній реалізації тут буде логіка наповнення дашборду
                // на основі фактичних даних

                return dashboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання даних для дашборда");
                throw;
            }
        }

        public async Task<KeyPerformanceIndicators> GetKeyPerformanceIndicatorsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var kpi = new KeyPerformanceIndicators
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    KPIs = new List<KPI>
                    {
                        new KPI
                        {
                            Name = "Дохід",
                            Category = "Фінанси",
                            Description = "Загальний дохід за період",
                            Value = 100000,
                            TargetValue = 120000,
                            Unit = "грн",
                            ChangePercentage = 15,
                            IsPositiveTrend = true
                        },
                        new KPI
                        {
                            Name = "Кількість нових клієнтів",
                            Category = "Клієнти",
                            Description = "Кількість нових клієнтів за період",
                            Value = 50,
                            TargetValue = 60,
                            Unit = "клієнти",
                            ChangePercentage = 20,
                            IsPositiveTrend = true
                        },
                        new KPI
                        {
                            Name = "Конверсія воронки продажів",
                            Category = "Продажі",
                            Description = "Відсоток угод, що закінчилися успішно",
                            Value = 25,
                            TargetValue = 30,
                            Unit = "%",
                            ChangePercentage = 5,
                            IsPositiveTrend = true
                        }
                    }
                };

                // У реальній реалізації тут буде логіка розрахунку KPI
                // на основі фактичних даних

                return kpi;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання ключових показників ефективності");
                throw;
            }
        }

        public async Task<List<ReportDefinition>> GetAvailableReportsAsync()
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var reports = new List<ReportDefinition>
                {
                    new ReportDefinition
                    {
                        Id = "sales-report",
                        Name = "Звіт з продажів",
                        Description = "Детальний звіт з продажів за вказаний період",
                        Category = "Продажі",
                        Parameters = new List<ReportParameter>
                        {
                            new ReportParameter
                            {
                                Name = "startDate",
                                DisplayName = "Початкова дата",
                                Type = "date",
                                IsRequired = true,
                                DefaultValue = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd")
                            },
                            new ReportParameter
                            {
                                Name = "endDate",
                                DisplayName = "Кінцева дата",
                                Type = "date",
                                IsRequired = true,
                                DefaultValue = DateTime.UtcNow.ToString("yyyy-MM-dd")
                            }
                        }
                    },
                    new ReportDefinition
                    {
                        Id = "customer-report",
                        Name = "Звіт по клієнтах",
                        Description = "Аналіз клієнтської бази",
                        Category = "Клієнти",
                        Parameters = new List<ReportParameter>
                        {
                            new ReportParameter
                            {
                                Name = "segment",
                                DisplayName = "Сегмент клієнтів",
                                Type = "select",
                                IsRequired = false,
                                DefaultValue = ""
                            }
                        }
                    }
                };

                return reports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка отримання списку доступних звітів");
                throw;
            }
        }

        public async Task<ReportData> GenerateReportAsync(string reportId, DateTime startDate, DateTime endDate, Dictionary<string, string> parameters)
        {
            try
            {
                // Базова імплементація - буде розширена з повною логікою
                var reportData = new ReportData
                {
                    ReportId = reportId,
                    ReportName = reportId == "sales-report" ? "Звіт з продажів" : "Звіт по клієнтах",
                    GeneratedAt = DateTime.UtcNow,
                    Parameters = parameters,
                    Sections = new List<ReportSection>
                    {
                        new ReportSection
                        {
                            Title = "Основні показники",
                            Description = "Ключові показники за вказаний період",
                            Data = new List<Dictionary<string, object>>(),
                            Charts = new List<ChartData>()
                        }
                    }
                };

                // У реальній реалізації тут буде логіка генерації звіту
                // на основі вказаного ID та параметрів

                return reportData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка генерації звіту: {ReportId}", reportId);
                throw;
            }
        }

        #endregion
    }
} 