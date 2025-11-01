using System;
using System.ComponentModel;

namespace VANTAGE.Models
{
    public class Activity : INotifyPropertyChanged
    {
        // ========================================
        // BACKING FIELDS (Only for properties with custom logic)
        // ========================================

        // Progress tracking (bidirectional updates)
        private double _percentEntry;
        private double _earnQtyEntry;
        private double _quantity;
        private double _budgetMHs;

        // ========================================
        // SIMPLE AUTO-PROPERTIES (No backing fields needed)
        // ========================================

        // IDs
        public int ActivityID { get; set; }
        public int HexNO { get; set; }

        // Categories
        public string CompType { get; set; }
        public string PhaseCategory { get; set; }
        public string ROCStep { get; set; }

        // Drawings
        public string DwgNO { get; set; }
        public string RevNO { get; set; }
        public string SecondDwgNO { get; set; }
        public string ShtNO { get; set; }

        // Notes
        public string Notes { get; set; }

        // Schedule
        public string OldActno { get; set; }
        public string Start { get; set; }
        public string Finish { get; set; }
        public string Status
        {
            get
            {
                if (PercentEntry == 0) return "Not Started";
                if (PercentEntry >= 1.0) return "Complete";
                return "In Progress";
            }
        }
        // Tags - Core Fields
        public string TagNO { get; set; }
        public string Description { get; set; }
        public string Area { get; set; }
        public string SubArea { get; set; }
        public string System { get; set; }
        public string SystemNO { get; set; }
        public string ProjectID { get; set; }
        public string WorkPackage { get; set; }
        public string PhaseCode { get; set; }
        public string Service { get; set; }
        public string ShopField { get; set; }

        // Tags - Equipment/Line
        public string EqmtNO { get; set; }
        public string LineNO { get; set; }
        public string ChgOrdNO { get; set; }

        // Tags - Material Specs
        public string MtrlSpec { get; set; }
        public string PipeGrade { get; set; }
        public string PaintCode { get; set; }
        public string InsulType { get; set; }
        public string HtTrace { get; set; }

        // Tags - Auxiliary
        public string Aux1 { get; set; }
        public string Aux2 { get; set; }
        public string Aux3 { get; set; }
        public string Estimator { get; set; }
        public string RFINO { get; set; }
        public string SchedActNO { get; set; }
        public double XRay { get; set; }

        // Trigger
        public int DateTrigger { get; set; }
        // User-Defined Fields
        public string UDFOne { get; set; }
        public string UDFTwo { get; set; }
        public string UDFThree { get; set; }
        public string UDFFour { get; set; }
        public string UDFFive { get; set; }
        public string UDFSix { get; set; }
        public int UDFSeven { get; set; }
        public string UDFEight { get; set; }
        public string UDFNine { get; set; }
        public string UDFTen { get; set; }
        public string UDFFourteen { get; set; }
        public string UDFFifteen { get; set; }
        public string UDFSixteen { get; set; }
        public string UDFSeventeen { get; set; }
        public string UDFEighteen { get; set; }
        public string UDFTwenty { get; set; }
        public string AzureUploadDate { get; set; }
        public string ProgDate { get; set; }

        // Special UDF Fields (with helper properties)
        private string _assignedTo;
        public string AssignedTo
        {
            get => _assignedTo;
            set
            {
                if (_assignedTo != value)
                {
                    _assignedTo = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMyRecord));
                    OnPropertyChanged(nameof(IsEditable));
                }
            }
        }

        public string LastModifiedBy { get; set; }
        public string CreatedBy { get; set; }
        public string UniqueID { get; set; }  // Read-only unique identifier

        // Helper properties for assignment
        public string AssignedToUsername => AssignedTo;

        public bool IsMyRecord
        {
            get
            {
                if (App.CurrentUser == null) return false;
                return AssignedTo == App.CurrentUser.Username;
            }
        }

        public bool IsEditable
        {
            get
            {
                if (App.CurrentUser == null) return false;
                return AssignedTo == App.CurrentUser.Username;
            }
        }
        // ========================================
        // VALUES - BUDGETED
        // ========================================

        public double BaseUnit { get; set; }

        public double BudgetMHs
        {
            get => _budgetMHs;
            set
            {
                if (Math.Abs(_budgetMHs - value) > 0.0001)
                {
                    _budgetMHs = value;
                    OnPropertyChanged();
                    RecalculatePercentEarned();
                }
            }
        }

        public double BudgetHoursGroup { get; set; }
        public double BudgetHoursROC { get; set; }

        // ========================================
        // VALUES - PROGRESS (User Editable with Bidirectional Updates)
        // ========================================

        public double Quantity
        {
            get => _quantity;
            set
            {
                if (Math.Abs(_quantity - value) > 0.0001)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    UpdatePercCompleteFromEarnedQty();
                }
            }
        }

        public string UOM { get; set; }

        public double EarnQtyEntry
        {
            get => _earnQtyEntry;
            set
            {
                if (Math.Abs(_earnQtyEntry - value) > 0.0001)
                {
                    _earnQtyEntry = value;
                    OnPropertyChanged();
                    UpdatePercCompleteFromEarnedQty();
                }
            }
        }

        public double PercentEntry
        {
            get => _percentEntry;
            set
            {
                if (Math.Abs(_percentEntry - value) > 0.0001)
                {
                    _percentEntry = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PercentEntry_Display));
                    UpdateEarnedQtyFromPercComplete();
                }
            }
        }

        /// <summary>
        /// Display PercentEntry as percentage (0-100)
        /// </summary>
        public double PercentEntry_Display
        {
            get => PercentEntry * 100;
            set
            {
                PercentEntry = value / 100.0;
            }
        }

        // ========================================
        // VALUES - CALCULATED (Read-Only)
        // ========================================

        /// <summary>
        /// Calculated: PercentEntry (for Azure compatibility)
        /// </summary>
        public double PercentCompleteCalc
        {
            get => PercentEntry;
        }

        /// <summary>
        /// Display PercentCompleteCalc as percentage (0-100)
        /// </summary>
        public double PercentCompleteCalc_Display
        {
            get => PercentCompleteCalc * 100;
        }

        /// <summary>
        /// Calculated: EarnQtyEntry / Quantity (decimal ratio)
        /// </summary>
        public double EarnedQtyCalc
        {
            get => Quantity > 0 ? EarnQtyEntry / Quantity : 0;
        }

        /// <summary>
        /// Display EarnedQtyCalc as percentage (0-100)
        /// </summary>
        public double EarnedQtyCalc_Display
        {
            get => EarnedQtyCalc * 100;
        }

        /// <summary>
        /// Calculated: PercentEntry * BudgetMHs
        /// </summary>
        public double EarnMHsCalc { get; private set; }

        public double EarnedMHsRoc { get; set; }
        // ========================================
        // VALUES - EQUIPMENT
        // ========================================

        public double EquivQTY { get; set; }
        public string EquivUOM { get; set; }

        // ========================================
        // VALUES - ROC (Rate of Completion)
        // ========================================

        public double ROCID { get; set; }

        /// <summary>
        /// Calculated: ProjectID & "|" & CompType & "|" & PhaseCategory & "|" & ROCStep
        /// </summary>
        public string ROCLookupID
        {
            get => $"{ProjectID}|{CompType}|{PhaseCategory}|{ROCStep}";
        }

        public double ROCPercent { get; set; }
        public double ROCBudgetQTY { get; set; }

        // ========================================
        // VALUES - PIPE
        // ========================================

        public double PipeSize1 { get; set; }
        public double PipeSize2 { get; set; }

        // ========================================
        // VALUES - HISTORY/PREVIOUS
        // ========================================

        public double PrevEarnMHs { get; set; }
        public double PrevEarnQTY { get; set; }
        public string? WeekEndDate { get; set; }

        // ========================================
        // VALUES - CLIENT
        // ========================================

        public double ClientEquivQty { get; set; }
        public double ClientBudget { get; set; }
        public double ClientCustom3 { get; set; }

        /// <summary>
        /// Calculated: IF(EarnMHsCalc > 0, ROUND((EarnMHsCalc / BudgetMHs) * ClientEquivQty, 3), 0)
        /// </summary>
        public double ClientEquivEarnQTY { get; private set; }

        // ========================================
        // CALCULATION METHODS
        // ========================================

        /// <summary>
        /// Update PercentEntry based on EarnQtyEntry
        /// Called when user edits EarnQtyEntry
        /// </summary>
        private void UpdatePercCompleteFromEarnedQty()
        {
            if (Quantity > 0)
            {
                double newPercComplete = EarnQtyEntry / Quantity;
                if (Math.Abs(_percentEntry - newPercComplete) > 0.001)
                {
                    _percentEntry = Math.Round(newPercComplete, 4);
                    OnPropertyChanged(nameof(PercentEntry));
                    OnPropertyChanged(nameof(PercentEntry_Display));
                }
            }
            RecalculatePercentEarned();
        }

        /// <summary>
        /// Update EarnQtyEntry based on PercentEntry
        /// Called when user edits PercentEntry
        /// </summary>
        private void UpdateEarnedQtyFromPercComplete()
        {
            if (Quantity > 0)
            {
                double newEarnedQty = PercentEntry * Quantity;
                if (Math.Abs(_earnQtyEntry - newEarnedQty) > 0.001)
                {
                    _earnQtyEntry = Math.Round(newEarnedQty, 4);
                    OnPropertyChanged(nameof(EarnQtyEntry));
                }
            }
            RecalculatePercentEarned();
        }

        /// <summary>
        /// Recalculate all dependent fields
        /// </summary>
        private void RecalculatePercentEarned()
        {
            OnPropertyChanged(nameof(PercentCompleteCalc));
            OnPropertyChanged(nameof(PercentCompleteCalc_Display));
            OnPropertyChanged(nameof(EarnedQtyCalc));
            OnPropertyChanged(nameof(EarnedQtyCalc_Display));
            OnPropertyChanged(nameof(Status));

            RecalculateEarnedHours();
            RecalculateClientEarnedQty();
        }

        /// <summary>
        /// Calculate EarnMHsCalc
        /// Formula: IF(PercentCompleteCalc >= 1, BudgetMHs, ROUND(PercentCompleteCalc * BudgetMHs, 3))
        /// </summary>
        private void RecalculateEarnedHours()
        {
            if (PercentCompleteCalc >= 1.0)
            {
                EarnMHsCalc = BudgetMHs;
            }
            else
            {
                EarnMHsCalc = Math.Round(PercentCompleteCalc * BudgetMHs, 3);
            }
            OnPropertyChanged(nameof(EarnMHsCalc));
        }

        /// <summary>
        /// Calculate ClientEquivEarnQTY
        /// Formula: IF(EarnMHsCalc > 0, ROUND((EarnMHsCalc / BudgetMHs) * ClientEquivQty, 3), 0)
        /// </summary>
        private void RecalculateClientEarnedQty()
        {
            if (EarnMHsCalc > 0 && BudgetMHs > 0)
            {
                ClientEquivEarnQTY = Math.Round((EarnMHsCalc / BudgetMHs) * ClientEquivQty, 3);
            }
            else
            {
                ClientEquivEarnQTY = 0;
            }
            OnPropertyChanged(nameof(ClientEquivEarnQTY));
        }

        // ========================================
        // INOTIFYPROPERTYCHANGED IMPLEMENTATION
        // ========================================

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}