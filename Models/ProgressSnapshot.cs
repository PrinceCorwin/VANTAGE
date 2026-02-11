using System;
using System.ComponentModel;

namespace VANTAGE.Models
{
    // Frozen copy of Activity at weekly progress submission time
    // Composite Primary Key: UniqueID + WeekEndDate
    public class ProgressSnapshot : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Primary Key (part 1)
        public string UniqueID { get; set; } = null!;

        // Primary Key (part 2) - NOT NULL
        public DateTime WeekEndDate { get; set; }

        // Export tracking
        public string? ExportedBy { get; set; }
        public DateTime? ExportedDate { get; set; }

        // Activity fields (copied from Activity at submission time)
        public string Area { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public DateTime? AzureUploadUtcDate { get; set; }
        public string Aux1 { get; set; } = string.Empty;
        public string Aux2 { get; set; } = string.Empty;
        public string Aux3 { get; set; } = string.Empty;
        public double BaseUnit { get; set; }
        public double BudgetHoursGroup { get; set; }
        public double BudgetHoursROC { get; set; }
        public double BudgetMHs { get; set; }
        public string ChgOrdNO { get; set; } = string.Empty;
        public double ClientBudget { get; set; }
        public double ClientCustom3 { get; set; }
        public double ClientEquivQty { get; set; }
        public string CompType { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public int DateTrigger { get; set; }
        public string Description { get; set; } = string.Empty;
        public string DwgNO { get; set; } = string.Empty;
        public double EarnQtyEntry { get; set; }
        public double EarnedMHsRoc { get; set; }
        public string EqmtNO { get; set; } = string.Empty;
        public string EquivQTY { get; set; } = string.Empty;
        public string EquivUOM { get; set; } = string.Empty;
        public string Estimator { get; set; } = string.Empty;
        public int HexNO { get; set; }
        public string HtTrace { get; set; } = string.Empty;
        public string InsulType { get; set; } = string.Empty;
        public string LineNumber { get; set; } = string.Empty;
        public string MtrlSpec { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string PaintCode { get; set; } = string.Empty;
        public double PercentEntry { get; set; }
        public string PhaseCategory { get; set; } = string.Empty;
        public string PhaseCode { get; set; } = string.Empty;
        public string PipeGrade { get; set; } = string.Empty;
        public double PipeSize1 { get; set; }
        public double PipeSize2 { get; set; }
        public double PrevEarnMHs { get; set; }
        public double PrevEarnQTY { get; set; }
        public DateTime? ProgDate { get; set; }
        public string ProjectID { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string RevNO { get; set; } = string.Empty;
        public string RFINO { get; set; } = string.Empty;
        public double ROCBudgetQTY { get; set; }
        public double ROCID { get; set; }
        public double ROCPercent { get; set; }
        public string ROCStep { get; set; } = string.Empty;
        public string SchedActNO { get; set; } = string.Empty;
        public DateTime? ActFin { get; set; }
        public DateTime? ActStart { get; set; }
        public string SecondActno { get; set; } = string.Empty;
        public string SecondDwgNO { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string ShopField { get; set; } = string.Empty;
        public string ShtNO { get; set; } = string.Empty;
        public string SubArea { get; set; } = string.Empty;
        public string PjtSystem { get; set; } = string.Empty;
        public string PjtSystemNo { get; set; } = string.Empty;
        public string SystemNO { get; set; } = string.Empty;
        public string TagNO { get; set; } = string.Empty;
        public string UDF1 { get; set; } = string.Empty;
        public string UDF2 { get; set; } = string.Empty;
        public string UDF3 { get; set; } = string.Empty;
        public string UDF4 { get; set; } = string.Empty;
        public string UDF5 { get; set; } = string.Empty;
        public string UDF6 { get; set; } = string.Empty;
        public string UDF7 { get; set; } = string.Empty;
        public string UDF8 { get; set; } = string.Empty;
        public string UDF9 { get; set; } = string.Empty;
        public string UDF10 { get; set; } = string.Empty;
        public string UDF11 { get; set; } = string.Empty;
        public string UDF12 { get; set; } = string.Empty;
        public string UDF13 { get; set; } = string.Empty;
        public string UDF14 { get; set; } = string.Empty;
        public string UDF15 { get; set; } = string.Empty;
        public string UDF16 { get; set; } = string.Empty;
        public string UDF17 { get; set; } = string.Empty;
        public string RespParty { get; set; } = string.Empty;
        public string UDF20 { get; set; } = string.Empty;
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime UpdatedUtcDate { get; set; }
        public string UOM { get; set; } = string.Empty;
        public string WorkPackage { get; set; } = string.Empty;
        public double XRay { get; set; }

        // Returns true if ActStart is required but missing (percent > 0 needs a start date)
        public bool HasMissingActStart => PercentEntry > 0 && ActStart == null;

        // Returns true if ActFin is required but missing (percent = 100 needs a finish date)
        public bool HasMissingActFin => PercentEntry >= 100 && ActFin == null;
    }
}