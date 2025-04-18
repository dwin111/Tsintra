using System;
using System.Collections.Generic;

namespace Tsintra.Domain.Models.Analytics
{
    #region Enums

    public enum TrendPeriod
    {
        Daily,
        Weekly,
        Monthly,
        Quarterly,
        Yearly
    }

    public enum CustomerSegment
    {
        New,
        Regular,
        VIP,
        Inactive,
        AtRisk,
        Lost
    }

    #endregion

    #region Sales Analytics Models

    public class SalesOverview
    {
        public decimal TotalRevenue { get; set; }
        public int TotalDeals { get; set; }
        public int CompletedDeals { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal AverageTimeToClose { get; set; } // в днях
        public decimal ConversionRate { get; set; } // у відсотках
        public List<TopSellingProduct> TopProducts { get; set; } = new List<TopSellingProduct>();
        public Dictionary<string, decimal> RevenueBySalesChannel { get; set; } = new Dictionary<string, decimal>();
    }

    public class TopSellingProduct
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal GrowthRate { get; set; } // у відсотках порівняно з попереднім періодом
    }

    public class SalesTrends
    {
        public TrendPeriod Period { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<TrendPoint> RevenueByPeriod { get; set; } = new List<TrendPoint>();
        public List<TrendPoint> OrdersByPeriod { get; set; } = new List<TrendPoint>();
        public List<TrendPoint> AverageOrderValueByPeriod { get; set; } = new List<TrendPoint>();
    }

    public class TrendPoint
    {
        public string PeriodLabel { get; set; }
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
        public decimal GrowthRate { get; set; } // у відсотках порівняно з попереднім періодом
    }

    public class SalesForecast
    {
        public List<ForecastPoint> RevenueForecast { get; set; } = new List<ForecastPoint>();
        public List<ForecastPoint> OrdersForecast { get; set; } = new List<ForecastPoint>();
        public decimal PredictedGrowthRate { get; set; } // у відсотках
        public decimal Confidence { get; set; } // у відсотках
    }

    public class ForecastPoint
    {
        public string PeriodLabel { get; set; }
        public DateTime Date { get; set; }
        public decimal PredictedValue { get; set; }
        public decimal LowerBound { get; set; }
        public decimal UpperBound { get; set; }
    }

    public class PipelineConversion
    {
        public Guid? PipelineId { get; set; }
        public string PipelineName { get; set; }
        public List<StageConversion> StageConversions { get; set; } = new List<StageConversion>();
        public decimal OverallConversionRate { get; set; } // у відсотках
        public decimal AverageTimeToConvert { get; set; } // в днях
    }

    public class StageConversion
    {
        public Guid StageId { get; set; }
        public string StageName { get; set; }
        public int EnteredDeals { get; set; }
        public int ExitedDeals { get; set; }
        public decimal ConversionRate { get; set; } // у відсотках
        public decimal AverageTimeInStage { get; set; } // в днях
    }

    #endregion

    #region Customer Analytics Models

    public class CustomerSegmentation
    {
        public int TotalCustomers { get; set; }
        public List<SegmentInfo> Segments { get; set; } = new List<SegmentInfo>();
        public List<CustomerSegmentTrend> SegmentTrends { get; set; } = new List<CustomerSegmentTrend>();
    }

    public class SegmentInfo
    {
        public CustomerSegment Segment { get; set; }
        public int CustomerCount { get; set; }
        public decimal Percentage { get; set; } // у відсотках від загальної кількості
        public decimal AverageRevenue { get; set; }
        public decimal AverageOrderFrequency { get; set; } // середня кількість замовлень за період
    }

    public class CustomerSegmentTrend
    {
        public CustomerSegment Segment { get; set; }
        public List<TrendPoint> Trend { get; set; } = new List<TrendPoint>();
    }

    public class CustomerLifetimeValue
    {
        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal AverageLTV { get; set; }
        public decimal AveragePurchaseValue { get; set; }
        public decimal AveragePurchaseFrequency { get; set; }
        public decimal AverageCustomerLifespan { get; set; } // в місяцях
        public decimal ProfitMargin { get; set; } // у відсотках
        public List<SegmentLTV> SegmentLTV { get; set; } = new List<SegmentLTV>();
    }

    public class SegmentLTV
    {
        public CustomerSegment Segment { get; set; }
        public decimal AverageLTV { get; set; }
        public int CustomerCount { get; set; }
    }

    public class CustomerRetention
    {
        public decimal OverallRetentionRate { get; set; } // у відсотках
        public decimal ChurnRate { get; set; } // у відсотках
        public List<RetentionByPeriod> RetentionRates { get; set; } = new List<RetentionByPeriod>();
        public List<RetentionBySegment> RetentionBySegments { get; set; } = new List<RetentionBySegment>();
    }

    public class RetentionByPeriod
    {
        public string Period { get; set; }
        public DateTime Date { get; set; }
        public decimal RetentionRate { get; set; } // у відсотках
        public decimal ChurnRate { get; set; } // у відсотках
    }

    public class RetentionBySegment
    {
        public CustomerSegment Segment { get; set; }
        public decimal RetentionRate { get; set; } // у відсотках
        public decimal ChurnRate { get; set; } // у відсотках
    }

    public class CustomerAcquisition
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalNewCustomers { get; set; }
        public decimal AcquisitionCost { get; set; }
        public decimal CostPerAcquisition { get; set; }
        public List<AcquisitionByPeriod> AcquisitionByPeriods { get; set; } = new List<AcquisitionByPeriod>();
        public List<AcquisitionByChannel> AcquisitionByChannels { get; set; } = new List<AcquisitionByChannel>();
    }

    public class AcquisitionByPeriod
    {
        public string Period { get; set; }
        public DateTime Date { get; set; }
        public int NewCustomers { get; set; }
        public decimal AcquisitionCost { get; set; }
        public decimal CostPerAcquisition { get; set; }
    }

    public class AcquisitionByChannel
    {
        public string Channel { get; set; }
        public int NewCustomers { get; set; }
        public decimal Percentage { get; set; } // у відсотках від загальної кількості
        public decimal AcquisitionCost { get; set; }
        public decimal CostPerAcquisition { get; set; }
    }

    #endregion

    #region Team Performance Models

    public class TeamPerformance
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalTeamMembers { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRevenuePerEmployee { get; set; }
        public List<UserSummary> TopPerformers { get; set; } = new List<UserSummary>();
        public Dictionary<string, decimal> KPIs { get; set; } = new Dictionary<string, decimal>();
    }

    public class UserSummary
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public int DealsWon { get; set; }
        public decimal Revenue { get; set; }
        public int TasksCompleted { get; set; }
        public decimal AverageTimeToClose { get; set; } // в днях
        public decimal ConversionRate { get; set; } // у відсотках
    }

    public class UserPerformance
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDeals { get; set; }
        public int DealsWon { get; set; }
        public int DealsLost { get; set; }
        public decimal WinRate { get; set; } // у відсотках
        public decimal TotalRevenue { get; set; }
        public decimal AverageTimeToClose { get; set; } // в днях
        public List<DealSummary> RecentDeals { get; set; } = new List<DealSummary>();
        public List<TrendPoint> PerformanceTrend { get; set; } = new List<TrendPoint>();
    }

    public class DealSummary
    {
        public Guid DealId { get; set; }
        public string DealName { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal Value { get; set; }
        public DealStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int DaysToClose { get; set; }
    }

    public class TeamMetric
    {
        public string MetricName { get; set; }
        public string Description { get; set; }
        public decimal Value { get; set; }
        public string Unit { get; set; }
        public decimal ChangeFromPrevious { get; set; } // у відсотках
        public bool IsPositiveTrend { get; set; }
    }

    #endregion

    #region Financial Analytics Models

    public class FinancialOverview
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossProfitMargin { get; set; } // у відсотках
        public decimal OperatingExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal NetProfitMargin { get; set; } // у відсотках
        public List<TrendPoint> RevenueTrend { get; set; } = new List<TrendPoint>();
        public List<TrendPoint> ProfitTrend { get; set; } = new List<TrendPoint>();
    }

    public class ProductFinancials
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ProductFinancialInfo> TopProducts { get; set; } = new List<ProductFinancialInfo>();
        public List<ProductFinancialInfo> WorstProducts { get; set; } = new List<ProductFinancialInfo>();
        public List<ProductCategoryFinancials> ByCategory { get; set; } = new List<ProductCategoryFinancials>();
    }

    public class ProductFinancialInfo
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitMargin { get; set; } // у відсотках
        public decimal GrowthRate { get; set; } // у відсотках порівняно з попереднім періодом
    }

    public class ProductCategoryFinancials
    {
        public string Category { get; set; }
        public int ProductCount { get; set; }
        public int TotalQuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
        public decimal ProfitMargin { get; set; } // у відсотках
    }

    public class ProfitLossStatement
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Revenue { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal GrossProfit { get; set; }
        public Dictionary<string, decimal> OperatingExpenses { get; set; } = new Dictionary<string, decimal>();
        public decimal TotalOperatingExpenses { get; set; }
        public decimal OperatingIncome { get; set; }
        public decimal OtherIncome { get; set; }
        public decimal OtherExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public List<TrendPoint> MonthlyProfitLoss { get; set; } = new List<TrendPoint>();
    }

    #endregion

    #region Dashboard and Reports Models

    public class DashboardData
    {
        public decimal TotalRevenueMTD { get; set; }
        public decimal RevenueChangePercentage { get; set; }
        public int NewDealsCount { get; set; }
        public int DealsInProgressCount { get; set; }
        public int DealsWonCount { get; set; }
        public decimal ConversionRate { get; set; } // у відсотках
        public List<TopSellingProduct> TopProducts { get; set; } = new List<TopSellingProduct>();
        public List<UserSummary> TopPerformers { get; set; } = new List<UserSummary>();
        public List<TrendPoint> RecentTrend { get; set; } = new List<TrendPoint>();
        public List<DealSummary> RecentDeals { get; set; } = new List<DealSummary>();
    }

    public class KeyPerformanceIndicators
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<KPI> KPIs { get; set; } = new List<KPI>();
    }

    public class KPI
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public decimal Value { get; set; }
        public decimal TargetValue { get; set; }
        public string Unit { get; set; }
        public decimal ChangePercentage { get; set; }
        public bool IsPositiveTrend { get; set; }
    }

    public class ReportDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public List<ReportParameter> Parameters { get; set; } = new List<ReportParameter>();
    }

    public class ReportParameter
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Type { get; set; }
        public bool IsRequired { get; set; }
        public string DefaultValue { get; set; }
    }

    public class ReportData
    {
        public string ReportId { get; set; }
        public string ReportName { get; set; }
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
        public List<ReportSection> Sections { get; set; } = new List<ReportSection>();
    }

    public class ReportSection
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public List<Dictionary<string, object>> Data { get; set; } = new List<Dictionary<string, object>>();
        public List<ChartData> Charts { get; set; } = new List<ChartData>();
    }

    public class ChartData
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
        public List<ChartSeries> Series { get; set; } = new List<ChartSeries>();
    }

    public class ChartSeries
    {
        public string Name { get; set; }
        public List<decimal> Data { get; set; } = new List<decimal>();
    }

    #endregion
} 