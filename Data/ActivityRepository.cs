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
                    Catg_ComponentType = @Catg_ComponentType,
                    Catg_PhaseCategory = @Catg_PhaseCategory,
                    Catg_ROC_Step = @Catg_ROC_Step,
                    Notes_Comments = @Notes_Comments,
                    Tag_Area = @Tag_Area,
                    Tag_Aux1 = @Tag_Aux1,
                    Tag_Aux2 = @Tag_Aux2,
                    Tag_Aux3 = @Tag_Aux3,
                    Tag_Descriptions = @Tag_Descriptions,
                    Tag_Phase_Code = @Tag_Phase_Code,
                    Tag_ProjectID = @Tag_ProjectID,
                    Tag_Sch_ActNo = @Tag_Sch_ActNo,
                    Tag_Service = @Tag_Service,
                    Tag_ShopField = @Tag_ShopField,
                    Tag_SubArea = @Tag_SubArea,
                    Tag_System = @Tag_System,
                    Tag_TagNo = @Tag_TagNo,
                    Tag_WorkPackage = @Tag_WorkPackage,
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
                    UDFEleven = @UDFEleven,
                    UDFTwelve = @UDFTwelve,
                    UDFFourteen = @UDFFourteen,
                    UDFFifteen = @UDFFifteen,
                    UDFSixteen = @UDFSixteen,
                    UDFSeventeen = @UDFSeventeen,
                    UDFEighteen = @UDFEighteen,
                    UDFTwenty = @UDFTwenty,
                    Val_BudgetedHours_Ind = @Val_BudgetedHours_Ind,
                    Val_EarnedQty = @Val_EarnedQty,
                    Val_Perc_Complete = @Val_Perc_Complete,
                    Val_Quantity = @Val_Quantity,
                    Val_UOM = @Val_UOM,
                    Val_Pipe_Size1 = @Val_Pipe_Size1,
                    Val_Pipe_Size2 = @Val_Pipe_Size2,
                    Val_UDF_Two = @Val_UDF_Two,
                    Val_UDF_Three = @Val_UDF_Three,
                    Val_TimeStamp = @Val_TimeStamp
                WHERE ActivityID = @ActivityID";

                    // Add parameters for all editable fields
                    command.Parameters.AddWithValue("@ActivityID", activity.ActivityID);
                    command.Parameters.AddWithValue("@HexNO", activity.HexNO);
                    command.Parameters.AddWithValue("@Catg_ComponentType", activity.Catg_ComponentType ?? "");
                    command.Parameters.AddWithValue("@Catg_PhaseCategory", activity.Catg_PhaseCategory ?? "");
                    command.Parameters.AddWithValue("@Catg_ROC_Step", activity.Catg_ROC_Step ?? "");
                    command.Parameters.AddWithValue("@Notes_Comments", activity.Notes_Comments ?? "");
                    command.Parameters.AddWithValue("@Tag_Area", activity.Tag_Area ?? "");
                    command.Parameters.AddWithValue("@Tag_Aux1", activity.Tag_Aux1 ?? "");
                    command.Parameters.AddWithValue("@Tag_Aux2", activity.Tag_Aux2 ?? "");
                    command.Parameters.AddWithValue("@Tag_Aux3", activity.Tag_Aux3 ?? "");
                    command.Parameters.AddWithValue("@Tag_Descriptions", activity.Tag_Descriptions ?? "");
                    command.Parameters.AddWithValue("@Tag_Phase_Code", activity.Tag_Phase_Code ?? "");
                    command.Parameters.AddWithValue("@Tag_ProjectID", activity.Tag_ProjectID ?? "");
                    command.Parameters.AddWithValue("@Tag_Sch_ActNo", activity.Tag_Sch_ActNo ?? "");
                    command.Parameters.AddWithValue("@Tag_Service", activity.Tag_Service ?? "");
                    command.Parameters.AddWithValue("@Tag_ShopField", activity.Tag_ShopField ?? "");
                    command.Parameters.AddWithValue("@Tag_SubArea", activity.Tag_SubArea ?? "");
                    command.Parameters.AddWithValue("@Tag_System", activity.Tag_System ?? "");
                    command.Parameters.AddWithValue("@Tag_TagNo", activity.Tag_TagNo ?? "");
                    command.Parameters.AddWithValue("@Tag_WorkPackage", activity.Tag_WorkPackage ?? "");
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
                    command.Parameters.AddWithValue("@UDFEleven", activity.UDFEleven ?? "");
                    command.Parameters.AddWithValue("@UDFTwelve", activity.UDFTwelve ?? "");
                    command.Parameters.AddWithValue("@UDFFourteen", activity.UDFFourteen ?? "");
                    command.Parameters.AddWithValue("@UDFFifteen", activity.UDFFifteen ?? "");
                    command.Parameters.AddWithValue("@UDFSixteen", activity.UDFSixteen ?? "");
                    command.Parameters.AddWithValue("@UDFSeventeen", activity.UDFSeventeen ?? "");
                    command.Parameters.AddWithValue("@UDFEighteen", activity.UDFEighteen ?? "");
                    command.Parameters.AddWithValue("@UDFTwenty", activity.UDFTwenty ?? "");
                    command.Parameters.AddWithValue("@Val_BudgetedHours_Ind", activity.Val_BudgetedHours_Ind);
                    command.Parameters.AddWithValue("@Val_EarnedQty", activity.Val_EarnedQty);
                    command.Parameters.AddWithValue("@Val_Perc_Complete", activity.Val_Perc_Complete);
                    command.Parameters.AddWithValue("@Val_Quantity", activity.Val_Quantity);
                    command.Parameters.AddWithValue("@Val_UOM", activity.Val_UOM ?? "");
                    command.Parameters.AddWithValue("@Val_Pipe_Size1", activity.Val_Pipe_Size1);
                    command.Parameters.AddWithValue("@Val_Pipe_Size2", activity.Val_Pipe_Size2);
                    command.Parameters.AddWithValue("@Val_UDF_Two", activity.Val_UDF_Two);
                    command.Parameters.AddWithValue("@Val_UDF_Three", activity.Val_UDF_Three);
                    command.Parameters.AddWithValue("@Val_TimeStamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

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
                        Catg_ComponentType = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Catg_PhaseCategory = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Catg_ROC_Step = reader.IsDBNull(4) ? "" : reader.GetString(4),

                        // Drawings
                        Dwg_PrimeDrawingNO = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        Dwg_RevisionNo = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        Dwg_SecondaryDrawingNO = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        Dwg_ShtNo = reader.IsDBNull(8) ? "" : reader.GetString(8),

                        // Notes
                        Notes_Comments = reader.IsDBNull(9) ? "" : reader.GetString(9),

                        // Schedule
                        Sch_Actno = reader.IsDBNull(10) ? "" : reader.GetString(10),
                        Sch_Start = reader.IsDBNull(11) ? "" : reader.GetString(11),
                        Sch_Finish = reader.IsDBNull(12) ? "" : reader.GetString(12),
                        Sch_Status = reader.IsDBNull(13) ? "" : reader.GetString(13),

                        // Tags
                        Tag_Aux1 = reader.IsDBNull(14) ? "" : reader.GetString(14),
                        Tag_Aux2 = reader.IsDBNull(15) ? "" : reader.GetString(15),
                        Tag_Aux3 = reader.IsDBNull(16) ? "" : reader.GetString(16),
                        Tag_Area = reader.IsDBNull(17) ? "" : reader.GetString(17),
                        Tag_CONo = reader.IsDBNull(18) ? "" : reader.GetString(18),
                        Tag_Descriptions = reader.IsDBNull(19) ? "" : reader.GetString(19),
                        Tag_EqmtNo = reader.IsDBNull(20) ? "" : reader.GetString(20),
                        Tag_Estimator = reader.IsDBNull(21) ? "" : reader.GetString(21),
                        Tag_Insulation_Typ = reader.IsDBNull(22) ? "" : reader.GetString(22),
                        Tag_LineNo = reader.IsDBNull(23) ? "" : reader.GetString(23),
                        Tag_Matl_Spec = reader.IsDBNull(24) ? "" : reader.GetString(24),
                        Tag_Phase_Code = reader.IsDBNull(25) ? "" : reader.GetString(25),
                        Tag_Paint_Code = reader.IsDBNull(26) ? "" : reader.GetString(26),
                        Tag_Pipe_Grade = reader.IsDBNull(27) ? "" : reader.GetString(27),
                        Tag_ProjectID = reader.IsDBNull(28) ? "" : reader.GetString(28),
                        Tag_RFINo = reader.IsDBNull(29) ? "" : reader.GetString(29),
                        Tag_Sch_ActNo = reader.IsDBNull(30) ? "" : reader.GetString(30),
                        Tag_Service = reader.IsDBNull(31) ? "" : reader.GetString(31),
                        Tag_ShopField = reader.IsDBNull(32) ? "" : reader.GetString(32),
                        Tag_SubArea = reader.IsDBNull(33) ? "" : reader.GetString(33),
                        Tag_System = reader.IsDBNull(34) ? "" : reader.GetString(34),
                        Tag_SystemNo = reader.IsDBNull(35) ? "" : reader.GetString(35),
                        Tag_TagNo = reader.IsDBNull(36) ? "" : reader.GetString(36),
                        Tag_Tracing = reader.IsDBNull(37) ? "" : reader.GetString(37),
                        Tag_WorkPackage = reader.IsDBNull(38) ? "" : reader.GetString(38),
                        Tag_XRAY = reader.IsDBNull(39) ? 0 : reader.GetDouble(39),

                        // Trigger
                        Trg_DateTrigger = reader.IsDBNull(40) ? 0 : reader.GetInt32(40),

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
                        UDFEleven = reader.IsDBNull(51) ? "Unassigned" :
                                    (string.IsNullOrWhiteSpace(reader.GetString(51)) ? "Unassigned" :
                                    (validUsernames.Contains(reader.GetString(51)) ? reader.GetString(51) : "Unassigned")),
                        UDFTwelve = reader.IsDBNull(52) ? "" : reader.GetString(52),
                        UDFThirteen = reader.IsDBNull(53) ? "" : reader.GetString(53),
                        UDFFourteen = reader.IsDBNull(54) ? "" : reader.GetString(54),
                        UDFFifteen = reader.IsDBNull(55) ? "" : reader.GetString(55),
                        UDFSixteen = reader.IsDBNull(56) ? "" : reader.GetString(56),
                        UDFSeventeen = reader.IsDBNull(57) ? "" : reader.GetString(57),
                        UDFEighteen = reader.IsDBNull(58) ? "" : reader.GetString(58),
                        UDFNineteen = reader.IsDBNull(59) ? "" : reader.GetString(59),
                        UDFTwenty = reader.IsDBNull(60) ? "" : reader.GetString(60),

                        // Values
                        Val_Base_Unit = reader.IsDBNull(61) ? 0 : reader.GetDouble(61),
                        Val_BudgetedHours_Ind = reader.IsDBNull(62) ? 0 : reader.GetDouble(62),
                        Val_BudgetedHours_Group = reader.IsDBNull(63) ? 0 : reader.GetDouble(63),
                        Val_BudgetedHours_ROC = reader.IsDBNull(64) ? 0 : reader.GetDouble(64),
                        Val_EarnedHours_ROC = reader.IsDBNull(65) ? 0 : reader.GetInt32(65),
                        Val_EarnedQty = reader.IsDBNull(66) ? 0 : reader.GetDouble(66),
                        Val_Perc_Complete = reader.IsDBNull(67) ? 0 : reader.GetDouble(67),
                        Val_Quantity = reader.IsDBNull(68) ? 0 : reader.GetDouble(68),
                        Val_UOM = reader.IsDBNull(69) ? "" : reader.GetString(69),

                        // Calculated
                        Val_EQ_QTY = reader.IsDBNull(73) ? 0 : reader.GetDouble(73),
                        Val_EQ_UOM = reader.IsDBNull(74) ? "" : reader.GetString(74),

                        // ROC
                        Tag_ROC_ID = reader.IsDBNull(75) ? 0 : reader.GetInt32(75),
                        Val_ROC_Perc = reader.IsDBNull(77) ? 0 : reader.GetDouble(77),
                        Val_ROC_BudgetQty = reader.IsDBNull(78) ? 0 : reader.GetDouble(78),

                        // Pipe
                        Val_Pipe_Size1 = reader.IsDBNull(79) ? 0 : reader.GetDouble(79),
                        Val_Pipe_Size2 = reader.IsDBNull(80) ? 0 : reader.GetDouble(80),

                        // Previous
                        Val_Prev_Earned_Hours = reader.IsDBNull(81) ? 0 : reader.GetDouble(81),
                        Val_Prev_Earned_Qty = reader.IsDBNull(82) ? 0 : reader.GetDouble(82),
                        Val_TimeStamp = reader.IsDBNull(83) ? "" : reader.GetString(83),

                        // Client
                        Val_Client_EQ_QTY_BDG = reader.IsDBNull(84) ? 0 : reader.GetDouble(84),
                        Val_UDF_Two = reader.IsDBNull(85) ? 0 : reader.GetDouble(85),
                        Val_UDF_Three = reader.IsDBNull(86) ? 0 : reader.GetDouble(86)
                    };

                    activities.Add(activity);
                }

                return activities;
            });
        }
    }
}