using RestaurantManagement.Business.Options;
using RestaurantManagement.Business.Policies;
using RestaurantManagement.Common.Enums;
using Xunit;

namespace RestaurantManagement.Business.Tests;

public class PolicyTests
{
    [Theory]
    [InlineData(1, 200000)]
    [InlineData(4, 200000)]
    [InlineData(5, 200000)]
    [InlineData(6, 500000)]
    [InlineData(10, 500000)]
    public void DepositPolicy_ShouldCalculateCorrectDeposit_BasedOnGuestCount(int guests, decimal expectedDeposit)
    {
        // Arrange
        var rules = new DepositRules { Small = 200000, Large = 500000, LargeGroupFrom = 6 };
        var policy = new DepositPolicy(rules);

        // Act
        var deposit = policy.CalculateDeposit(guests);

        // Assert
        Assert.Equal(expectedDeposit, deposit);
    }

    [Fact]
    public void PeakHourPolicy_ShouldDetectSaturdayPeakHours()
    {
        // Arrange: Thứ 7 ngày 17/10/2026 lúc 18:30 (Giờ VN = UTC+7 => 11:30 UTC)
        var rules = new BillingRules
        {
            PeakSurcharge = 50000,
            PeakDays = ["Saturday", "Sunday"],
            PeakFrom = "18:00",
            PeakTo = "21:00"
        };
        var policy = new PeakHourPolicy(rules);
        var saturdayUtc = new DateTime(2026, 10, 17, 11, 30, 0, DateTimeKind.Utc); // 18:30 VN

        // Act
        var isPeak = policy.IsPeakHour(saturdayUtc);

        // Assert
        Assert.True(isPeak);
    }

    [Fact]
    public void PeakHourPolicy_ShouldReturnFalse_OnWeekday()
    {
        // Arrange: Thứ 4 ngày 14/10/2026 lúc 19:00 VN
        var rules = new BillingRules
        {
            PeakSurcharge = 50000,
            PeakDays = ["Saturday", "Sunday"],
            PeakFrom = "18:00",
            PeakTo = "21:00"
        };
        var policy = new PeakHourPolicy(rules);
        var wednesdayUtc = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc); // 19:00 VN

        // Act
        var isPeak = policy.IsPeakHour(wednesdayUtc);

        // Assert
        Assert.False(isPeak);
    }

    [Fact]
    public void RefundPolicy_ShouldReturnFullRefund_WhenCancelledMoreThan2HoursBefore()
    {
        // Arrange
        var policy = new RefundPolicy();
        var bookingTime = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var cancelTime = new DateTime(2026, 10, 17, 15, 0, 0, DateTimeKind.Utc); // 3 hours before

        // Act
        var (rate, amount, _) = policy.CalculateRefund(200000, bookingTime, cancelTime);

        // Assert
        Assert.Equal(1.0m, rate);
        Assert.Equal(200000m, amount);
    }

    [Fact]
    public void RefundPolicy_ShouldReturnHalfRefund_WhenCancelledWithin2Hours()
    {
        // Arrange
        var policy = new RefundPolicy();
        var bookingTime = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var cancelTime = new DateTime(2026, 10, 17, 17, 0, 0, DateTimeKind.Utc); // 1 hour before

        // Act
        var (rate, amount, _) = policy.CalculateRefund(200000, bookingTime, cancelTime);

        // Assert
        Assert.Equal(0.5m, rate);
        Assert.Equal(100000m, amount);
    }

    [Fact]
    public void RefundPolicy_ShouldReturnZeroRefund_WhenCancelledAfterBookingTime()
    {
        // Arrange
        var policy = new RefundPolicy();
        var bookingTime = new DateTime(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
        var cancelTime = new DateTime(2026, 10, 17, 18, 30, 0, DateTimeKind.Utc); // 30 mins late

        // Act
        var (rate, amount, _) = policy.CalculateRefund(200000, bookingTime, cancelTime);

        // Assert
        Assert.Equal(0.0m, rate);
        Assert.Equal(0m, amount);
    }

    [Fact]
    public void LatePolicy_ShouldDetectLateArrival_WhenOverThreshold()
    {
        // Arrange
        var rules = new ShiftRules { LateThresholdMinutes = 15 };
        var policy = new LatePolicy(rules);

        // Act
        var (isLate, lateMinutes) = policy.CheckLate(new TimeOnly(8, 0), new TimeOnly(8, 25));

        // Assert
        Assert.True(isLate);
        Assert.Equal(25, lateMinutes);
    }

    [Fact]
    public void LatePolicy_ShouldNotFlagLate_WhenWithinThreshold()
    {
        // Arrange
        var rules = new ShiftRules { LateThresholdMinutes = 15 };
        var policy = new LatePolicy(rules);

        // Act
        var (isLate, _) = policy.CheckLate(new TimeOnly(8, 0), new TimeOnly(8, 10));

        // Assert
        Assert.False(isLate);
    }

    [Fact]
    public void BillingCalculator_ShouldCalculateInvoiceWithPeakFeeAndVat()
    {
        // Arrange
        var rules = new BillingRules
        {
            VatRate = 0.08m,
            PeakSurcharge = 50000,
            PeakDays = ["Saturday", "Sunday"],
            PeakFrom = "18:00",
            PeakTo = "21:00",
            MembershipDiscount = new MembershipDiscountRules { Gold = 0.10m }
        };
        var peakPolicy = new PeakHourPolicy(rules);
        var calculator = new BillingCalculator(rules, peakPolicy);

        // Saturday 18:30 local = 11:30 UTC
        var satOrderTime = new DateTime(2026, 10, 17, 11, 30, 0, DateTimeKind.Utc);

        // Act: items = 500,000đ, deposit = 200,000đ
        var breakdown = calculator.Calculate(500000, satOrderTime, MembershipRank.Standard, 0, 200000);

        // Surcharge = 50,000
        // VAT = 8% of (500k + 50k) = 44,000
        // Subtotal before deposit = 500k + 50k + 44k = 594,000
        // Deposit deducted = 200,000
        // FinalAmount = 394,000
        Assert.Equal(500000m, breakdown.ItemsTotal);
        Assert.Equal(500000m, breakdown.ItemsTotal);
        Assert.Equal(50000m, breakdown.SurchargeAmount);
        Assert.Equal(44000m, breakdown.VatAmount);
        Assert.Equal(200000m, breakdown.DepositDeducted);
        Assert.Equal(394000m, breakdown.FinalAmount);
    }
}
