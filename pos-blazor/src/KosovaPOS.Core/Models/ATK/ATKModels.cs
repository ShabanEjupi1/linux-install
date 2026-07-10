using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.ATK
{
    // TVSh (VAT) Monthly Declaration – TV-1
    [Table("ATK_TVShDeclarations")]
    public class TVShDeclaration
    {
        [Key] public int Id { get; set; }
        public int Year  { get; set; }
        public int Month { get; set; }

        // Sales / Output VAT
        public decimal SalesStandardRate   { get; set; }  // 18%
        public decimal SalesReducedRate    { get; set; }  // 8%
        public decimal SalesZeroRate       { get; set; }  // 0%
        public decimal SalesExempt         { get; set; }
        public decimal OutputVAT           { get; set; }

        // Purchases / Input VAT
        public decimal PurchasesStandard   { get; set; }
        public decimal PurchasesReduced    { get; set; }
        public decimal InputVAT            { get; set; }

        // Calculation
        public decimal NetVAT              { get; set; }  // OutputVAT - InputVAT
        public decimal VATPayable          { get; set; }  // > 0 pay; < 0 refund

        public string  Status              { get; set; } = "Draft"; // Draft/Filed
        public DateTime CreatedAt          { get; set; } = DateTime.Now;
        public DateTime? FiledAt           { get; set; }
        public string?   Notes             { get; set; }
    }

    // Tatimi mbi Pagat (TP-1) – Monthly Payroll Tax Declaration
    [Table("ATK_PayrollTaxDeclarations")]
    public class PayrollTaxDeclaration
    {
        [Key] public int Id { get; set; }
        public int Year  { get; set; }
        public int Month { get; set; }

        public int     EmployeeCount         { get; set; }
        public decimal TotalGrossWages       { get; set; }
        public decimal EmployeePension       { get; set; }  // 5%
        public decimal EmployerPension       { get; set; }  // 5%
        public decimal TotalTAP              { get; set; }  // Income tax withheld
        public decimal TotalContributions    { get; set; }  // Employee + Employer pension

        public string  Status                { get; set; } = "Draft";
        public DateTime CreatedAt            { get; set; } = DateTime.Now;
        public DateTime? FiledAt             { get; set; }

        [NotMapped] public decimal TotalToRemit => TotalTAP + TotalContributions;
    }

    // TAK – Annual Corporate Income Tax
    [Table("ATK_CorporateTaxDeclarations")]
    public class CorporateTaxDeclaration
    {
        [Key] public int Id { get; set; }
        public int Year { get; set; }

        public decimal TotalRevenue          { get; set; }
        public decimal AllowableDeductions   { get; set; }
        public decimal TaxableIncome         { get; set; }
        public decimal TaxRate               { get; set; } = 10m;  // 10% Kosovo corporate tax
        public decimal TaxLiability          { get; set; }
        public decimal PrepaymentsMade       { get; set; }
        public decimal TaxPayable            { get; set; }

        public string  Status                { get; set; } = "Draft";
        public DateTime CreatedAt            { get; set; } = DateTime.Now;
        public DateTime? FiledAt             { get; set; }
        public string?   Notes              { get; set; }
    }

    // Tatimi në Burim – Withholding Tax (WH-1)
    [Table("ATK_WithholdingTaxDeclarations")]
    public class WithholdingTaxDeclaration
    {
        [Key] public int Id { get; set; }
        public int Year  { get; set; }
        public int Month { get; set; }

        public string? PayeeBusinessName     { get; set; }
        public string? PayeeNUI              { get; set; }
        public string? ServiceDescription    { get; set; }
        public decimal GrossPayment          { get; set; }
        public decimal WithholdingRate       { get; set; } = 5m;   // 5% standard WHT Kosovo
        public decimal TaxWithheld           { get; set; }

        public string  Status                { get; set; } = "Draft";
        public DateTime CreatedAt            { get; set; } = DateTime.Now;
    }
}
