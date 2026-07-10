using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KosovaPOS.Models.BMDData
{
    /// <summary>
    /// Maps to SQL Server BMDData.tbl_Stoku — the per-article stock ledger the
    /// desktop wrote on every purchase, sale and correction.
    ///
    /// One row per document line. The quantity columns are mutually exclusive in
    /// practice: a purchase line fills <see cref="SasiaBlerje"/>, a sale line
    /// fills <see cref="SasiaShitje"/>, a recount fills <see cref="SasiaKorigjimi"/>.
    /// Net movement is therefore Blerje + Prodhimi + Korigjimi - Shitje.
    ///
    /// This is the audit trail behind <c>Artikujt.Sasia</c>. It is NOT the same as
    /// the web POS's own <c>StockMovements</c> table, which only records movements
    /// the Blazor app itself made.
    /// </summary>
    [Table("tbl_Stoku")]
    public class TblStoku
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("ArtikulliID")]
        public int? ArtikulliId { get; set; }

        [Column("Nr_Rendor")]
        public int? NrRendor { get; set; } // line ordinal within the document

        [Column("Dokumenti_NR")]
        public int? DokumentiNr { get; set; }

        [Column("Dokumenti_ID")]
        [StringLength(50)]
        public string? DokumentiId { get; set; } // e.g. "01-BV32", "01-KO1"

        [Column("TIPI")]
        [StringLength(100)]
        public string? Tipi { get; set; } // "BLERJA VENDORE", "SHITJA ME KUPON", …

        [Column("TIPI_ID")]
        public int? TipiId { get; set; }

        [Column("Data")]
        public DateOnly? Data { get; set; }

        [Column("SasiaBlerje")]
        public double? SasiaBlerje { get; set; } // quantity in (purchase)

        [Column("KostojaBlerje")]
        public double? KostojaBlerje { get; set; } // unit purchase cost

        [Column("KostojaShitje")]
        public double? KostojaShitje { get; set; } // unit sale price

        [Column("SasiaKorigjimi")]
        public double? SasiaKorigjimi { get; set; } // recount adjustment (signed)

        [Column("SasiaProdhimi")]
        public double? SasiaProdhimi { get; set; } // quantity in (production)

        [Column("SasiaShitje")]
        public double? SasiaShitje { get; set; } // quantity out (sale)

        [Column("LineID")]
        public int? LineId { get; set; }

        /// <summary>Signed effect of this line on <c>Artikujt.Sasia</c>.</summary>
        [NotMapped]
        public double NetQuantity =>
            (SasiaBlerje ?? 0) + (SasiaProdhimi ?? 0) + (SasiaKorigjimi ?? 0) - (SasiaShitje ?? 0);
    }

    /// <summary>
    /// Maps to SQL Server BMDData.Kartela_Subjektit — the running account ("kartela")
    /// of every business partner in <see cref="FurnitoriNew"/>.
    ///
    /// One row per document: <see cref="PerPagese"/> is what the document owed,
    /// <see cref="Pagoi"/> what was settled against it, and <see cref="Mbeti"/> the
    /// signed remainder (negative = overpaid). A partner's outstanding balance is
    /// the SUM of <see cref="Mbeti"/> over their rows.
    ///
    /// <see cref="SubjektiId"/> is <c>FurnitoriNew.Id</c>. There is no FK constraint.
    /// </summary>
    [Table("Kartela_Subjektit")]
    public class KartelaSubjektit
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("SUBJEKTI_ID")]
        public int? SubjektiId { get; set; } // -> FurnitoriNew.Id

        [Column("Metoda_ID")]
        public int? MetodaId { get; set; } // -> MetodaPagese.id

        [Column("Dokumenti_NR")]
        public int? DokumentiNr { get; set; }

        [Column("Dokumenti_ID")]
        [StringLength(50)]
        public string? DokumentiId { get; set; }

        [Column("TIPI")]
        [StringLength(100)]
        public string? Tipi { get; set; } // "BLERJA VENDORE" | "SHITJA ME KUPON"

        [Column("Pershkrimi")]
        [StringLength(100)]
        public string? Pershkrimi { get; set; }

        [Column("Data")]
        public DateOnly? Data { get; set; }

        [Column("PerPagese")]
        public double? PerPagese { get; set; } // due

        [Column("Pagoi")]
        public double? Pagoi { get; set; } // paid

        [Column("Mbeti")]
        public double? Mbeti { get; set; } // remaining (signed)
    }
}
