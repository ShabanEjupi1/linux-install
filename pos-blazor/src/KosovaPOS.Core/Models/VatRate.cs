namespace KosovaPOS.Models
{
    /// <summary>
    /// Resolves the Kosovo VAT rate for an article from the two BMD columns that
    /// carry it, and extracts the VAT contained in a VAT-inclusive price.
    ///
    /// Shelf prices (<c>Artikujt.CShitjes</c>) are gross — VAT is already inside
    /// them — so VAT is never added on top, only split out of the total.
    /// </summary>
    public static class KosovoVat
    {
        public const decimal Standard = 18m; // class 3
        public const decimal Reduced = 8m;   // class 2
        public const decimal Zero = 0m;      // class 1

        /// <summary>
        /// BMD holds the VAT class in <c>Vat</c> and normally mirrors it in <c>Tatimi</c>,
        /// but part of the catalogue carries the literal percentage (18) in <c>Tatimi</c>
        /// instead. A value of 8 or more can only be a percentage — no class code reaches
        /// that high — so it is taken verbatim; anything else is read as a class.
        /// </summary>
        public static decimal Resolve(int? vatClass, double? tatimi)
        {
            if (tatimi is >= 8) return (decimal)tatimi.Value;
            return FromClass(vatClass ?? (int?)tatimi);
        }

        /// <summary>
        /// Unknown classes fall back to the standard rate: every article in the
        /// catalogue is standard-rated retail merchandise, and under-charging VAT
        /// is the costly direction of the error.
        /// </summary>
        public static decimal FromClass(int? vatClass) => vatClass switch
        {
            1 => Zero,
            2 => Reduced,
            _ => Standard,
        };

        /// <summary>The class code to store back alongside a rate.</summary>
        public static int ToClass(decimal rate) => rate switch
        {
            <= 0m => 1,
            <= 8m => 2,
            _ => 3,
        };

        /// <summary>
        /// The VAT contained within <paramref name="grossAmount"/>, rounded to cents.
        /// Net is always <c>gross - VatOf(gross)</c>, so net + VAT == gross exactly.
        /// </summary>
        public static decimal VatOf(decimal grossAmount, decimal rate)
        {
            if (rate <= 0m) return 0m;
            return decimal.Round(grossAmount * rate / (100m + rate), 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>The net (VAT-exclusive) part of a VAT-inclusive amount, to the cent.</summary>
        public static decimal NetOf(decimal grossAmount, decimal rate) => grossAmount - VatOf(grossAmount, rate);

        /// <summary>
        /// The net part of a VAT-inclusive unit price, unrounded. Unit prices are not
        /// money amounts — rounding them to cents would stop <c>qty * unit</c> from
        /// reconciling with the rounded line total.
        /// </summary>
        public static decimal NetUnitPrice(decimal grossUnitPrice, decimal rate) =>
            rate <= 0m ? grossUnitPrice : grossUnitPrice * 100m / (100m + rate);
    }
}
