using System;
using System.ComponentModel;

namespace VANTAGE.Models
{
    public class Activity : INotifyPropertyChanged
    {
        // ========================================
        // PRIVATE BACKING FIELDS
        // ========================================

        private int _activityID;
        private int _hexNO;

        // Categories
        private string _catg_ComponentType;
        private string _catg_PhaseCategory;
        private string _catg_ROC_Step;

        // Drawings
        private string _dwg_PrimeDrawingNO;
        private string _dwg_RevisionNo;
        private string _dwg_SecondaryDrawingNO;
        private string _dwg_ShtNo;

        // Notes
        private string _notes_Comments;

        // Schedule
        private string _sch_Actno;
        private string _sch_Start;
        private string _sch_Finish;
        private string _sch_Status;

        // Tags
        private string _tag_Aux1;
        private string _tag_Aux2;
        private string _tag_Aux3;
        private string _tag_Area;
        private string _tag_CONo;
        private string _tag_Descriptions;
        private string _tag_EqmtNo;
        private string _tag_Estimator;
        private string _tag_Insulation_Typ;
        private string _tag_LineNo;
        private string _tag_Matl_Spec;
        private string _tag_Phase_Code;
        private string _tag_Paint_Code;
        private string _tag_Pipe_Grade;
        private string _tag_ProjectID;
        private string _tag_RFINo;
        private string _tag_Sch_ActNo;
        private string _tag_Service;
        private string _tag_ShopField;
        private string _tag_SubArea;
        private string _tag_System;
        private string _tag_SystemNo;
        private string _tag_TagNo;
        private string _tag_Tracing;
        private string _tag_WorkPackage;
        private double _tag_XRAY;

        // Trigger
        private int _trg_DateTrigger;

        // UDF Fields
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
        private string _udfTwelve;      // LastModifiedBy
        private string _udfThirteen;    // CreatedBy
        private string _udfFourteen;
        private string _udfFifteen;
        private string _udfSixteen;
        private string _udfSeventeen;
        private string _udfEighteen;
        private string _udfNineteen;    // Unique Activity ID (READ ONLY)
        private string _udfTwenty;

        // Values (user-editable)
        private double _val_Base_Unit;
        private double _val_BudgetedHours_Ind;
        private double _val_BudgetedHours_Group;
        private double _val_BudgetedHours_ROC;
        private int _val_EarnedHours_ROC;
        private double _val_EarnedQty;
        private double _val_Perc_Complete;
        private double _val_Quantity;
        private string _val_UOM;

        // Values (calculated - will be computed)
        private double _val_EarnedHours_Ind;
        private double _val_Earn_Qty;
        private double _val_Percent_Earned;

        // Equipment Quantity
        private double _val_EQ_QTY;
        private string _val_EQ_UOM;

        // ROC
        private int _tag_ROC_ID;
        private string _lookUP_ROC_ID;
        private double _val_ROC_Perc;
        private double _val_ROC_BudgetQty;

        // Pipe
        private double _val_Pipe_Size1;
        private double _val_Pipe_Size2;

        // Previous values
        private double _val_Prev_Earned_Hours;
        private double _val_Prev_Earned_Qty;

        // Timestamps
        private string _val_TimeStamp;

        // Client values
        private double _val_Client_EQ_QTY_BDG;
        private double _val_UDF_Two;
        private double _val_UDF_Three;
        private double _val_Client_Earned_EQ_QTY;

        // ========================================
        // PUBLIC PROPERTIES
        // ========================================

        public int ActivityID
        {
            get => _activityID;
            set { _activityID = value; OnPropertyChanged(nameof(ActivityID)); }
        }

        public int HexNO
        {
            get => _hexNO;
            set { _hexNO = value; OnPropertyChanged(nameof(HexNO)); }
        }

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

        // === DRAWINGS ===

        public string Dwg_PrimeDrawingNO
        {
            get => _dwg_PrimeDrawingNO;
            set { _dwg_PrimeDrawingNO = value; OnPropertyChanged(nameof(Dwg_PrimeDrawingNO)); }
        }

        public string Dwg_RevisionNo
        {
            get => _dwg_RevisionNo;
            set { _dwg_RevisionNo = value; OnPropertyChanged(nameof(Dwg_RevisionNo)); }
        }

        public string Dwg_SecondaryDrawingNO
        {
            get => _dwg_SecondaryDrawingNO;
            set { _dwg_SecondaryDrawingNO = value; OnPropertyChanged(nameof(Dwg_SecondaryDrawingNO)); }
        }

        public string Dwg_ShtNo
        {
            get => _dwg_ShtNo;
            set { _dwg_ShtNo = value; OnPropertyChanged(nameof(Dwg_ShtNo)); }
        }

        // === NOTES ===

        public string Notes_Comments
        {
            get => _notes_Comments;
            set { _notes_Comments = value; OnPropertyChanged(nameof(Notes_Comments)); }
        }

        // === SCHEDULE ===

        public string Sch_Actno
        {
            get => _sch_Actno;
            set { _sch_Actno = value; OnPropertyChanged(nameof(Sch_Actno)); }
        }

        public string Sch_Start
        {
            get => _sch_Start;
            set { _sch_Start = value; OnPropertyChanged(nameof(Sch_Start)); }
        }

        public string Sch_Finish
        {
            get => _sch_Finish;
            set { _sch_Finish = value; OnPropertyChanged(nameof(Sch_Finish)); }
        }

        public string Sch_Status
        {
            get => _sch_Status;
            set { _sch_Status = value; OnPropertyChanged(nameof(Sch_Status)); }
        }

        // === TAGS ===

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

        public string Tag_CONo
        {
            get => _tag_CONo;
            set { _tag_CONo = value; OnPropertyChanged(nameof(Tag_CONo)); }
        }

        public string Tag_Descriptions
        {
            get => _tag_Descriptions;
            set { _tag_Descriptions = value; OnPropertyChanged(nameof(Tag_Descriptions)); }
        }

        public string Tag_EqmtNo
        {
            get => _tag_EqmtNo;
            set { _tag_EqmtNo = value; OnPropertyChanged(nameof(Tag_EqmtNo)); }
        }

        public string Tag_Estimator
        {
            get => _tag_Estimator;
            set { _tag_Estimator = value; OnPropertyChanged(nameof(Tag_Estimator)); }
        }

        public string Tag_Insulation_Typ
        {
            get => _tag_Insulation_Typ;
            set { _tag_Insulation_Typ = value; OnPropertyChanged(nameof(Tag_Insulation_Typ)); }
        }

        public string Tag_LineNo
        {
            get => _tag_LineNo;
            set { _tag_LineNo = value; OnPropertyChanged(nameof(Tag_LineNo)); }
        }

        public string Tag_Matl_Spec
        {
            get => _tag_Matl_Spec;
            set { _tag_Matl_Spec = value; OnPropertyChanged(nameof(Tag_Matl_Spec)); }
        }

        public string Tag_Phase_Code
        {
            get => _tag_Phase_Code;
            set { _tag_Phase_Code = value; OnPropertyChanged(nameof(Tag_Phase_Code)); }
        }

        public string Tag_Paint_Code
        {
            get => _tag_Paint_Code;
            set { _tag_Paint_Code = value; OnPropertyChanged(nameof(Tag_Paint_Code)); }
        }

        public string Tag_Pipe_Grade
        {
            get => _tag_Pipe_Grade;
            set { _tag_Pipe_Grade = value; OnPropertyChanged(nameof(Tag_Pipe_Grade)); }
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

        public string Tag_RFINo
        {
            get => _tag_RFINo;
            set { _tag_RFINo = value; OnPropertyChanged(nameof(Tag_RFINo)); }
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

        public string Tag_SystemNo
        {
            get => _tag_SystemNo;
            set { _tag_SystemNo = value; OnPropertyChanged(nameof(Tag_SystemNo)); }
        }

        public string Tag_TagNo
        {
            get => _tag_TagNo;
            set { _tag_TagNo = value; OnPropertyChanged(nameof(Tag_TagNo)); }
        }

        public string Tag_Tracing
        {
            get => _tag_Tracing;
            set { _tag_Tracing = value; OnPropertyChanged(nameof(Tag_Tracing)); }
        }

        public string Tag_WorkPackage
        {
            get => _tag_WorkPackage;
            set { _tag_WorkPackage = value; OnPropertyChanged(nameof(Tag_WorkPackage)); }
        }

        public double Tag_XRAY
        {
            get => _tag_XRAY;
            set { _tag_XRAY = value; OnPropertyChanged(nameof(Tag_XRAY)); }
        }

        // === TRIGGER ===

        public int Trg_DateTrigger
        {
            get => _trg_DateTrigger;
            set { _trg_DateTrigger = value; OnPropertyChanged(nameof(Trg_DateTrigger)); }
        }

        // === UDF FIELDS ===

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
            set { _udfEleven = value; OnPropertyChanged(nameof(UDFEleven)); OnPropertyChanged(nameof(AssignedToUsername)); }
        }

        // REPURPOSED: LastModifiedBy
        public string UDFTwelve
        {
            get => _udfTwelve;
            set { _udfTwelve = value; OnPropertyChanged(nameof(UDFTwelve)); OnPropertyChanged(nameof(LastModifiedBy)); }
        }

        // REPURPOSED: CreatedBy
        public string UDFThirteen
        {
            get => _udfThirteen;
            set { _udfThirteen = value; OnPropertyChanged(nameof(UDFThirteen)); OnPropertyChanged(nameof(CreatedBy)); }
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

        // REPURPOSED: Unique Activity ID (READ ONLY)
        public string UDFNineteen
        {
            get => _udfNineteen;
            set { _udfNineteen = value; OnPropertyChanged(nameof(UDFNineteen)); }
        }

        public string UDFTwenty
        {
            get => _udfTwenty;
            set { _udfTwenty = value; OnPropertyChanged(nameof(UDFTwenty)); }
        }

        // === FRIENDLY ALIASES FOR REPURPOSED UDFs ===

        public string AssignedToUsername
        {
            get => UDFEleven;
            set => UDFEleven = value;
        }

        public string LastModifiedBy
        {
            get => UDFTwelve;
            set => UDFTwelve = value;
        }

        public string CreatedBy
        {
            get => UDFThirteen;
            set => UDFThirteen = value;
        }

        // ========================================
        // USER-EDITABLE VALUES (with calculations)
        // ========================================

        public double Val_Base_Unit
        {
            get => _val_Base_Unit;
            set { _val_Base_Unit = value; OnPropertyChanged(nameof(Val_Base_Unit)); }
        }

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

        public double Val_BudgetedHours_Group
        {
            get => _val_BudgetedHours_Group;
            set { _val_BudgetedHours_Group = value; OnPropertyChanged(nameof(Val_BudgetedHours_Group)); }
        }

        public double Val_BudgetedHours_ROC
        {
            get => _val_BudgetedHours_ROC;
            set { _val_BudgetedHours_ROC = value; OnPropertyChanged(nameof(Val_BudgetedHours_ROC)); }
        }

        public int Val_EarnedHours_ROC
        {
            get => _val_EarnedHours_ROC;
            set { _val_EarnedHours_ROC = value; OnPropertyChanged(nameof(Val_EarnedHours_ROC)); }
        }

        // PRIMARY EDITABLE: Val_EarnedQty
        public double Val_EarnedQty
        {
            get => _val_EarnedQty;
            set
            {
                if (_val_EarnedQty != value)
                {
                    _val_EarnedQty = value;
                    OnPropertyChanged(nameof(Val_EarnedQty));

                    // Bidirectional update: Update Val_Perc_Complete
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
                if (_val_Perc_Complete != value)
                {
                    _val_Perc_Complete = value;
                    OnPropertyChanged(nameof(Val_Perc_Complete));

                    // Bidirectional update: Update Val_EarnedQty
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

        // ========================================
        // CALCULATED VALUES (READ-ONLY)
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
                OnPropertyChanged(nameof(Status)); // Status depends on this
            }
        }

        // === EQUIPMENT QUANTITY ===

        public double Val_EQ_QTY
        {
            get => _val_EQ_QTY;
            set { _val_EQ_QTY = value; OnPropertyChanged(nameof(Val_EQ_QTY)); }
        }

        public string Val_EQ_UOM
        {
            get => _val_EQ_UOM;
            set { _val_EQ_UOM = value; OnPropertyChanged(nameof(Val_EQ_UOM)); }
        }

        // === ROC ===

        public int Tag_ROC_ID
        {
            get => _tag_ROC_ID;
            set { _tag_ROC_ID = value; OnPropertyChanged(nameof(Tag_ROC_ID)); }
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

        public double Val_ROC_Perc
        {
            get => _val_ROC_Perc;
            set { _val_ROC_Perc = value; OnPropertyChanged(nameof(Val_ROC_Perc)); }
        }

        public double Val_ROC_BudgetQty
        {
            get => _val_ROC_BudgetQty;
            set { _val_ROC_BudgetQty = value; OnPropertyChanged(nameof(Val_ROC_BudgetQty)); }
        }

        // === PIPE ===

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

        // === PREVIOUS VALUES ===

        public double Val_Prev_Earned_Hours
        {
            get => _val_Prev_Earned_Hours;
            set { _val_Prev_Earned_Hours = value; OnPropertyChanged(nameof(Val_Prev_Earned_Hours)); }
        }

        public double Val_Prev_Earned_Qty
        {
            get => _val_Prev_Earned_Qty;
            set { _val_Prev_Earned_Qty = value; OnPropertyChanged(nameof(Val_Prev_Earned_Qty)); }
        }

        // === TIMESTAMPS ===

        public string Val_TimeStamp
        {
            get => _val_TimeStamp;
            set { _val_TimeStamp = value; OnPropertyChanged(nameof(Val_TimeStamp)); }
        }

        // === CLIENT VALUES ===

        public double Val_Client_EQ_QTY_BDG
        {
            get => _val_Client_EQ_QTY_BDG;
            set
            {
                _val_Client_EQ_QTY_BDG = value;
                OnPropertyChanged(nameof(Val_Client_EQ_QTY_BDG));
                RecalculateClientEarnedQty();
            }
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
        // DERIVED/CALCULATED PROPERTIES
        // ========================================

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

                // Only update if different to avoid infinite loop
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

                // Only update if different to avoid infinite loop
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
            Val_Earn_Qty = Val_Percent_Earned; // They're the same per your simplification

            // Trigger dependent calculations
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
        /// Formula: IF(Val_EarnedHours_Ind > 0, ROUND((Val_EarnedHours_Ind / Val_BudgetedHours_Ind) * VAL_Client_EQ_QTY_BDG, 3), 0)
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