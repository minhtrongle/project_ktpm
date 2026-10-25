using RestaurantManagement.Business.Options;
using RestaurantManagement.Common.Enums;

namespace RestaurantManagement.Business.Policies;

public class DepositPolicy
{
    private readonly DepositRules _rules;

    public DepositPolicy(DepositRules rules)
    {
        _rules = rules;
    }

    public decimal CalculateDeposit(int guestCount)
    {
        if (guestCount < 1)
            throw new ArgumentException("Số lượng khách phải lớn hơn hoặc bằng 1.", nameof(guestCount));

        return guestCount >= _rules.LargeGroupFrom ? _rules.Large : _rules.Small;
    }
}

public class PeakHourPolicy
{
    private readonly BillingRules _rules;
    private readonly TimeZoneInfo _timeZone;

    public PeakHourPolicy(BillingRules rules, string timeZoneId = "Asia/Ho_Chi_Minh")
    {
        _rules = rules;
        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            // Fallback cho Windows nếu tên IANA chưa có
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    public bool IsPeakHour(DateTime utcDateTime)
    {
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _timeZone);
        var dayOfWeek = localTime.DayOfWeek.ToString();

        if (!_rules.PeakDays.Contains(dayOfWeek, StringComparer.OrdinalIgnoreCase))
            return false;

        var timeOfDay = TimeOnly.FromTimeSpan(localTime.TimeOfDay);
        var peakFrom = TimeOnly.Parse(_rules.PeakFrom);
        var peakTo = TimeOnly.Parse(_rules.PeakTo);

        return timeOfDay >= peakFrom && timeOfDay <= peakTo;
    }
}

public record BillingBreakdown(
    decimal ItemsTotal,
    decimal SurchargeAmount,
    decimal VatAmount,
    decimal DiscountAmount,
    decimal DepositDeducted,
    decimal FinalAmount
);

public class BillingCalculator
{
    private readonly BillingRules _rules;
    private readonly PeakHourPolicy _peakHourPolicy;

    public BillingCalculator(BillingRules rules, PeakHourPolicy peakHourPolicy)
    {
        _rules = rules;
        _peakHourPolicy = peakHourPolicy;
    }

    public BillingBreakdown Calculate(
        decimal itemsTotal,
        DateTime orderTimeUtc,
        MembershipRank rank = MembershipRank.Standard,
        decimal manualDiscountPercent = 0,
        decimal depositAmount = 0)
    {
        // 1. Phụ thu cao điểm cuối tuần
        var surcharge = _peakHourPolicy.IsPeakHour(orderTimeUtc) ? _rules.PeakSurcharge : 0m;

        // 2. Chiết khấu thành viên trên tiền món
        var memberDiscountRate = rank switch
        {
            MembershipRank.Silver => _rules.MembershipDiscount.Silver,
            MembershipRank.Gold => _rules.MembershipDiscount.Gold,
            MembershipRank.Diamond => _rules.MembershipDiscount.Diamond,
            _ => 0m
        };

        var totalDiscountRate = memberDiscountRate + manualDiscountPercent;
        var discountAmount = Round(itemsTotal * totalDiscountRate);

        // 3. VAT (8%) trên (Tiền món + Phụ thu)
        var taxableAmount = itemsTotal + surcharge;
        var vatAmount = Round(taxableAmount * _rules.VatRate);

        // 4. Tổng trước cọc = (Tiền món + Phụ thu + VAT) - Giảm giá
        var totalBeforeDeposit = (itemsTotal + surcharge + vatAmount) - discountAmount;
        totalBeforeDeposit = Math.Max(0, totalBeforeDeposit);

        // 5. Khấu trừ cọc
        var depositDeducted = Math.Min(depositAmount, totalBeforeDeposit);

        // 6. Tiền thực thu
        var finalAmount = Math.Max(0, totalBeforeDeposit - depositDeducted);

        return new BillingBreakdown(
            Round(itemsTotal),
            Round(surcharge),
            Round(vatAmount),
            Round(discountAmount),
            Round(depositDeducted),
            Round(finalAmount)
        );
    }

    public static decimal Round(decimal amount)
    {
        // Làm tròn đến 1.000đ theo quy tắc thương mại Việt Nam
        return Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
    }
}

public class RefundPolicy
{
    public (decimal RefundRate, decimal RefundAmount, string Reason) CalculateRefund(decimal depositAmount, DateTime bookingStartTimeUtc, DateTime cancelTimeUtc)
    {
        var deltaHours = (bookingStartTimeUtc - cancelTimeUtc).TotalHours;

        if (deltaHours >= 2.0)
        {
            return (1.0m, depositAmount, "Hủy trước giờ đặt từ 2 tiếng trở lên: Hoàn 100% tiền cọc.");
        }
        else if (deltaHours >= 0.0)
        {
            var halfRefund = BillingCalculator.Round(depositAmount * 0.5m);
            return (0.5m, halfRefund, "Hủy trong vòng 2 tiếng trước giờ đặt: Hoàn 50% tiền cọc.");
        }
        else
        {
            return (0.0m, 0m, "Hủy sau giờ hẹn hoặc không đến (No-Show): Không hoàn cọc theo quy định.");
        }
    }
}

public class LatePolicy
{
    private readonly ShiftRules _rules;

    public LatePolicy(ShiftRules rules)
    {
        _rules = rules;
    }

    public (bool IsLate, int LateMinutes) CheckLate(TimeOnly scheduledStartTime, TimeOnly actualCheckInTime)
    {
        var diffMinutes = (int)(actualCheckInTime - scheduledStartTime).TotalMinutes;
        if (diffMinutes > _rules.LateThresholdMinutes)
        {
            return (true, diffMinutes);
        }
        return (false, Math.Max(0, diffMinutes));
    }
}
