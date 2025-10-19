using System;
using System.ComponentModel;

namespace VANTAGE.Models
{
    public class Activity : INotifyPropertyChanged
    {
        // ========================================
        // PRIVATE BACKING FIELDS (EDITABLE ONLY)
        // ========================================

        // Categories (editable)
        private string _catg_ComponentType;
        private string _catg_PhaseCategory;
        private string _catg_ROC_Step;

        // Notes (editable)
        private string _notes_Comments;

        // Tags (editable subset)
        private string _tag_Aux1;
        private string _tag_Aux2;
        private string _tag_Aux3;
        private string _tag_Area;
        private string _tag_Descriptions;
        private string _tag_Phase_Code;
        private string _tag_ProjectID;
        private string _tag_Sch_ActNo;
        private string _tag_Service;
        private string _tag_ShopField;
        private string _tag_SubArea;
        private string _tag_System;
        private string _tag_TagNo;
        private string _tag_WorkPackage;

        // UDF Fields (editable subset)
        private string _udfOne;
        private string _udfTwo;
        private string _udfThree;
        private string _udfFour;
        private string _udfFive;
        private string _udfSix;
        private int _udfSeven;
        private string _udfEight;
        private string _udfNine;
        private string _udfTen;
        private string _udfEleven;      // AssignedToUsername
        private string _udfFourteen;
        private string _udfFifteen;
        private string _udfSixteen;
        private string _udfSeventeen;
        private string _udfEighteen;
        private string _udfTwenty;

        // Values (editable)
        private double _val_BudgetedHours_Ind;
        private double _val_EarnedQty;
        private double _val_Perc_Complete;
        private double _val_Quantity;
        private string _val_UOM;
        private double _val_Pipe_Size1;
        private double _val_Pipe_Size2;
        private double _val_UDF_Two;
        private double _val_UDF_Three;

        // Calculated (private setters only)
        private double _val_EarnedHours_Ind;
        private double _val_Earn_Qty;
        private double _val_Percent_Earned;
        private string _lookUP_ROC_ID;
        private double _val_Client_Earned_EQ_QTY;

        // ========================================
        // READ-ONLY PROPERTIES (Simple Auto-Properties)
        // ========================================

        public int ActivityID { get; set; }
        public int HexNO { get; set; }

        // Drawings (read-only)
        public string Dwg_PrimeDrawingNO { get; set; }
        public string Dwg_RevisionNo { get; set; }
        public string Dwg_SecondaryDrawingNO { get; set; }
        public string Dwg_ShtNo { get; set; }

        // Schedule (read-only)
        public string Sch_Actno { get; set; }
        public string Sch_Start { get; set; }
        public string Sch_Finish { get; set; }
        public string Sch_Status { get; set; }

        // Tags (read-only subset)
        public string Tag_CONo { get; set; }
        public string Tag_EqmtNo { get; set; }
        public string Tag_Estimator { get; set; }
        public string Tag_Insulation_Typ { get; set; }
        public string Tag_LineNo { get; set; }
        public string Tag_Matl_Spec { get; set; }
        public string Tag_Paint_Code { get; set; }
        public string Tag_Pipe_Grade { get; set; }
        public string Tag_RFINo { get; set; }
        public string Tag_SystemNo { get; set; }
        public string Tag_Tracing { get; set; }
        public double Tag_XRAY { get; set; }

        // Trigger
        public int Trg_DateTrigger { get; set; }

        // UDF (read-only subset)
        public string UDFTwelve { get; set; }      // LastModifiedBy
        public string UDFThirteen { get; set; }    // CreatedBy
        public string UDFNineteen { get; set; }    // Unique Activity ID (READ ONLY)

        // Values (read-only)
        public double Val_Base_Unit { get; set; }
        public double Val_BudgetedHours_Group { get; set; }
        public double Val_BudgetedHours_ROC { get; set; }
        public int Val_EarnedHours_ROC { get; set; }
        public double Val_EQ_QTY { get; set; }
        public string Val_EQ_UOM { get; set; }
        public int Tag_ROC_ID { get; set; }
        public double Val_ROC_Perc { get; set; }
        public double Val_ROC_BudgetQty { get; set; }
        public double Val_Prev_Earned_Hours { get; set; }
        public double Val_Prev_Earned_Qty { get; set; }
        public string Val_TimeStamp { get; set; }
        public double Val_Client_EQ_QTY_BDG { get; set; }
        // ========================================
        // EDITABLE PROPERTIES (With INotifyPropertyChanged)
        // ========================================

        // === CATEGORIES ===

        public string Catg_ComponentType
        {
            get => _catg_ComponentType;
            set
            {
                _catg_ComponentType = value;
                OnPropertyChanged(nameof(Catg_ComponentType));
                RecalculateLookupROCID();
            }
        }

        public string Catg_PhaseCategory
        {
            get => _catg_PhaseCategory;
            set
            {
                _catg_PhaseCategory = value;
                OnPropertyChanged(nameof(Catg_PhaseCategory));
                RecalculateLookupROCID();
            }
        }

        public string Catg_ROC_Step
        {
            get => _catg_ROC_Step;
            set
            {
                _catg_ROC_Step = value;
                OnPropertyChanged(nameof(Catg_ROC_Step));
                RecalculateLookupROCID();
            }
        }

        // === NOTES ===

        public string Notes_Comments
        {
            get => _notes_Comments;
            set { _notes_Comments = value; OnPropertyChanged(nameof(Notes_Comments)); }
        }

        // === TAGS (Editable) ===

        public string Tag_Aux1
        {
            get => _tag_Aux1;
            set { _tag_Aux1 = value; OnPropertyChanged(nameof(Tag_Aux1)); }
        }

        public string Tag_Aux2
        {
            get => _tag_Aux2;
            set { _tag_Aux2 = value; OnPropertyChanged(nameof(Tag_Aux2)); }
        }

        public string Tag_Aux3
        {
            get => _tag_Aux3;
            set { _tag_Aux3 = value; OnPropertyChanged(nameof(Tag_Aux3)); }
        }

        public string Tag_Area
        {
            get => _tag_Area;
            set { _tag_Area = value; OnPropertyChanged(nameof(Tag_Area)); }
        }

        public string Tag_Descriptions
        {
            get => _tag_Descriptions;
            set { _tag_Descriptions = value; OnPropertyChanged(nameof(Tag_Descriptions)); }
        }

        public string Tag_Phase_Code
        {
            get => _tag_Phase_Code;
            set { _tag_Phase_Code = value; OnPropertyChanged(nameof(Tag_Phase_Code)); }
        }

        public string Tag_ProjectID
        {
            get => _tag_ProjectID;
            set
            {
                _tag_ProjectID = value;
                OnPropertyChanged(nameof(Tag_ProjectID));
                RecalculateLookupROCID();
            }
        }

        public string Tag_Sch_ActNo
        {
            get => _tag_Sch_ActNo;
            set { _tag_Sch_ActNo = value; OnPropertyChanged(nameof(Tag_Sch_ActNo)); }
        }

        public string Tag_Service
        {
            get => _tag_Service;
            set { _tag_Service = value; OnPropertyChanged(nameof(Tag_Service)); }
        }

        public string Tag_ShopField
        {
            get => _tag_ShopField;
            set { _tag_ShopField = value; OnPropertyChanged(nameof(Tag_ShopField)); }
        }

        public string Tag_SubArea
        {
            get => _tag_SubArea;
            set { _tag_SubArea = value; OnPropertyChanged(nameof(Tag_SubArea)); }
        }

        public string Tag_System
        {
            get => _tag_System;
            set { _tag_System = value; OnPropertyChanged(nameof(Tag_System)); }
        }

        public string Tag_TagNo
        {
            get => _tag_TagNo;
            set { _tag_TagNo = value; OnPropertyChanged(nameof(Tag_TagNo)); }
        }

        public string Tag_WorkPackage
        {
            get => _tag_WorkPackage;
            set { _tag_WorkPackage = value; OnPropertyChanged(nameof(Tag_WorkPackage)); }
        }

        // === UDF FIELDS (Editable) ===

        public string UDFOne
        {
            get => _udfOne;
            set { _udfOne = value; OnPropertyChanged(nameof(UDFOne)); }
        }

        public string UDFTwo
        {
            get => _udfTwo;
            set { _udfTwo = value; OnPropertyChanged(nameof(UDFTwo)); }
        }

        public string UDFThree
        {
            get => _udfThree;
            set { _udfThree = value; OnPropertyChanged(nameof(UDFThree)); }
        }

        public string UDFFour
        {
            get => _udfFour;
            set { _udfFour = value; OnPropertyChanged(nameof(UDFFour)); }
        }

        public string UDFFive
        {
            get => _udfFive;
            set { _udfFive = value; OnPropertyChanged(nameof(UDFFive)); }
        }

        public string UDFSix
        {
            get => _udfSix;
            set { _udfSix = value; OnPropertyChanged(nameof(UDFSix)); }
        }

        public int UDFSeven
        {
            get => _udfSeven;
            set { _udfSeven = value; OnPropertyChanged(nameof(UDFSeven)); }
        }

        public string UDFEight
        {
            get => _udfEight;
            set { _udfEight = value; OnPropertyChanged(nameof(UDFEight)); }
        }

        public string UDFNine
        {
            get => _udfNine;
            set { _udfNine = value; OnPropertyChanged(nameof(UDFNine)); }
        }

        public string UDFTen
        {
            get => _udfTen;
            set { _udfTen = value; OnPropertyChanged(nameof(UDFTen)); }
        }

        // REPURPOSED: AssignedToUsername
        public string UDFEleven
        {
            get => _udfEleven;
            set
            {
                _udfEleven = value;
                OnPropertyChanged(nameof(UDFEleven));
                OnPropertyChanged(nameof(AssignedToUsername));
                OnPropertyChanged(nameof(IsMyRecord));
            }
        }

        public string UDFFourteen
        {
            get => _udfFourteen;
            set { _udfFourteen = value; OnPropertyChanged(nameof(UDFFourteen)); }
        }

        public string UDFFifteen
        {
            get => _udfFifteen;
            set { _udfFifteen = value; OnPropertyChanged(nameof(UDFFifteen)); }
        }

        public string UDFSixteen
        {
            get => _udfSixteen;
            set { _udfSixteen = value; OnPropertyChanged(nameof(UDFSixteen)); }
        }

        public string UDFSeventeen
        {
            get => _udfSeventeen;
            set { _udfSeventeen = value; OnPropertyChanged(nameof(UDFSeventeen)); }
        }

        public string UDFEighteen
        {
            get => _udfEighteen;
            set { _udfEighteen = value; OnPropertyChanged(nameof(UDFEighteen)); }
        }

        public string UDFTwenty
        {
            get => _udfTwenty;
            set { _udfTwenty = value; OnPropertyChanged(nameof(UDFTwenty)); }
        }

        // === VALUES (Editable) ===

        public double Val_BudgetedHours_Ind
        {
            get => _val_BudgetedHours_Ind;
            set
            {
                _val_BudgetedHours_Ind = value;
                OnPropertyChanged(nameof(Val_BudgetedHours_Ind));
                RecalculateEarnedHours();
                RecalculateClientEarnedQty();
            }
        }

        // PRIMARY EDITABLE: Val_EarnedQty
        public double Val_EarnedQty
        {
            get => _val_EarnedQty;
            set
            {
                if (Math.Abs(_val_EarnedQty - value) > 0.0001)
                {
                    _val_EarnedQty = value;
                    OnPropertyChanged(nameof(Val_EarnedQty));
                    UpdatePercCompleteFromEarnedQty();
                }
            }
        }

        // PRIMARY EDITABLE: Val_Perc_Complete
        public double Val_Perc_Complete
        {
            get => _val_Perc_Complete;
            set
            {
                if (Math.Abs(_val_Perc_Complete - value) > 0.0001)
                {
                    _val_Perc_Complete = value;
                    OnPropertyChanged(nameof(Val_Perc_Complete));
                    UpdateEarnedQtyFromPercComplete();
                }
            }
        }

        public double Val_Quantity
        {
            get => _val_Quantity;
            set
            {
                _val_Quantity = value;
                OnPropertyChanged(nameof(Val_Quantity));
                RecalculatePercentEarned();
            }
        }

        public string Val_UOM
        {
            get => _val_UOM;
            set { _val_UOM = value; OnPropertyChanged(nameof(Val_UOM)); }
        }

        public double Val_Pipe_Size1
        {
            get => _val_Pipe_Size1;
            set { _val_Pipe_Size1 = value; OnPropertyChanged(nameof(Val_Pipe_Size1)); }
        }

        public double Val_Pipe_Size2
        {
            get => _val_Pipe_Size2;
            set { _val_Pipe_Size2 = value; OnPropertyChanged(nameof(Val_Pipe_Size2)); }
        }

        public double Val_UDF_Two
        {
            get => _val_UDF_Two;
            set { _val_UDF_Two = value; OnPropertyChanged(nameof(Val_UDF_Two)); }
        }

        public double Val_UDF_Three
        {
            get => _val_UDF_Three;
            set { _val_UDF_Three = value; OnPropertyChanged(nameof(Val_UDF_Three)); }
        }
        // ========================================
        // CALCULATED PROPERTIES (Read-Only)
        // ========================================

        public double Val_EarnedHours_Ind
        {
            get => _val_EarnedHours_Ind;
            private set
            {
                _val_EarnedHours_Ind = value;
                OnPropertyChanged(nameof(Val_EarnedHours_Ind));
            }
        }

        public double Val_Earn_Qty
        {
            get => _val_Earn_Qty;
            private set
            {
                _val_Earn_Qty = value;
                OnPropertyChanged(nameof(Val_Earn_Qty));
            }
        }

        public double Val_Percent_Earned
        {
            get => _val_Percent_Earned;
            private set
            {
                _val_Percent_Earned = value;
                OnPropertyChanged(nameof(Val_Percent_Earned));
                OnPropertyChanged(nameof(Status));
            }
        }

        public string LookUP_ROC_ID
        {
            get => _lookUP_ROC_ID;
            private set
            {
                _lookUP_ROC_ID = value;
                OnPropertyChanged(nameof(LookUP_ROC_ID));
            }
        }

        public double VAL_Client_Earned_EQ_QTY
        {
            get => _val_Client_Earned_EQ_QTY;
            private set
            {
                _val_Client_Earned_EQ_QTY = value;
                OnPropertyChanged(nameof(VAL_Client_Earned_EQ_QTY));
            }
        }

        // ========================================
        // DERIVED/COMPUTED PROPERTIES
        // ========================================

        /// <summary>
        /// User-friendly property for AssignedToUsername (maps to UDFEleven)
        /// </summary>
        public string AssignedToUsername
        {
            get => UDFEleven;
            set => UDFEleven = value;
        }

        /// <summary>
        /// User-friendly property for LastModifiedBy (maps to UDFTwelve)
        /// </summary>
        public string LastModifiedBy
        {
            get => UDFTwelve;
            set => UDFTwelve = value;
        }

        /// <summary>
        /// User-friendly property for CreatedBy (maps to UDFThirteen)
        /// </summary>
        public string CreatedBy
        {
            get => UDFThirteen;
            set => UDFThirteen = value;
        }

        /// <summary>
        /// Status derived from Val_Percent_Earned
        /// </summary>
        public string Status
        {
            get
            {
                if (Val_Percent_Earned == 0)
                    return "Not Started";
                else if (Val_Percent_Earned >= 1.0)
                    return "Complete";
                else
                    return "In Progress";
            }
        }

        /// <summary>
        /// Check if this record is assigned to the current user
        /// </summary>
        public bool IsMyRecord => UDFEleven == App.CurrentUser?.Username;

        // ========================================
        // CALCULATION METHODS
        // ========================================

        /// <summary>
        /// Bidirectional: Update Val_Perc_Complete when Val_EarnedQty changes
        /// Formula: Val_Perc_Complete = (Val_EarnedQty / Val_Quantity) * 100
        /// </summary>
        private void UpdatePercCompleteFromEarnedQty()
        {
            if (Val_Quantity > 0)
            {
                double newPercComplete = (Val_EarnedQty / Val_Quantity) * 100;

                if (Math.Abs(_val_Perc_Complete - newPercComplete) > 0.001)
                {
                    _val_Perc_Complete = Math.Round(newPercComplete, 2);
                    OnPropertyChanged(nameof(Val_Perc_Complete));
                }
            }

            RecalculatePercentEarned();
        }

        /// <summary>
        /// Bidirectional: Update Val_EarnedQty when Val_Perc_Complete changes
        /// Formula: Val_EarnedQty = (Val_Perc_Complete / 100) * Val_Quantity
        /// </summary>
        private void UpdateEarnedQtyFromPercComplete()
        {
            if (Val_Quantity > 0)
            {
                double newEarnedQty = (Val_Perc_Complete / 100) * Val_Quantity;

                if (Math.Abs(_val_EarnedQty - newEarnedQty) > 0.001)
                {
                    _val_EarnedQty = Math.Round(newEarnedQty, 4);
                    OnPropertyChanged(nameof(Val_EarnedQty));
                }
            }

            RecalculatePercentEarned();
        }

        /// <summary>
        /// Calculate Val_Percent_Earned
        /// Formula: MAX(Val_EarnedQty/Val_Quantity, Val_Perc_Complete/100)
        /// </summary>
        private void RecalculatePercentEarned()
        {
            double fromQty = Val_Quantity > 0 ? Val_EarnedQty / Val_Quantity : 0;
            double fromPerc = Val_Perc_Complete / 100;

            Val_Percent_Earned = Math.Max(fromQty, fromPerc);
            Val_Earn_Qty = Val_Percent_Earned;

            RecalculateEarnedHours();
            RecalculateClientEarnedQty();
        }

        /// <summary>
        /// Calculate Val_EarnedHours_Ind
        /// Formula: IF(Val_Percent_Earned >= 1, Val_BudgetedHours_Ind, ROUND(Val_Percent_Earned * Val_BudgetedHours_Ind, 3))
        /// </summary>
        private void RecalculateEarnedHours()
        {
            if (Val_Percent_Earned >= 1.0)
            {
                Val_EarnedHours_Ind = Val_BudgetedHours_Ind;
            }
            else
            {
                Val_EarnedHours_Ind = Math.Round(Val_Percent_Earned * Val_BudgetedHours_Ind, 3);
            }
        }

        /// <summary>
        /// Calculate LookUP_ROC_ID
        /// Formula: Tag_ProjectID & "|" & Catg_ComponentType & "|" & Catg_PhaseCategory & "|" & Catg_ROC_Step
        /// </summary>
        private void RecalculateLookupROCID()
        {
            LookUP_ROC_ID = $"{Tag_ProjectID ?? ""}|{Catg_ComponentType ?? ""}|{Catg_PhaseCategory ?? ""}|{Catg_ROC_Step ?? ""}";
        }

        /// <summary>
        /// Calculate VAL_Client_Earned_EQ_QTY
        /// Formula: IF(Val_EarnedHours_Ind > 0, ROUND((Val_EarnedHours_Ind / Val_BudgetedHours_Ind) * Val_Client_EQ_QTY_BDG, 3), 0)
        /// </summary>
        private void RecalculateClientEarnedQty()
        {
            if (Val_EarnedHours_Ind > 0 && Val_BudgetedHours_Ind > 0)
            {
                VAL_Client_Earned_EQ_QTY = Math.Round(
                    (Val_EarnedHours_Ind / Val_BudgetedHours_Ind) * Val_Client_EQ_QTY_BDG,
                    3
                );
            }
            else
            {
                VAL_Client_Earned_EQ_QTY = 0;
            }
        }

        // ========================================
        // INOTIFYPROPERTYCHANGED IMPLEMENTATION
        // ========================================

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}