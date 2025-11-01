﻿using System;
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
        public string SecondActno { get; set; }
        public DateTime? SchStart { get; set; }
        public DateTime? SchFinish { get; set; }
        public DateTime? ProgDate { get; set; }
        public DateTime? WeekEndDate { get; set; }
        public DateTime? AzureUploadDate { get; set; }

        /// <summary>
        /// Status based on PercentEntry (0-100)
        /// </summary>
        public string Status
        {
            get
            {
                if (PercentEntry == 0) return "Not Started";
                if (PercentEntry >= 100) return "Complete";
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
        public string UDF1 { get; set; }
        public string UDF2 { get; set; }
        public string UDF3 { get; set; }
        public string UDF4 { get; set; }
        public string UDF5 { get; set; }
        public string UDF6 { get; set; }
        public int UDF7 { get; set; }
        public string UDF8 { get; set; }
        public string UDF9 { get; set; }
        public string UDF10 { get; set; }
        public string UDF14 { get; set; }
        public string UDF15 { get; set; }
        public string UDF16 { get; set; }
        public string UDF17 { get; set; }
        public string UDF18 { get; set; }
        public string UDF20 { get; set; }

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

        /// <summary>
        /// PercentEntry: STORED AS 0-100 (percentage)
        /// Example: 75.5 means 75.5%
        /// </summary>
        public double PercentEntry
        {
            get => _percentEntry;
            set
            {
                // Clamp to 0-100 range
                double clampedValue = Math.Max(0, Math.Min(100, value));

                if (Math.Abs(_percentEntry - clampedValue) > 0.0001)
                {
                    _percentEntry = clampedValue;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PercentEntry_Display));
                    OnPropertyChanged(nameof(Status));
                    UpdateEarnedQtyFromPercComplete();
                }
            }
        }

        /// <summary>
        /// Display PercentEntry with % symbol
        /// Same as PercentEntry since we store as 0-100
        /// </summary>
        public string PercentEntry_Display => $"{PercentEntry:F1}%";

        /// <summary>
        /// Helper method for Excel import: convert 0-1 decimal to 0-100 percentage
        /// Example: 0.755 → 75.5
        /// </summary>
        public void SetPercentFromDecimal(double decimalValue)
        {
            PercentEntry = decimalValue * 100;
        }

        /// <summary>
        /// Helper method for Excel export: convert 0-100 percentage to 0-1 decimal
        /// Example: 75.5 → 0.755
        /// </summary>
        public double GetPercentAsDecimal()
        {
            return PercentEntry / 100;
        }

        // ========================================
        // VALUES - CALCULATED (Read-Only)
        // ========================================

        /// <summary>
        /// Calculated: PercentEntry (for backward compatibility)
        /// Since PercentEntry is already 0-100, this just returns it
        /// </summary>
        public double PercentCompleteCalc => PercentEntry;

        /// <summary>
        /// Display PercentCompleteCalc as percentage string
        /// </summary>
        public string PercentCompleteCalc_Display => $"{PercentCompleteCalc:F1}%";

        /// <summary>
        /// Calculated: EarnQtyEntry / Quantity (as percentage 0-100)
        /// </summary>
        public double EarnedQtyCalc
        {
            get => Quantity > 0 ? (EarnQtyEntry / Quantity) * 100 : 0;
        }

        /// <summary>
        /// Display EarnedQtyCalc as percentage string
        /// </summary>
        public string EarnedQtyCalc_Display => $"{EarnedQtyCalc:F1}%";

        /// <summary>
        /// Calculated: PercentEntry / 100 * BudgetMHs
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
                // Calculate as percentage (0-100)
                double newPercComplete = (EarnQtyEntry / Quantity) * 100;
                if (Math.Abs(_percentEntry - newPercComplete) > 0.001)
                {
                    _percentEntry = Math.Round(newPercComplete, 4);
                    OnPropertyChanged(nameof(PercentEntry));
                    OnPropertyChanged(nameof(PercentEntry_Display));
                    OnPropertyChanged(nameof(Status));
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
                // PercentEntry is 0-100, so divide by 100 for calculation
                double newEarnedQty = (PercentEntry / 100) * Quantity;
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
        /// Formula: PercentEntry / 100 * BudgetMHs
        /// </summary>
        private void RecalculateEarnedHours()
        {
            // PercentEntry is 0-100, so divide by 100
            if (PercentEntry >= 100)
            {
                EarnMHsCalc = BudgetMHs;
            }
            else
            {
                EarnMHsCalc = Math.Round((PercentEntry / 100) * BudgetMHs, 3);
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