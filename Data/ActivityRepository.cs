using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VANTAGE.Models;

namespace VANTAGE.Data
{

    /// <summary>
    /// Repository for Activity data access with pagination support
    /// </summary>
    public static class ActivityRepository
    {
        /// <summary>
        /// Update an existing activity in the database
        /// </summary>
        public static async Task<bool> UpdateActivityInDatabase(Activity activity)
        {
            try
            {
                await Task.Run(() =>
                {
                    using var connection = DatabaseSetup.GetConnection();
                    connection.Open();

                    var command = connection.CreateCommand();
                    command.CommandText = @"
                UPDATE Activities SET
                    HexNO = @HexNO,
                    Catg_ComponentType = @CompType,
                    Catg_PhaseCategory = @PhaseCategory,
                    Catg_ROC_Step = @ROCStep,
                    Notes_Comments = @Notes,
                    Tag_Area = @Area,
                    Tag_Aux1 = @Aux1,
                    Tag_Aux2 = @Aux2,
                    Tag_Aux3 = @Aux3,
                    Tag_Descriptions = @Description,
                    Tag_Phase_Code = @PhaseCode,
                    Tag_ProjectID = @ProjectID,
                    Tag_Sch_ActNo = @SchedActNO,
                    Tag_Service = @Service,
                    Tag_ShopField = @ShopField,
                    Tag_SubArea = @SubArea,
                    Tag_System = @System,
                    Tag_TagNo = @TagNO,
                    Tag_WorkPackage = @WorkPackage,
                    UDFOne = @UDFOne,
                    UDFTwo = @UDFTwo,
                    UDFThree = @UDFThree,
                    UDFFour = @UDFFour,
                    UDFFive = @UDFFive,
                    UDFSix = @UDFSix,
                    UDFSeven = @UDFSeven,
                    UDFEight = @UDFEight,
                    UDFNine = @UDFNine,
                    UDFTen = @UDFTen,
                    UDFEleven = @AssignedTo,
                    UDFTwelve = @LastModifiedBy,
                    UDFFourteen = @UDFFourteen,
                    UDFFifteen = @UDFFifteen,
                    UDFSixteen = @UDFSixteen,
                    UDFSeventeen = @UDFSeventeen,
                    UDFEighteen = @UDFEighteen,
                    UDFTwenty = @UDFTwenty,
                    Val_BudgetedHours_Ind = @BudgetMHs,
                    Val_EarnedQty = @EarnQtyEntry,
                    Val_Perc_Complete = @PercentEntry,
                    Val_Quantity = @Quantity,
                    Val_UOM = @UOM,
                    Val_Pipe_Size1 = @PipeSize1,
                    Val_Pipe_Size2 = @PipeSize2,
                    Val_UDF_Two = @ClientBudget,
                    Val_UDF_Three = @ClientCustom3,
                    Val_TimeStamp = @Timestamp
                WHERE ActivityID = @ActivityID";

                    // Add parameters with NEW property names
                    command.Parameters.AddWithValue("@ActivityID", activity.ActivityID);
                    command.Parameters.AddWithValue("@HexNO", activity.HexNO);
                    command.Parameters.AddWithValue("@CompType", activity.CompType ?? "");
                    command.Parameters.AddWithValue("@PhaseCategory", activity.PhaseCategory ?? "");
                    command.Parameters.AddWithValue("@ROCStep", activity.ROCStep ?? "");
                    command.Parameters.AddWithValue("@Notes", activity.Notes ?? "");
                    command.Parameters.AddWithValue("@Area", activity.Area ?? "");
                    command.Parameters.AddWithValue("@Aux1", activity.Aux1 ?? "");
                    command.Parameters.AddWithValue("@Aux2", activity.Aux2 ?? "");
                    command.Parameters.AddWithValue("@Aux3", activity.Aux3 ?? "");
                    command.Parameters.AddWithValue("@Description", activity.Description ?? "");
                    command.Parameters.AddWithValue("@PhaseCode", activity.PhaseCode ?? "");
                    command.Parameters.AddWithValue("@ProjectID", activity.ProjectID ?? "");
                    command.Parameters.AddWithValue("@SchedActNO", activity.SchedActNO ?? "");
                    command.Parameters.AddWithValue("@Service", activity.Service ?? "");
                    command.Parameters.AddWithValue("@ShopField", activity.ShopField ?? "");
                    command.Parameters.AddWithValue("@SubArea", activity.SubArea ?? "");
                    command.Parameters.AddWithValue("@System", activity.System ?? "");
                    command.Parameters.AddWithValue("@TagNO", activity.TagNO ?? "");
                    command.Parameters.AddWithValue("@WorkPackage", activity.WorkPackage ?? "");
                    command.Parameters.AddWithValue("@UDFOne", activity.UDFOne ?? "");
                    command.Parameters.AddWithValue("@UDFTwo", activity.UDFTwo ?? "");
                    command.Parameters.AddWithValue("@UDFThree", activity.UDFThree ?? "");
                    command.Parameters.AddWithValue("@UDFFour", activity.UDFFour ?? "");
                    command.Parameters.AddWithValue("@UDFFive", activity.UDFFive ?? "");
                    command.Parameters.AddWithValue("@UDFSix", activity.UDFSix ?? "");
                    command.Parameters.AddWithValue("@UDFSeven", activity.UDFSeven);
                    command.Parameters.AddWithValue("@UDFEight", activity.UDFEight ?? "");
                    command.Parameters.AddWithValue("@UDFNine", activity.UDFNine ?? "");
                    command.Parameters.AddWithValue("@UDFTen", activity.UDFTen ?? "");
                    command.Parameters.AddWithValue("@AssignedTo", activity.AssignedTo ?? "");
                    command.Parameters.AddWithValue("@LastModifiedBy", activity.LastModifiedBy ?? "");
                    command.Parameters.AddWithValue("@UDFFourteen", activity.UDFFourteen ?? "");
                    command.Parameters.AddWithValue("@UDFFifteen", activity.UDFFifteen ?? "");
                    command.Parameters.AddWithValue("@UDFSixteen", activity.UDFSixteen ?? "");
                    command.Parameters.AddWithValue("@UDFSeventeen", activity.UDFSeventeen ?? "");
                    command.Parameters.AddWithValue("@UDFEighteen", activity.UDFEighteen ?? "");
                    command.Parameters.AddWithValue("@UDFTwenty", activity.UDFTwenty ?? "");
                    command.Parameters.AddWithValue("@BudgetMHs", activity.BudgetMHs);
                    command.Parameters.AddWithValue("@EarnQtyEntry", activity.EarnQtyEntry);
                    command.Parameters.AddWithValue("@PercentEntry", activity.PercentEntry);
                    command.Parameters.AddWithValue("@Quantity", activity.Quantity);
                    command.Parameters.AddWithValue("@UOM", activity.UOM ?? "");
                    command.Parameters.AddWithValue("@PipeSize1", activity.PipeSize1);
                    command.Parameters.AddWithValue("@PipeSize2", activity.PipeSize2);
                    command.Parameters.AddWithValue("@ClientBudget", activity.ClientBudget);
                    command.Parameters.AddWithValue("@ClientCustom3", activity.ClientCustom3);
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    int rowsAffected = command.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine($"✓ Activity {activity.ActivityID} updated in database");

                    return rowsAffected > 0;
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error updating activity: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        /// <summary>
        /// Get list of valid usernames from Users table
        /// </summary>
        private static HashSet<string> GetValidUsernames()
        {
            var validUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT Username FROM Users";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    validUsers.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading valid usernames: {ex.Message}");
            }

            return validUsers;
        }
        /// <summary>
        /// Get total count of activities in database
        /// </summary>
        public static async Task<int> GetTotalCountAsync()
        {
            return await Task.Run(() =>
            {
                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Activities";

                var result = command.ExecuteScalar();
                return Convert.ToInt32(result);
            });
        }

        /// <summary>
        /// Get a page of activities with pagination
        /// </summary>
        /// <param name="pageNumber">Zero-based page number</param>
        /// <param name="pageSize">Number of records per page</param>
        /// <returns>List of activities for the requested page</returns>
        public static async Task<List<Activity>> GetPageAsync(int pageNumber, int pageSize)
        {
            // Get valid usernames for validation
            var validUsernames = GetValidUsernames();

            return await Task.Run(() =>
            {
                var activities = new List<Activity>();

                using var connection = DatabaseSetup.GetConnection();
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        ActivityID, HexNO,
                        Catg_ComponentType, Catg_PhaseCategory, Catg_ROC_Step,
                        Dwg_PrimeDrawingNO, Dwg_RevisionNo, Dwg_SecondaryDrawingNO, Dwg_ShtNo,
                        Notes_Comments,
                        Sch_Actno, Sch_Start, Sch_Finish, Sch_Status,
                        Tag_Aux1, Tag_Aux2, Tag_Aux3, Tag_Area, Tag_CONo, Tag_Descriptions,
                        Tag_EqmtNo, Tag_Estimator, Tag_Insulation_Typ, Tag_LineNo, Tag_Matl_Spec,
                        Tag_Phase_Code, Tag_Paint_Code, Tag_Pipe_Grade, Tag_ProjectID, Tag_RFINo,
                        Tag_Sch_ActNo, Tag_Service, Tag_ShopField, Tag_SubArea, Tag_System,
                        Tag_SystemNo, Tag_TagNo, Tag_Tracing, Tag_WorkPackage, Tag_XRAY,
                        Trg_DateTrigger,
                        UDFOne, UDFTwo, UDFThree, UDFFour, UDFFive, UDFSix, UDFSeven,
                        UDFEight, UDFNine, UDFTen, UDFEleven, UDFTwelve, UDFThirteen,
                        UDFFourteen, UDFFifteen, UDFSixteen, UDFSeventeen, UDFEighteen,
                        UDFNineteen, UDFTwenty,
                        Val_Base_Unit, Val_BudgetedHours_Ind, Val_BudgetedHours_Group,
                        Val_BudgetedHours_ROC, Val_EarnedHours_ROC, Val_EarnedQty,
                        Val_Perc_Complete, Val_Quantity, Val_UOM,
                        Val_EarnedHours_Ind, Val_Earn_Qty, Val_Percent_Earned,
                        Val_EQ_QTY, Val_EQ_UOM,
                        Tag_ROC_ID, LookUP_ROC_ID, Val_ROC_Perc, Val_ROC_BudgetQty,
                        Val_Pipe_Size1, Val_Pipe_Size2,
                        Val_Prev_Earned_Hours, Val_Prev_Earned_Qty, Val_TimeStamp,
                        Val_Client_EQ_QTY_BDG, Val_UDF_Two, Val_UDF_Three, VAL_Client_Earned_EQ_QTY
                    FROM Activities
                    ORDER BY UDFNineteen
                    LIMIT @pageSize OFFSET @offset";

                command.Parameters.AddWithValue("@pageSize", pageSize);
                command.Parameters.AddWithValue("@offset", pageNumber * pageSize);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var activity = new Activity
                    {
                        ActivityID = reader.GetInt32(0),
                        HexNO = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),

                        // Categories
                        CompType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        PhaseCategory = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        ROCStep = reader.IsDBNull(4) ? "" : reader.GetString(4),

                        // Drawings
                        DwgNO = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        RevNO = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        SecondDwgNO = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        ShtNO = reader.IsDBNull(8) ? "" : reader.GetString(8),

                        // Notes
                        Notes = reader.IsDBNull(9) ? "" : reader.GetString(9),

                        // Schedule
                        OldActno = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        Start = reader.IsDBNull(11) ? "" : reader.GetString(11),
                        Finish = reader.IsDBNull(12) ? "" : reader.GetString(12),

                        // Tags
                        Aux1 = reader.IsDBNull(14) ? "" : reader.GetString(14),
                        Aux2 = reader.IsDBNull(15) ? "" : reader.GetString(15),
                        Aux3 = reader.IsDBNull(16) ? "" : reader.GetString(16),
                        Area = reader.IsDBNull(17) ? "" : reader.GetString(17),
                        ChgOrdNO = reader.IsDBNull(18) ? "" : reader.GetString(18),
                        Description = reader.IsDBNull(19) ? "" : reader.GetString(19),
                        EqmtNO = reader.IsDBNull(20) ? "" : reader.GetString(20),
                        Estimator = reader.IsDBNull(21) ? "" : reader.GetString(21),
                        InsulType = reader.IsDBNull(22) ? "" : reader.GetString(22),
                        LineNO = reader.IsDBNull(23) ? "" : reader.GetString(23),
                        MtrlSpec = reader.IsDBNull(24) ? "" : reader.GetString(24),
                        PhaseCode = reader.IsDBNull(25) ? "" : reader.GetString(25),
                        PaintCode = reader.IsDBNull(26) ? "" : reader.GetString(26),
                        PipeGrade = reader.IsDBNull(27) ? "" : reader.GetString(27),
                        ProjectID = reader.IsDBNull(28) ? "" : reader.GetString(28),
                        RFINO = reader.IsDBNull(29) ? "" : reader.GetString(29),
                        SchedActNO = reader.IsDBNull(30) ? "" : reader.GetString(30),
                        Service = reader.IsDBNull(31) ? "" : reader.GetString(31),
                        ShopField = reader.IsDBNull(32) ? "" : reader.GetString(32),
                        SubArea = reader.IsDBNull(33) ? "" : reader.GetString(33),
                        System = reader.IsDBNull(34) ? "" : reader.GetString(34),
                        SystemNO = reader.IsDBNull(35) ? "" : reader.GetString(35),
                        TagNO = reader.IsDBNull(36) ? "" : reader.GetString(36),
                        HtTrace = reader.IsDBNull(37) ? "" : reader.GetString(37),
                        WorkPackage = reader.IsDBNull(38) ? "" : reader.GetString(38),
                        XRay = reader.IsDBNull(39) ? 0 : reader.GetDouble(39),

                        // Trigger
                        DateTrigger = reader.IsDBNull(40) ? 0 : reader.GetInt32(40),

                        // UDFs
                        UDFOne = reader.IsDBNull(41) ? "" : reader.GetString(41),
                        UDFTwo = reader.IsDBNull(42) ? "" : reader.GetString(42),
                        UDFThree = reader.IsDBNull(43) ? "" : reader.GetString(43),
                        UDFFour = reader.IsDBNull(44) ? "" : reader.GetString(44),
                        UDFFive = reader.IsDBNull(45) ? "" : reader.GetString(45),
                        UDFSix = reader.IsDBNull(46) ? "" : reader.GetString(46),
                        UDFSeven = reader.IsDBNull(47) ? 0 : reader.GetInt32(47),
                        UDFEight = reader.IsDBNull(48) ? "" : reader.GetString(48),
                        UDFNine = reader.IsDBNull(49) ? "" : reader.GetString(49),
                        UDFTen = reader.IsDBNull(50) ? "" : reader.GetString(50),
                        AssignedTo = reader.IsDBNull(51) ? "Unassigned" :
                                    (string.IsNullOrWhiteSpace(reader.GetString(51)) ? "Unassigned" :
                                    (validUsernames.Contains(reader.GetString(51)) ? reader.GetString(51) : "Unassigned")),
                        LastModifiedBy = reader.IsDBNull(52) ? "" : reader.GetString(52),
                        CreatedBy = reader.IsDBNull(53) ? "" : reader.GetString(53),
                        UDFFourteen = reader.IsDBNull(54) ? "" : reader.GetString(54),
                        UDFFifteen = reader.IsDBNull(55) ? "" : reader.GetString(55),
                        UDFSixteen = reader.IsDBNull(56) ? "" : reader.GetString(56),
                        UDFSeventeen = reader.IsDBNull(57) ? "" : reader.GetString(57),
                        UDFEighteen = reader.IsDBNull(58) ? "" : reader.GetString(58),
                        UniqueID = reader.IsDBNull(59) ? "" : reader.GetString(59),
                        UDFTwenty = reader.IsDBNull(60) ? "" : reader.GetString(60),

                        // Values
                        BaseUnit = reader.IsDBNull(61) ? 0 : reader.GetDouble(61),
                        BudgetMHs = reader.IsDBNull(62) ? 0 : reader.GetDouble(62),
                        BudgetHoursGroup = reader.IsDBNull(63) ? 0 : reader.GetDouble(63),
                        BudgetHoursROC = reader.IsDBNull(64) ? 0 : reader.GetDouble(64),
                        EarnedMHsRoc = reader.IsDBNull(65) ? 0 : reader.GetInt32(65),
                        EarnQtyEntry = reader.IsDBNull(66) ? 0 : reader.GetDouble(66),
                        PercentEntry = reader.IsDBNull(67) ? 0 : reader.GetDouble(67),
                        Quantity = reader.IsDBNull(68) ? 0 : reader.GetDouble(68),
                        UOM = reader.IsDBNull(69) ? "" : reader.GetString(69),

                        // Equipment
                        EquivQTY = reader.IsDBNull(73) ? 0 : reader.GetDouble(73),
                        EquivUOM = reader.IsDBNull(74) ? "" : reader.GetString(74),

                        // ROC
                        ROCID = reader.IsDBNull(75) ? 0 : reader.GetInt32(75),
                        ROCPercent = reader.IsDBNull(77) ? 0 : reader.GetDouble(77),
                        ROCBudgetQTY = reader.IsDBNull(78) ? 0 : reader.GetDouble(78),

                        // Pipe
                        PipeSize1 = reader.IsDBNull(79) ? 0 : reader.GetDouble(79),
                        PipeSize2 = reader.IsDBNull(80) ? 0 : reader.GetDouble(80),

                        // Previous
                        PrevEarnMHs = reader.IsDBNull(81) ? 0 : reader.GetDouble(81),
                        PrevEarnQTY = reader.IsDBNull(82) ? 0 : reader.GetDouble(82),
                        Timestamp = reader.IsDBNull(83) ? "" : reader.GetString(83),

                        // Client
                        ClientEquivQty = reader.IsDBNull(84) ? 0 : reader.GetDouble(84),
                        ClientBudget = reader.IsDBNull(85) ? 0 : reader.GetDouble(85),
                        ClientCustom3 = reader.IsDBNull(86) ? 0 : reader.GetDouble(86)
                    };

                    activities.Add(activity);
                }

                return activities;
            });
        }
    }
}