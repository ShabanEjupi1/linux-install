using System;

namespace KosovaPOS.Models.HR
{
    /// <summary>
    /// Kosovo payroll tax calculator.
    /// Rates per ATK regulations (2025):
    ///   - Pension contributions: 5% employee + 5% employer (Ligji Nr. 04/L-101 KSPF)
    ///   - Personal Income Tax brackets:
    ///       0% on annual income ≤ €960
    ///       4% on €960 – €3,000
    ///       8% on €3,000 – €5,400
    ///      10% on > €5,400
    /// </summary>
    public static class KosovoPayrollCalculator
    {
        public const decimal PensionRate = 0.05m;   // 5%

        public static decimal EmployeePension(decimal grossMonthly) =>
            Math.Round(grossMonthly * PensionRate, 2);

        public static decimal EmployerPension(decimal grossMonthly) =>
            Math.Round(grossMonthly * PensionRate, 2);

        /// <summary>Calculates monthly personal income tax from monthly gross salary.</summary>
        public static decimal IncomeTax(decimal grossMonthly)
        {
            // Convert to annual, apply brackets, convert back to monthly
            decimal annual = grossMonthly * 12m;
            decimal annualTax = 0m;

            // Bracket 1: 0% on first €960
            if (annual <= 960m)
                annualTax = 0m;
            // Bracket 2: 4% on €960 – €3,000
            else if (annual <= 3000m)
                annualTax = (annual - 960m) * 0.04m;
            // Bracket 3: 8% on €3,000 – €5,400
            else if (annual <= 5400m)
                annualTax = (3000m - 960m) * 0.04m
                          + (annual - 3000m) * 0.08m;
            // Bracket 4: 10% on > €5,400
            else
                annualTax = (3000m - 960m) * 0.04m
                          + (5400m - 3000m) * 0.08m
                          + (annual - 5400m) * 0.10m;

            return Math.Round(annualTax / 12m, 2);
        }

        public static decimal CalculateNet(decimal grossMonthly)
        {
            var pension = EmployeePension(grossMonthly);
            var tax     = IncomeTax(grossMonthly);
            return Math.Round(grossMonthly - pension - tax, 2);
        }

        public static PayrollBreakdown Calculate(decimal grossMonthly, decimal bonus = 0, decimal overtime = 0)
        {
            var totalGross = grossMonthly + bonus + overtime;

            var empPension = EmployeePension(totalGross);
            var erPension  = EmployerPension(totalGross);
            var tax        = IncomeTax(totalGross);
            var net        = Math.Round(totalGross - empPension - tax, 2);

            return new PayrollBreakdown
            {
                GrossSalary     = grossMonthly,
                Bonus           = bonus,
                OvertimePay     = overtime,
                TotalGross      = totalGross,
                EmployeePension = empPension,
                EmployerPension = erPension,
                IncomeTax       = tax,
                NetPay          = net,
                TotalEmployerCost = totalGross + erPension
            };
        }
    }

    public class PayrollBreakdown
    {
        public decimal GrossSalary      { get; set; }
        public decimal Bonus            { get; set; }
        public decimal OvertimePay      { get; set; }
        public decimal TotalGross       { get; set; }
        public decimal EmployeePension  { get; set; }
        public decimal EmployerPension  { get; set; }
        public decimal IncomeTax        { get; set; }
        public decimal NetPay           { get; set; }
        public decimal TotalEmployerCost { get; set; }
    }
}
