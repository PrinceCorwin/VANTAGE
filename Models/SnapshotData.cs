namespace VANTAGE.Models
{
    // Container for a single VMS_ProgressSnapshots row as loaded from Azure for
    // Revert or Modify flows. Not all snapshot columns are represented — the ones
    // missing here (LocalDirty, SyncVersion, ExportedBy, ExportedDate, AssignedTo,
    // WeekEndDate) are either user-invisible or travel with the dialog's context,
    // so they never need to be carried on this object.
    public class SnapshotData
    {
        public string UniqueID { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string? AzureUploadUtcDate { get; set; }
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
        public string? ProgDate { get; set; }
        public string ProjectID { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public string RevNO { get; set; } = string.Empty;
        public string RFINO { get; set; } = string.Empty;
        public double ROCBudgetQTY { get; set; }
        public string ROCID { get; set; } = string.Empty;
        public double ROCPercent { get; set; }
        public string ROCStep { get; set; } = string.Empty;
        public string SchedActNO { get; set; } = string.Empty;
        public string? ActFin { get; set; }
        public string? ActStart { get; set; }
        public string? PlanStart { get; set; }
        public string? PlanFin { get; set; }
        public string SecondActno { get; set; } = string.Empty;
        public string SecondDwgNO { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string ShopField { get; set; } = string.Empty;
        public string ShtNO { get; set; } = string.Empty;
        public string SubArea { get; set; } = string.Empty;
        public string PjtSystem { get; set; } = string.Empty;
        public string PjtSystemNo { get; set; } = string.Empty;
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
        public string UOM { get; set; } = string.Empty;
        public string WorkPackage { get; set; } = string.Empty;
        public string XRay { get; set; } = string.Empty;
    }
}
