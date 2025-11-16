using System.Reflection;
using Syncfusion.SfSkinManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace Syncfusion.Themes.FluentDark.WPF
{
    /// <exclude/>
    public class FluentDarkSkinHelper : SkinHelper
    {
        [Obsolete("GetDictonaries is deprecated, please use GetDictionaries instead.")]
        public override List<string> GetDictonaries(string type, string style)
        {
            return GetDictionaries(type, style);
        }

        #region Fluent Settings
        public override void SetFluentSettings(DependencyObject obj, HoverEffect hoverEffectMode, PressedEffect pressedEffectMode)
        {
            base.SetFluentSettings(obj, hoverEffectMode, pressedEffectMode);
            FluentHelper.SetHoverEffectMode(obj, hoverEffectMode);
            FluentHelper.SetPressedEffectMode(obj, pressedEffectMode);
        }
        #endregion

        public override List<string> GetDictionaries(String type, string style)
        {
            string rootStylePath = "/Syncfusion.Themes.FluentDark.WPF;component/";
            List<string> styles = new List<string>();
            # region Switch

			switch (type)
			{
				case "Syncfusion.UI.Xaml.Chat.TypingIndicator":
					styles.Add(rootStylePath + "AssistView/AssistView.xaml");
					break;
				case "Syncfusion.UI.Xaml.Chat.SfAIAssistView":
					styles.Add(rootStylePath + "AssistView/AssistView.xaml");
					break;
				case "Syncfusion.UI.Xaml.Chat.ChatItem":
					styles.Add(rootStylePath + "AssistView/AssistView.xaml");
					break;
				case "Syncfusion.UI.Xaml.Chat.SuggestionsViewer":
					styles.Add(rootStylePath + "AssistView/AssistView.xaml");
					break;
				case "Syncfusion.UI.Xaml.Chat.RichTextBox":
					styles.Add(rootStylePath + "AssistView/RichTextBox.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.SfScheduler":
					styles.Add(rootStylePath + "SfScheduler/SfScheduler.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.ResourceHeaderControl":
					styles.Add(rootStylePath + "SfScheduler/SfScheduler.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.RecurrenceEditorControl":
					styles.Add(rootStylePath + "SfScheduler/AppointmentEditorWindow.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.AppointmentEditorControl":
					styles.Add(rootStylePath + "SfScheduler/AppointmentEditorWindow.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.SchedulerReminderAlertWindow":
					styles.Add(rootStylePath + "SfScheduler/AppointmentEditorWindow.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.ReminderNotificationControl":
					styles.Add(rootStylePath + "SfScheduler/AppointmentEditorWindow.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.MonthAgendaView":
					styles.Add(rootStylePath + "SfScheduler/AgendaViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.AllDayAppointmentViewControl":
					styles.Add(rootStylePath + "SfScheduler/AllDayAppointmentPanelStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.AppointmentControl":
					styles.Add(rootStylePath + "SfScheduler/AppointmentStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.AppointmentsCountControl":
					styles.Add(rootStylePath + "SfScheduler/AppointmentStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.MonthViewControl":
					styles.Add(rootStylePath + "SfScheduler/MonthViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.MonthCell":
					styles.Add(rootStylePath + "SfScheduler/MonthViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.WeekNumberCell":
					styles.Add(rootStylePath + "SfScheduler/MonthViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.SchedulerHeaderControl":
					styles.Add(rootStylePath + "SfScheduler/SchedulerHeaderStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.TimelineViewControl":
					styles.Add(rootStylePath + "SfScheduler/TimeSlotViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.DayViewControl":
					styles.Add(rootStylePath + "SfScheduler/TimeSlotViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.TimeSlotCell":
					styles.Add(rootStylePath + "SfScheduler/TimeSlotViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.TimeRulerCell":
					styles.Add(rootStylePath + "SfScheduler/TimeSlotViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.SpecialTimeRegionControl":
					styles.Add(rootStylePath + "SfScheduler/TimeSlotViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.TimeIndicatorControl":
					styles.Add(rootStylePath + "SfScheduler/TimeSlotViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.TimeIndicator":
					styles.Add(rootStylePath + "SfScheduler/TimeSlotViewStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.ViewHeaderControl":
					styles.Add(rootStylePath + "SfScheduler/ViewHeaderStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.ViewHeaderBase":
					styles.Add(rootStylePath + "SfScheduler/ViewHeaderStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.DayViewHeader":
					styles.Add(rootStylePath + "SfScheduler/ViewHeaderStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.TimelineViewHeader":
					styles.Add(rootStylePath + "SfScheduler/ViewHeaderStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Scheduler.MonthViewHeader":
					styles.Add(rootStylePath + "SfScheduler/ViewHeaderStyle.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.SfDataGrid":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridHeaderCellControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.FilterToggleButton":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridStackedHeaderCellControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridRowHeaderCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridHeaderIndentCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridRowHeaderIndentCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.VirtualizingCellsControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.HeaderRowControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.UnBoundRowControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.RowFilter.FilterRowControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.TableSummaryRowControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridUnBoundRowCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.CaptionSummaryRowControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridIndentCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GroupSummaryRowControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridGroupSummaryCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridCaptionSummaryCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridTableSummaryCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.AddNewRowControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.RowFilter.GridFilterRowCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GroupDropArea":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.PopupContentControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GroupDropAreaItem":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridExpanderCellControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.UpIndicatorContentControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.DownIndicatorContentControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.DetailsViewDataGrid":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridDetailsViewExpanderCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.DetailsViewContentPresenter":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridDetailsViewIndentCell":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.CheckboxFilterControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridFilterControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.AdvancedFilterControl":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.ColumnChooserItem":
					styles.Add(rootStylePath + "SfDataGrid/SfDataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.SfTreeGrid":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridHeaderCell":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridHeaderRowControl":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridStackedHeaderCell":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridRowControl":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridCell":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridExpanderCell":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridRowHeaderCell":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridRowHeaderIndentCell":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.TreeGridExpander":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.Filtering.TreeGridCheckBoxFilterControl":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.Filtering.TreeGridFilterControl":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeGrid.Filtering.TreeGridAdvancedFilterControl":
					styles.Add(rootStylePath + "SfTreeGrid/SfTreeGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.PrintOptionsControl":
					styles.Add(rootStylePath + "GridPrintPreviewControl/GridPrintPreviewControl.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.GridPrintPreviewControl":
					styles.Add(rootStylePath + "GridPrintPreviewControl/GridPrintPreviewControl.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.PrintPageControl":
					styles.Add(rootStylePath + "GridPrintPreviewControl/GridPrintPreviewControl.xaml");
					break;
				case "Syncfusion.UI.Xaml.Controls.DataPager.NumericButton":
					styles.Add(rootStylePath + "SfDataPager/SfDataPager.xaml");
					break;
				case "Syncfusion.UI.Xaml.Controls.DataPager.SfDataPager":
					styles.Add(rootStylePath + "SfDataPager/SfDataPager.xaml");
					break;
				case "Syncfusion.Windows.Controls.PivotGrid.PivotExpanderCell":
					styles.Add(rootStylePath + "PivotGridControl/PivotGridControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.PivotGrid.PivotSortHeaderCell":
					styles.Add(rootStylePath + "PivotGridControl/PivotGridControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.PivotGrid.PivotGridHyperlinkCell":
					styles.Add(rootStylePath + "PivotGridControl/PivotGridControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.PivotGrid.PivotGridRowGroupBar":
					styles.Add(rootStylePath + "PivotGridControl/PivotGridControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.PivotGrid.PivotGridGroupingBar":
					styles.Add(rootStylePath + "PivotGridControl/PivotGridControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.PivotGrid.PivotGridControl":
					styles.Add(rootStylePath + "PivotGridControl/PivotGridControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.PivotGrid.AnimatedGrid":
					styles.Add(rootStylePath + "PivotGridControl/PivotGridControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.PivotSchemaDesigner.PivotSchemaDesigner":
					styles.Add(rootStylePath + "PivotGridControl/PivotSchemaDesigner.xaml");
					break;
				case "Syncfusion.Windows.Shared.SfAvatarView":
					styles.Add(rootStylePath + "AvatarView/AvatarView.xaml");
					break;
				case "Syncfusion.Windows.Shared.DoubleTextBox":
					styles.Add(rootStylePath + "DoubleTextBox/DoubleTextBox.xaml");
					break;
				case "Syncfusion.Windows.Shared.IntegerTextBox":
					styles.Add(rootStylePath + "IntegerTextBox/IntegerTextBox.xaml");
					break;
				case "Syncfusion.Windows.Shared.PercentTextBox":
					styles.Add(rootStylePath + "PercentTextBox/PercentTextBox.xaml");
					break;
				case "Syncfusion.Windows.Shared.CurrencyTextBox":
					styles.Add(rootStylePath + "CurrencyTextBox/CurrencyTextBox.xaml");
					break;
				case "Syncfusion.Windows.Shared.MaskedTextBox":
					styles.Add(rootStylePath + "MaskedTextBox/MaskedTextBox.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfMaskedEdit":
					styles.Add(rootStylePath + "SfMaskedEdit/SfMaskedEdit.xaml");
					break;
				case "Syncfusion.Windows.Controls.Notification.SfBadge":
					styles.Add(rootStylePath + "SfBadge/SfBadge.xaml");
					break;
				case "Syncfusion.Windows.Shared.UpDown":
					styles.Add(rootStylePath + "UpDown/UpDown.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfDomainUpDown":
					styles.Add(rootStylePath + "SfDomainUpDown/SfDomainUpDown.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.TickBar":
					styles.Add(rootStylePath + "SfRangeSlider/SfRangeSlider.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.TickBarItem":
					styles.Add(rootStylePath + "SfRangeSlider/SfRangeSlider.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfRangeSlider":
					styles.Add(rootStylePath + "SfRangeSlider/SfRangeSlider.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfTextBoxExt":
					styles.Add(rootStylePath + "SfTextBoxExt/SfTextBoxExt.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.TokenItem":
					styles.Add(rootStylePath + "SfTextBoxExt/SfTextBoxExt.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfRating":
					styles.Add(rootStylePath + "SfRating/SfRating.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfRatingItem":
					styles.Add(rootStylePath + "SfRating/SfRating.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.CheckListBox":
					styles.Add(rootStylePath + "CheckListBox/CheckListBox.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.CheckListBoxItem":
					styles.Add(rootStylePath + "CheckListBox/CheckListBox.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.CheckListSelectAllItem":
					styles.Add(rootStylePath + "CheckListBox/CheckListBox.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.CheckListGroupItem":
					styles.Add(rootStylePath + "CheckListBox/CheckListBox.xaml");
					break;
				case "Syncfusion.UI.Xaml.Grid.SfMultiColumnDropDownControl":
					styles.Add(rootStylePath + "SfMultiColumnDropDownControl/SfMultiColumnDropDownControl.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ComboBoxAdv":
					styles.Add(rootStylePath + "ComboBoxAdv/ComboBoxAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ComboBoxItemAdv":
					styles.Add(rootStylePath + "ComboBoxAdv/ComboBoxAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.BottomThumb":
					styles.Add(rootStylePath + "FontListComboBox/FontListComboBox.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.FontListComboBox":
					styles.Add(rootStylePath + "FontListComboBox/FontListComboBox.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.FontListBox":
					styles.Add(rootStylePath + "FontListBox/FontListBox.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.FontListBoxInternalItem":
					styles.Add(rootStylePath + "FontListBox/FontListBox.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GroupHeader":
					styles.Add(rootStylePath + "FontListBox/FontListBox.xaml");
					break;
				case "Syncfusion.Windows.Shared.PinnableListBox":
					styles.Add(rootStylePath + "PinnableListBox/PinnableListBox.xaml");
					break;
				case "Syncfusion.Windows.Shared.PinnableListBoxItem":
					styles.Add(rootStylePath + "PinnableListBox/PinnableListBox.xaml");
					break;
				case "Syncfusion.UI.Xaml.ProgressBar.SfCircularProgressBar":
					styles.Add(rootStylePath + "SfCircularProgressBar/SfCircularProgressBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.ProgressBar.SfLinearProgressBar":
					styles.Add(rootStylePath + "SfLinearProgressBar/SfLinearProgressBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.ProgressBar.StepViewItem":
					styles.Add(rootStylePath + "SfStepProgressBar/SfStepProgressBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.ProgressBar.SfStepProgressBar":
					styles.Add(rootStylePath + "SfStepProgressBar/SfStepProgressBar.xaml");
					break;
				case "Syncfusion.Windows.Shared.WeekNumberCellPanel":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.DayCell":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.MonthCell":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.YearCell":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.YearRangeCell":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.DayNameCell":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.DayNamesGrid":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.WeekNumberCell":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.WeekNumbersGrid":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.DayGrid":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.MonthGrid":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.YearGrid":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.YearRangeGrid":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.NavigateButtonBase":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.NavigateButton":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.MonthButton":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.CalendarEdit":
					styles.Add(rootStylePath + "CalendarEdit/CalendarEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.TimeSpanEdit":
					styles.Add(rootStylePath + "TimeSpanEdit/TimeSpanEdit.xaml");
					break;
				case "Syncfusion.Windows.Shared.DateTimeEdit":
					styles.Add(rootStylePath + "DateTimeEdit/DateTimeEdit.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.AutoComplete":
					styles.Add(rootStylePath + "AutoComplete/AutoComplete.xaml");
					break;
				case "Syncfusion.Windows.Shared.Clock":
					styles.Add(rootStylePath + "Clock/Clock.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.CalculatorButton":
					styles.Add(rootStylePath + "SfCalculator/SfCalculator.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfCalculator":
					styles.Add(rootStylePath + "SfCalculator/SfCalculator.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.InputPane":
					styles.Add(rootStylePath + "SfCalculator/SfCalculator.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.DisplayPane":
					styles.Add(rootStylePath + "SfCalculator/SfCalculator.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.RadialLabel":
					styles.Add(rootStylePath + "SfRadialSlider/SfRadialSlider.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.RadialTick":
					styles.Add(rootStylePath + "SfRadialSlider/SfRadialSlider.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.SfRadialSlider":
					styles.Add(rootStylePath + "SfRadialSlider/SfRadialSlider.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.BusyIndicator":
					styles.Add(rootStylePath + "BusyIndicator/BusyIndicator.xaml");
					break;
				case "Syncfusion.Windows.Controls.Notification.SfHubTile":
					styles.Add(rootStylePath + "SfHubTile/SfHubTile.xaml");
					break;
				case "Syncfusion.Windows.Controls.Notification.SfPulsingTile":
					styles.Add(rootStylePath + "SfPulsingTile/SfPulsingTile.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ButtonAdv":
					styles.Add(rootStylePath + "ButtonAdv/ButtonAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.DropDownButtonAdv":
					styles.Add(rootStylePath + "DropDownButtonAdv/DropDownButtonAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.DropDownMenuGroup":
					styles.Add(rootStylePath + "DropDownButtonAdv/DropDownButtonAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.DropDownMenuItem":
					styles.Add(rootStylePath + "DropDownButtonAdv/DropDownButtonAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.SplitButtonAdv":
					styles.Add(rootStylePath + "SplitButtonAdv/SplitButtonAdv.xaml");
					break;
				case "Syncfusion.Windows.Shared.ColorEdit":
					styles.Add(rootStylePath + "ColorEdit/ColorEdit.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ColorGroupItem":
					styles.Add(rootStylePath + "ColorPickerPalette/ColorPickerPalette.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ColorGroup":
					styles.Add(rootStylePath + "ColorPickerPalette/ColorPickerPalette.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.PolygonItem":
					styles.Add(rootStylePath + "ColorPickerPalette/ColorPickerPalette.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ColorPickerPalette":
					styles.Add(rootStylePath + "ColorPickerPalette/ColorPickerPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.ColorSwatches":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.SfColorPalette":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.ColorPaletteButton":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Office":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Waveform":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Urban":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Solstice":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Pushpin":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Paper":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Module":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Metro":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Hardcover":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.Apex":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Controls.Media.ColorItem":
					styles.Add(rootStylePath + "SfColorPalette/SfColorPalette.xaml");
					break;
				case "Syncfusion.Windows.Shared.ColorPicker":
					styles.Add(rootStylePath + "ColorPicker/ColorPicker.xaml");
					break;
				case "Syncfusion.Windows.PropertyGrid.PropertyViewItem":
					styles.Add(rootStylePath + "PropertyGrid/PropertyGrid.xaml");
					break;
				case "Syncfusion.Windows.PropertyGrid.PropertyCatagoryViewItem":
					styles.Add(rootStylePath + "PropertyGrid/PropertyGrid.xaml");
					break;
				case "Syncfusion.Windows.PropertyGrid.PropertyView":
					styles.Add(rootStylePath + "PropertyGrid/PropertyGrid.xaml");
					break;
				case "Syncfusion.Windows.PropertyGrid.PropertyGrid":
					styles.Add(rootStylePath + "PropertyGrid/PropertyGrid.xaml");
					break;
				case "Syncfusion.Windows.PropertyGrid.ItemsSourceControl":
					styles.Add(rootStylePath + "PropertyGrid/PropertyGrid.xaml");
					break;
				case "Syncfusion.Windows.PropertyGrid.DefaultMenuButton":
					styles.Add(rootStylePath + "PropertyGrid/PropertyGrid.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfDateSelector":
					styles.Add(rootStylePath + "SfDateSelector/SfDateSelector.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfDatePicker":
					styles.Add(rootStylePath + "SfDatePicker/SfDatePicker.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfTimeSelector":
					styles.Add(rootStylePath + "SfTimeSelector/SfTimeSelector.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfTimePicker":
					styles.Add(rootStylePath + "SfTimePicker/SfTimePicker.xaml");
					break;
				case "Syncfusion.Windows.Controls.SpellCheckerDialog":
					styles.Add(rootStylePath + "SpellChecker/SpellChecker.xaml");
					break;
				case "Syncfusion.Windows.Shared.TitleBar":
					styles.Add(rootStylePath + "ChromelessWindow/ChromelessWindow.xaml");
					break;
				case "Syncfusion.Windows.Shared.TitleButton":
					styles.Add(rootStylePath + "ChromelessWindow/ChromelessWindow.xaml");
					break;
				case "Syncfusion.Windows.Shared.ChromelessWindow":
					styles.Add(rootStylePath + "ChromelessWindow/ChromelessWindow.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GalleryItem":
					styles.Add(rootStylePath + "Gallery/Gallery.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GalleryGroup":
					styles.Add(rootStylePath + "Gallery/Gallery.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.Gallery":
					styles.Add(rootStylePath + "Gallery/Gallery.xaml");
					break;
				case "Syncfusion.Windows.Controls.Input.SfGridSplitter":
					styles.Add(rootStylePath + "SfGridSplitter/SfGridSplitter.xaml");
					break;
				case "Syncfusion.UI.Xaml.TextInputLayout.SfTextInputLayout":
					styles.Add(rootStylePath + "SfTextInputLayout/SfTextInputLayout.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TabPanelAdv":
					styles.Add(rootStylePath + "TabControlExt/TabControlExt.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TabItemExt":
					styles.Add(rootStylePath + "TabControlExt/TabControlExt.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TabControlExt":
					styles.Add(rootStylePath + "TabControlExt/TabControlExt.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.CardViewItem":
					styles.Add(rootStylePath + "CardView/CardView.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.CardView":
					styles.Add(rootStylePath + "CardView/CardView.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TabSplitterItem":
					styles.Add(rootStylePath + "TabSplitter/TabSplitter.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.SplitterPage":
					styles.Add(rootStylePath + "TabSplitter/TabSplitter.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TabSplitter":
					styles.Add(rootStylePath + "TabSplitter/TabSplitter.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.VS2005SwitchPreviewControl":
					styles.Add(rootStylePath + "DocumentContainer/DocumentContainer.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.VistaFlipSwitchPreviewControl":
					styles.Add(rootStylePath + "DocumentContainer/DocumentContainer.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ItemWindow":
					styles.Add(rootStylePath + "DocumentContainer/DocumentContainer.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.QuickTabSwicthPreviewControl":
					styles.Add(rootStylePath + "DocumentContainer/DocumentContainer.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.DocumentHeader":
					styles.Add(rootStylePath + "DocumentContainer/DocumentContainer.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.DocumentContainer":
					styles.Add(rootStylePath + "DocumentContainer/DocumentContainer.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.MDIWindow":
					styles.Add(rootStylePath + "DocumentContainer/DocumentContainer.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ListSwicthPreviewControl":
					styles.Add(rootStylePath + "DocumentContainer/DocumentContainer.xaml");
					break;
				case "Syncfusion.Windows.Shared.TileViewItem":
					styles.Add(rootStylePath + "TileViewControl/TileViewControl.xaml");
					break;
				case "Syncfusion.Windows.Shared.TileViewControl":
					styles.Add(rootStylePath + "TileViewControl/TileViewControl.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.DropDownButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.SplitButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.PopupResizeThumb":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GalleryFilterSelector":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonGalleryItem":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonMenuGroup":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonGalleryGroup":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonGallery":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonButtonChecker":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ButtonPanel":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonMenuItem":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonContextMenu":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonAutomatableTextBlock":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonComboBoxItem":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.LabelTextBlock":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonListBox":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonCheckBox":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonRadioButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonSeparator":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonTextBox":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonItemHost":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonToggleButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonBar":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonPage":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonTab":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TabButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.QuickAccessToolBar":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.BackStageButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ApplicationMenu":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.Ribbon":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ScreenTip":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.SimpleMenuButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.SplitMenuButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonStatusBar":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.RibbonWindow":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.BackStageSeparator":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.BackstageTabItem":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.BackStageCommandButton":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.Backstage":
					styles.Add(rootStylePath + "Ribbon/Ribbon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.QATListBox":
					styles.Add(rootStylePath + "Ribbon/QATCustomizationDialog.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.QATListBoxItem":
					styles.Add(rootStylePath + "Ribbon/QATCustomizationDialog.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.QATTreeViewItem":
					styles.Add(rootStylePath + "Ribbon/QATCustomizationDialog.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.QATTreeView":
					styles.Add(rootStylePath + "Ribbon/QATCustomizationDialog.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.QATCustomizationDialog":
					styles.Add(rootStylePath + "Ribbon/QATCustomizationDialog.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.Splitter":
					styles.Add(rootStylePath + "DockingManager/DockingManager.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.CustomContextMenu":
					styles.Add(rootStylePath + "DockingManager/DockingManager.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.SidePanel":
					styles.Add(rootStylePath + "DockingManager/DockingManager.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.DockHeaderPresenter":
					styles.Add(rootStylePath + "DockingManager/DockingManager.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.HostAdornerVS2005":
					styles.Add(rootStylePath + "DockingManager/DockingManager.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ScrollButtonsBar":
					styles.Add(rootStylePath + "DockingManager/DockingManager.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.NativeFloatWindow":
					styles.Add(rootStylePath + "DockingManager/DockingManager.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.DockingManager":
					styles.Add(rootStylePath + "DockingManager/DockingManager.xaml");
					break;
				case "Syncfusion.Windows.Controls.Notification.SfBusyIndicator":
					styles.Add(rootStylePath + "SfBusyIndicator/SfBusyIndicator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.BalloonTipHeader":
					styles.Add(rootStylePath + "NotifyIcon/NotifyIcon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.BalloonTip":
					styles.Add(rootStylePath + "NotifyIcon/NotifyIcon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.NotifyIcon":
					styles.Add(rootStylePath + "NotifyIcon/NotifyIcon.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.WizardNavigationArea":
					styles.Add(rootStylePath + "WizardControl/WizardControl.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.WizardControl":
					styles.Add(rootStylePath + "WizardControl/WizardControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.SfRadialColorItem":
					styles.Add(rootStylePath + "SfRadialMenu/SfRadialMenu.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.OuterRim":
					styles.Add(rootStylePath + "SfRadialMenu/SfRadialMenu.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.SfRadialMenuItem":
					styles.Add(rootStylePath + "SfRadialMenu/SfRadialMenu.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.SfRadialMenu":
					styles.Add(rootStylePath + "SfRadialMenu/SfRadialMenu.xaml");
					break;
				case "Syncfusion.UI.Xaml.NavigationDrawer.NavigationItem":
					styles.Add(rootStylePath + "SfNavigationDrawer/SfNavigationDrawer.xaml");
					styles.Add(rootStylePath + "SfNavigationDrawer/PrimarySfNavigationDrawer.xaml");
					break;
				case "Syncfusion.UI.Xaml.NavigationDrawer.SfNavigationDrawer":
					styles.Add(rootStylePath + "SfNavigationDrawer/SfNavigationDrawer.xaml");
					styles.Add(rootStylePath + "SfNavigationDrawer/PrimarySfNavigationDrawer.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.HierarchyNavigatorHistoryListBox":
					styles.Add(rootStylePath + "HierarchyNavigator/HierarchyNavigator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.HierarchyNavigatorHistoryControl":
					styles.Add(rootStylePath + "HierarchyNavigator/HierarchyNavigator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.HierarchyNavigatorDropDownItem":
					styles.Add(rootStylePath + "HierarchyNavigator/HierarchyNavigator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.HierarchyNavigatorBarContent":
					styles.Add(rootStylePath + "HierarchyNavigator/HierarchyNavigator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.HierarchyNavigatorItem":
					styles.Add(rootStylePath + "HierarchyNavigator/HierarchyNavigator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.HierarchyNavigatorItemsControl":
					styles.Add(rootStylePath + "HierarchyNavigator/HierarchyNavigator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.HierarchyNavigator":
					styles.Add(rootStylePath + "HierarchyNavigator/HierarchyNavigator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TreeViewAdv":
					styles.Add(rootStylePath + "TreeViewAdv/TreeViewAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TreeViewColumnHeader":
					styles.Add(rootStylePath + "TreeViewAdv/TreeViewAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TreeViewItemAdv":
					styles.Add(rootStylePath + "TreeViewAdv/TreeViewAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TreeViewRowDragMarkerTemplatedAdornerInternalControl":
					styles.Add(rootStylePath + "TreeViewAdv/TreeViewAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TreeViewColumnHeaderTemplatedAdornerInternalControl":
					styles.Add(rootStylePath + "TreeViewAdv/TreeViewAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TabNavigationItem":
					styles.Add(rootStylePath + "TabNavigationControl/TabNavigationControl.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TabNavigationControl":
					styles.Add(rootStylePath + "TabNavigationControl/TabNavigationControl.xaml");
					break;
				case "Syncfusion.Windows.Shared.MenuItemSeparator":
					styles.Add(rootStylePath + "MenuAdv/MenuAdv.xaml");
					break;
				case "Syncfusion.Windows.Shared.MenuAdv":
					styles.Add(rootStylePath + "MenuAdv/MenuAdv.xaml");
					break;
				case "Syncfusion.Windows.Shared.MenuItemAdv":
					styles.Add(rootStylePath + "MenuAdv/MenuAdv.xaml");
					break;
				case "Syncfusion.Windows.Controls.Layout.SfAccordion":
					styles.Add(rootStylePath + "SfAccordion/SfAccordion.xaml");
					break;
				case "Syncfusion.Windows.Controls.Layout.SfAccordionItem":
					styles.Add(rootStylePath + "SfAccordion/SfAccordion.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeView.TreeViewItem":
					styles.Add(rootStylePath + "SfTreeView/SfTreeView.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeView.SfTreeView":
					styles.Add(rootStylePath + "SfTreeView/SfTreeView.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeView.TreeViewDragPreviewControl":
					styles.Add(rootStylePath + "SfTreeView/SfTreeView.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.SfTreeNavigatorItem":
					styles.Add(rootStylePath + "SfTreeNavigator/SfTreeNavigator.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.TreeNavigatorHeaderItem":
					styles.Add(rootStylePath + "SfTreeNavigator/SfTreeNavigator.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.TreeNavigatorItemsHost":
					styles.Add(rootStylePath + "SfTreeNavigator/SfTreeNavigator.xaml");
					break;
				case "Syncfusion.Windows.Controls.Navigation.SfTreeNavigator":
					styles.Add(rootStylePath + "SfTreeNavigator/SfTreeNavigator.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ToggleButtonExt":
					styles.Add(rootStylePath + "TaskBar/TaskBar.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ExpanderExt":
					styles.Add(rootStylePath + "TaskBar/TaskBar.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TaskBarItem":
					styles.Add(rootStylePath + "TaskBar/TaskBar.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.TaskBar":
					styles.Add(rootStylePath + "TaskBar/TaskBar.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ToolBarAdv":
					styles.Add(rootStylePath + "ToolBarAdv/ToolBarAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ToolBarItemSeparator":
					styles.Add(rootStylePath + "ToolBarAdv/ToolBarAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.FloatingToolBar":
					styles.Add(rootStylePath + "ToolBarAdv/ToolBarAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ToolBarManager":
					styles.Add(rootStylePath + "ToolBarAdv/ToolBarAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.ToolBarTrayAdv":
					styles.Add(rootStylePath + "ToolBarAdv/ToolBarAdv.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GroupBar":
					styles.Add(rootStylePath + "GroupBar/GroupBar.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GroupBarItem":
					styles.Add(rootStylePath + "GroupBar/GroupBar.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GroupBarItemHeader":
					styles.Add(rootStylePath + "GroupBar/GroupBar.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GroupView":
					styles.Add(rootStylePath + "GroupBar/GroupBar.xaml");
					break;
				case "Syncfusion.Windows.Tools.Controls.GroupViewItem":
					styles.Add(rootStylePath + "GroupBar/GroupBar.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.BookmarkLabel":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.BookmarkPane":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.CommentsLabel":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.CommentsPane":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.CopyProgressIndicator":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.DocumentToolbar":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.DocumentView":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.FontPropertiesDialog":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.FreeTextAnnotationTextBox":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.LayerPane":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.NotificationBar":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.OutlinePane":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.PageOrganizerPane":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.PdfDocumentView":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.PdfLoadingIndicator":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.PdfProgressIndicator":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.PdfViewerControl":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.RedactionToolbar":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.TextSearchBar":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.TextSearchProgressIndicator":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.PdfViewer.ThumbnailPane":
					styles.Add(rootStylePath + "PdfViewerControl/PdfViewerControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.SfRichTextBoxAdv":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/SfRichTextBoxAdv.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.PasswordDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/Dialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.HyperlinkDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/Dialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.FindAndReplaceDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/Dialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.ShowMessageDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/Dialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.ParagraphDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/FormatDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.ListDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/FormatDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.FontDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/FormatDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.BulletsAndNumberingDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/FormatDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.MiniToolBar":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/MiniToolBar.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.StylesDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/StyleDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.StyleDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/StyleDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.TableDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/TableDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.CellOptionsDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/TableDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.TableOptionsDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/TableDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.BordersAndShadingDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/TableDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.InsertTableDialog":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/TableDialogs.xaml");
					break;
				case "Syncfusion.Windows.Controls.RichTextBoxAdv.SfRichTextRibbon":
					styles.Add(rootStylePath + "SfRichTextBoxAdv/SfRichTextRibbon.xaml");
					break;
				case "Syncfusion.Windows.Edit.LineItem":
					styles.Add(rootStylePath + "EditControl/EditControl.xaml");
					break;
				case "Syncfusion.Windows.Edit.FindReplaceControl":
					styles.Add(rootStylePath + "EditControl/EditControl.xaml");
					break;
				case "Syncfusion.Windows.Edit.EditControl":
					styles.Add(rootStylePath + "EditControl/EditControl.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.SpreadsheetGroupButton":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.ProgressRing":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.SfSpreadsheet":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.FormulaBar":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.SpreadsheetGrid":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.PasteDropDownItem":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.FillDropDownItem":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.FindAndReplaceDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.FormatCellsDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.ProtectWorkbookDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.ProtectSheetDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.InsertDeleteCellsDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.UnprotectWorkbookDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.UnprotectSheetDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.GoToDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.DataValidationDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.UnHideSheetDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.BetweenNotBetweenDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.PasswordDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.OutlineSettingsDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.NewNameRangeDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.ConditionalFormatDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.FileEncryptDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.DateOccurringConditionDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.DefaultColumnWidthDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.GroupUngroupDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.FormatAsTableDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.InsertHyperlinkDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.NameManagerDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.FormatHeightAndWidthDialog":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheet.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.CheckboxFilterControl":
					styles.Add(rootStylePath + "SfSpreadsheet/SpreadsheetFilterControl.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.SpreadsheetFilterControl":
					styles.Add(rootStylePath + "SfSpreadsheet/SpreadsheetFilterControl.xaml");
					break;
				case "Syncfusion.UI.Xaml.Spreadsheet.SfSpreadsheetRibbon":
					styles.Add(rootStylePath + "SfSpreadsheet/SfSpreadsheetRibbon.xaml");
					break;
				case "MSControls":
					styles.Add(rootStylePath + "MSControl/Button.xaml");
					styles.Add(rootStylePath + "MSControl/FlatButton.xaml");
					styles.Add(rootStylePath + "MSControl/GlyphButton.xaml");
					styles.Add(rootStylePath + "MSControl/GlyphPrimaryToggleButton.xaml");
					styles.Add(rootStylePath + "MSControl/GlyphToggleButton.xaml");
					styles.Add(rootStylePath + "MSControl/GlyphTreeExpander.xaml");
					styles.Add(rootStylePath + "MSControl/GlyphDropdownExpander.xaml");
					styles.Add(rootStylePath + "MSControl/GlyphEditableDropdownExpander.xaml");
					styles.Add(rootStylePath + "MSControl/GroupBox.xaml");
					styles.Add(rootStylePath + "MSControl/Label.xaml");
					styles.Add(rootStylePath + "MSControl/Hyperlink.xaml");
					styles.Add(rootStylePath + "MSControl/PasswordBox.xaml");
					styles.Add(rootStylePath + "MSControl/PrimaryButton.xaml");
					styles.Add(rootStylePath + "MSControl/FlatPrimaryButton.xaml");
					styles.Add(rootStylePath + "MSControl/ProgressBar.xaml");
					styles.Add(rootStylePath + "MSControl/RadioButton.xaml");
					styles.Add(rootStylePath + "MSControl/TextBox.xaml");
					styles.Add(rootStylePath + "MSControl/ToggleButton.xaml");
					styles.Add(rootStylePath + "MSControl/FlatToggleButton.xaml");
					styles.Add(rootStylePath + "MSControl/RepeatButton.xaml");
					styles.Add(rootStylePath + "MSControl/GlyphRepeatButton.xaml");
					styles.Add(rootStylePath + "MSControl/ComboBox.xaml");
					styles.Add(rootStylePath + "MSControl/CheckBox.xaml");
					styles.Add(rootStylePath + "MSControl/Calendar.xaml");
					styles.Add(rootStylePath + "MSControl/DatePicker.xaml");
					styles.Add(rootStylePath + "MSControl/GridSplitter.xaml");
					styles.Add(rootStylePath + "MSControl/Separator.xaml");
					styles.Add(rootStylePath + "MSControl/Expander.xaml");
					styles.Add(rootStylePath + "MSControl/ToolTip.xaml");
					styles.Add(rootStylePath + "MSControl/Window.xaml");
					styles.Add(rootStylePath + "MSControl/Slider.xaml");
					styles.Add(rootStylePath + "MSControl/StatusBar.xaml");
					styles.Add(rootStylePath + "MSControl/ResizeGrip.xaml");
					styles.Add(rootStylePath + "MSControl/ScrollViewer.xaml");
					styles.Add(rootStylePath + "MSControl/TabControl.xaml");
					styles.Add(rootStylePath + "MSControl/TreeView.xaml");
					styles.Add(rootStylePath + "MSControl/ListView.xaml");
					styles.Add(rootStylePath + "MSControl/ListBox.xaml");
					styles.Add(rootStylePath + "MSControl/Menu.xaml");
					styles.Add(rootStylePath + "MSControl/ToolBar.xaml");
					styles.Add(rootStylePath + "MSControl/RichTextBox.xaml");
					styles.Add(rootStylePath + "MSControl/DataGrid.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ChartPrintDialog":
					styles.Add(rootStylePath + "SfChart/SfChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ShapeAnnotation":
					styles.Add(rootStylePath + "SfChart/SfChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.HorizontalLineAnnotation":
					styles.Add(rootStylePath + "SfChart/SfChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.VerticalLineAnnotation":
					styles.Add(rootStylePath + "SfChart/SfChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.EllipseAnnotation":
					styles.Add(rootStylePath + "SfChart/SfChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.RectangleAnnotation":
					styles.Add(rootStylePath + "SfChart/SfChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SfChart":
					styles.Add(rootStylePath + "SfChart/ChartArea.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.NumericalAxis":
					styles.Add(rootStylePath + "SfChart/ChartAxis.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.CategoryAxis":
					styles.Add(rootStylePath + "SfChart/ChartAxis.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.LogarithmicAxis":
					styles.Add(rootStylePath + "SfChart/ChartAxis.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.TimeSpanAxis":
					styles.Add(rootStylePath + "SfChart/ChartAxis.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.DateTimeAxis":
					styles.Add(rootStylePath + "SfChart/ChartAxis.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.DateTimeCategoryAxis":
					styles.Add(rootStylePath + "SfChart/ChartAxis.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ZoomingToolBar":
					styles.Add(rootStylePath + "SfChart/ChartToolBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ZoomIn":
					styles.Add(rootStylePath + "SfChart/ChartToolBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ZoomOut":
					styles.Add(rootStylePath + "SfChart/ChartToolBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ZoomReset":
					styles.Add(rootStylePath + "SfChart/ChartToolBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ZoomPan":
					styles.Add(rootStylePath + "SfChart/ChartToolBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SelectionZoom":
					styles.Add(rootStylePath + "SfChart/ChartToolBar.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.Resizer":
					styles.Add(rootStylePath + "SfChart/Resizer.xaml");
					break;
				case "Syncfusion.UI.Xaml.Kanban.PlaceholderStyle":
					styles.Add(rootStylePath + "SfKanban/SfKanban.xaml");
					break;
				case "Syncfusion.UI.Xaml.Kanban.Swimlane":
					styles.Add(rootStylePath + "SfKanban/SfKanban.xaml");
					break;
				case "Syncfusion.UI.Xaml.Kanban.KanbanColumn":
					styles.Add(rootStylePath + "SfKanban/SfKanban.xaml");
					break;
				case "Syncfusion.UI.Xaml.Kanban.SwimlaneColumn":
					styles.Add(rootStylePath + "SfKanban/SfKanban.xaml");
					break;
				case "Syncfusion.UI.Xaml.Kanban.TagsStackPanel":
					styles.Add(rootStylePath + "SfKanban/SfKanban.xaml");
					break;
				case "Syncfusion.UI.Xaml.Kanban.SfKanban":
					styles.Add(rootStylePath + "SfKanban/SfKanban.xaml");
					break;
				case "Syncfusion.UI.Xaml.Kanban.KanbanTagsScrollBar":
					styles.Add(rootStylePath + "SfKanban/SfKanban.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ResizableScrollBar":
					styles.Add(rootStylePath + "SfDateTimeRangeNavigator/SfDateTimeRangeNavigator.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SfDateTimeRangeNavigator":
					styles.Add(rootStylePath + "SfDateTimeRangeNavigator/SfDateTimeRangeNavigator.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.BarSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.StackingBarSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.StackingBar100Series3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.StackingColumn100Series3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.StackingColumnSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ScatterSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.AreaSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.DoughnutSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.PieSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ColumnSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.LineSeries3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.CategoryAxis3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.DateTimeAxis3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.LogarithmicAxis3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.NumericalAxis3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.TimeSpanAxis3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SfChart3D":
					styles.Add(rootStylePath + "SfChart3D/SfChart3D.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.SfCircularGauge":
					styles.Add(rootStylePath + "SfCircularGauge/SfCircularGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.CircularScale":
					styles.Add(rootStylePath + "SfCircularGauge/SfCircularGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.CircularRange":
					styles.Add(rootStylePath + "SfCircularGauge/SfCircularGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.CircularPointer":
					styles.Add(rootStylePath + "SfCircularGauge/SfCircularGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.SfLinearGauge":
					styles.Add(rootStylePath + "SfLinearGauge/SfLinearGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.LinearRange":
					styles.Add(rootStylePath + "SfLinearGauge/SfLinearGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.LinearPointer":
					styles.Add(rootStylePath + "SfLinearGauge/SfLinearGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.LinearScale":
					styles.Add(rootStylePath + "SfLinearGauge/SfLinearGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.LinearScaleLabel":
					styles.Add(rootStylePath + "SfLinearGauge/SfLinearGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.Gauges.SfDigitalGauge":
					styles.Add(rootStylePath + "SfDigitalGauge/SfDigitalGauge.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeMap.TreeMapItem":
					styles.Add(rootStylePath + "SfTreeMap/SfTreeMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeMap.TreeMapLegend":
					styles.Add(rootStylePath + "SfTreeMap/SfTreeMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.TreeMap.SfTreeMap":
					styles.Add(rootStylePath + "SfTreeMap/SfTreeMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.Maps.ImageryLayer":
					styles.Add(rootStylePath + "SfMap/SfMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.Maps.SfMap":
					styles.Add(rootStylePath + "SfMap/SfMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.Maps.ShapeFileLayer":
					styles.Add(rootStylePath + "SfMap/SfMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.SmithChart.SmithChartLegend":
					styles.Add(rootStylePath + "SfSmithChart/SfSmithChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.SmithChart.RadialAxis":
					styles.Add(rootStylePath + "SfSmithChart/SfSmithChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.SmithChart.HorizontalAxis":
					styles.Add(rootStylePath + "SfSmithChart/SfSmithChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.SmithChart.SfSmithChart":
					styles.Add(rootStylePath + "SfSmithChart/SfSmithChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.SunburstChart.SfSunburstChart":
					styles.Add(rootStylePath + "SfSunburstChart/SfSunburstChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.SunburstChart.SunburstLegend":
					styles.Add(rootStylePath + "SfSunburstChart/SfSunburstChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.BulletGraph.SfBulletGraph":
					styles.Add(rootStylePath + "SfBulletGraph/SfBulletGraph.xaml");
					break;
				case "Syncfusion.UI.Xaml.BulletGraph.QualitativeRange":
					styles.Add(rootStylePath + "SfBulletGraph/SfBulletGraph.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SfAreaSparkline":
					styles.Add(rootStylePath + "SfAreaSparkline/SfAreaSparkline.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SfLineSparkline":
					styles.Add(rootStylePath + "SfLineSparkline/SfLineSparkline.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SfColumnSparkline":
					styles.Add(rootStylePath + "SfColumnSparkline/SfColumnSparkline.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SfWinLossSparkline":
					styles.Add(rootStylePath + "SfWinLossSparkline/SfWinLossSparkline.xaml");
					break;
				case "Syncfusion.UI.Xaml.ImageEditor.SfImageEditor":
					styles.Add(rootStylePath + "SfImageEditor/SfImageEditor.xaml");
					break;
				case "Syncfusion.UI.Xaml.ImageEditor.ToolbarMenuItem":
					styles.Add(rootStylePath + "SfImageEditor/SfImageEditor.xaml");
					break;
				case "Syncfusion.UI.Xaml.HeatMap.SfHeatMap":
					styles.Add(rootStylePath + "SfHeatMap/SfHeatMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.HeatMap.HeatMapCell":
					styles.Add(rootStylePath + "SfHeatMap/SfHeatMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.HeatMap.RowHeader":
					styles.Add(rootStylePath + "SfHeatMap/SfHeatMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.HeatMap.ColumnHeader":
					styles.Add(rootStylePath + "SfHeatMap/SfHeatMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.HeatMap.SfHeatMapLegend":
					styles.Add(rootStylePath + "SfHeatMap/SfHeatMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.HeatMap.ScrollViewer":
					styles.Add(rootStylePath + "SfHeatMap/SfHeatMap.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Container":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.BpmnGroup":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.BpmnNode":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Node":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.PortBase":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.NodePort":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.ConnectorPort":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.BpmnFlow":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Connector":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Group":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Swimlane":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.ContainerHeader":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.SwimlaneHeader":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Lane":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Phase":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.DockPort":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.QuickCommand":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Selector":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.DiagramThumb":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.Ruler":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.RulerSegment":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.SfDiagram":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.Overview":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.OverviewResizer":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.AnnotationEditor":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.RunTimeConnectionIndicator":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.ScrollViewer":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.PrintPreviewWindow":
					styles.Add(rootStylePath + "SfDiagram/SfDiagram.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.PrintPreviewControl":
					styles.Add(rootStylePath + "PrintPreviewControl/PrintPreviewControl.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Controls.PageNavigator":
					styles.Add(rootStylePath + "PrintPreviewControl/PrintPreviewControl.xaml");
					break;
				case "Syncfusion.Windows.Shared.Printing.PrintPageControl":
					styles.Add(rootStylePath + "PrintPreview/PrintPreview.xaml");
					break;
				case "Syncfusion.Windows.Shared.Printing.PrintOptionsControl":
					styles.Add(rootStylePath + "PrintPreview/PrintPreview.xaml");
					break;
				case "Syncfusion.Windows.Shared.Printing.PrintPreviewAreaControl":
					styles.Add(rootStylePath + "PrintPreview/PrintPreview.xaml");
					break;
				case "Syncfusion.Windows.Shared.Printing.PrintPreview":
					styles.Add(rootStylePath + "PrintPreview/PrintPreview.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Stencil.Stencil":
					styles.Add(rootStylePath + "Stencil/Stencil.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Stencil.Symbol":
					styles.Add(rootStylePath + "Stencil/Stencil.xaml");
					break;
				case "Syncfusion.UI.Xaml.Diagram.Stencil.SymbolGroup":
					styles.Add(rootStylePath + "Stencil/Stencil.xaml");
					break;
				case "Syncfusion.UI.Xaml.DiagramRibbon.SfDiagramRibbon":
					styles.Add(rootStylePath + "SfDiagramRibbon/SfDiagramRibbon.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.GanttControl":
					styles.Add(rootStylePath + "GanttControl/GanttControl.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.GanttSchedule":
					styles.Add(rootStylePath + "GanttControl/GanttSchedule.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.GanttScheduleRow":
					styles.Add(rootStylePath + "GanttControl/GanttSchedule.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.GanttScheduleCell":
					styles.Add(rootStylePath + "GanttControl/GanttSchedule.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.GanttChart":
					styles.Add(rootStylePath + "GanttControl/GanttChart.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.GanttNodeConnector":
					styles.Add(rootStylePath + "GanttControl/GanttChart.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.StripLine":
					styles.Add(rootStylePath + "GanttControl/GanttChart.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.GanttChartRow":
					styles.Add(rootStylePath + "GanttControl/GanttChart.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.GanttNode":
					styles.Add(rootStylePath + "GanttControl/GanttChartItems.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.HeaderNode":
					styles.Add(rootStylePath + "GanttControl/GanttChartItems.xaml");
					break;
				case "Syncfusion.Windows.Controls.Gantt.MileStone":
					styles.Add(rootStylePath + "GanttControl/GanttChartItems.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SfSurfaceChart":
					styles.Add(rootStylePath + "SfSurfaceChart/SfSurfaceChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.ChartColorBar":
					styles.Add(rootStylePath + "SfSurfaceChart/SfSurfaceChart.xaml");
					break;
				case "Syncfusion.UI.Xaml.Charts.SurfaceAxis":
					styles.Add(rootStylePath + "SfSurfaceChart/SurfaceAxis.xaml");
					break;
				case "Common":
					styles.Add(rootStylePath + "Common/Common.xaml");
					break;
				case "Brushes":
					styles.Add(rootStylePath + "Common/Brushes.xaml");
					break;
			}

            # endregion

            return styles;
        }
    }

    #region Palette enum

	
	/// Specifies the different set of palette color combination to apply on specific theme.
	
	public enum FluentPalette
	{
		
		/// The Default palette primary colors will be applied for specific theme.
		
		Default,
		
		/// The PinkRed palette primary colors will be applied for specific theme.
		
		PinkRed,
		
		/// The Red palette primary colors will be applied for specific theme.
		
		Red,
		
		/// The RedOrange palette primary colors will be applied for specific theme.
		
		RedOrange,
		
		/// The Orange palette primary colors will be applied for specific theme.
		
		Orange,
		
		/// The Green palette primary colors will be applied for specific theme.
		
		Green,
		
		/// The GreenCyan palette primary colors will be applied for specific theme.
		
		GreenCyan,
		
		/// The Cyan palette primary colors will be applied for specific theme.
		
		Cyan,
		
		/// The CyanBlue palette primary colors will be applied for specific theme.
		
		CyanBlue,
		
		/// The Blue palette primary colors will be applied for specific theme.
		
		Blue,
		
		/// The BlueMegenta palette primary colors will be applied for specific theme.
		
		BlueMegenta,
		
		/// The Megenta palette primary colors will be applied for specific theme.
		
		Megenta,
		
		/// The MegentaPink palette primary colors will be applied for specific theme.
		
		MegentaPink
	}
    #endregion

    
    /// Represents a class that holds the respective theme color and common key values for customization
    
    public class FluentDarkThemeSettings: IThemeSetting
    {
        
        /// Constructor to create an instance of FluentDarkThemeSettings.
        
        public FluentDarkThemeSettings()
        {
            #region Initialize Value 
			HeaderFontSize = 16;
			SubHeaderFontSize = 14;
			TitleFontSize = 14;
			SubTitleFontSize = 12;
			BodyFontSize = 12;
			BodyAltFontSize = 10;

            #endregion
        }

        #region Palette Properties
        
        /// Gets or sets the palette primary colors to be set for specific theme. 
        
        /// <value>
        /// <para>One of the <see cref="Palette"/> enumeration that specifies the palette to be chosen.</para>
        /// <para>The default value is <see cref="FluentPalette.Default"/>.</para>
        /// <para><b>Fields:</b></para>
        /// <list type="table">
        /// <listheader>
        /// <term>Enumeration</term>
        /// <description>Description.</description>
        /// </listheader>
		/// <item>
		/// <term><see cref="FluentPalette.Default"/></term>
		/// <description>The Default palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.PinkRed"/></term>
		/// <description>The PinkRed palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.Red"/></term>
		/// <description>The Red palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.RedOrange"/></term>
		/// <description>The RedOrange palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.Orange"/></term>
		/// <description>The Orange palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.Green"/></term>
		/// <description>The Green palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.GreenCyan"/></term>
		/// <description>The GreenCyan palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.Cyan"/></term>
		/// <description>The Cyan palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.CyanBlue"/></term>
		/// <description>The CyanBlue palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.Blue"/></term>
		/// <description>The Blue palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.BlueMegenta"/></term>
		/// <description>The BlueMegenta palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.Megenta"/></term>
		/// <description>The Megenta palette primary colors will be applied for specific theme.</description>
		/// </item>
		/// <item>
		/// <term><see cref="FluentPalette.MegentaPink"/></term>
		/// <description>The MegentaPink palette primary colors will be applied for specific theme.</description>
		/// </item>
        /// </list>
        /// </value>
        /// <example>
        /// <code language="C#">
        /// <![CDATA[
        /// FluentDarkThemeSettings themeSettings = new FluentDarkThemeSettings();
		/// themeSettings.Palette = FluentPalette.PinkRed;
        /// SfSkinManager.RegisterThemeSettings("FluentDark", themeSettings);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// Applicable only for <see href="https://help.syncfusion.com/wpf/themes/skin-manager#themes-list">ThemeStudio specific themes.</see>
        /// </remarks>
        public FluentPalette Palette { get; set; }
        #endregion

        #region Properties


		
		/// Gets or sets the font size of header related areas of control in selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.HeaderFontSize = 16;
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public Double HeaderFontSize { get; set; }


		
		/// Gets or sets the font size of sub header related areas of control in selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.SubHeaderFontSize = 14;
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public Double SubHeaderFontSize { get; set; }


		
		/// Gets or sets the font size of title related areas of control in selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.TitleFontSize = 14;
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public Double TitleFontSize { get; set; }


		
		/// Gets or sets the font size of sub title related areas of control in selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.SubTitleFontSize = 12;
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public Double SubTitleFontSize { get; set; }


		
		/// Gets or sets the font size of content area of control in selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.BodyFontSize = 12;
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public Double BodyFontSize { get; set; }


		
		/// Gets or sets the alternate font size of content area of control in selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.Body AltFontSize = 10;
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public Double BodyAltFontSize { get; set; }


		
		/// Gets or sets the font family of text in control for selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.FontFamily = new FontFamily("Callibri");
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public FontFamily FontFamily { get; set; }

		private Brush primarybackground;


		
		/// Gets or sets the primary background color of content area of control in selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.PrimaryBackground = Brushes.Red;
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public Brush PrimaryBackground
		{
			get
			{
				return primarybackground;
			}
			set
			{
				primarybackground = value;
				PrimaryColorForeground = value;
				PrimaryColorDark3 = ThemeSettingsHelper.GetDerivationColor(value, 0.2, 0);
				PrimaryColorDark2 = ThemeSettingsHelper.GetDerivationColor(value, 0.15, 0);
				PrimaryColorDark1 = ThemeSettingsHelper.GetDerivationColor(value, 0.05, 0);
				PrimaryColorLight1 = ThemeSettingsHelper.GetDerivationColor(value, -0.1, 0);
				PrimaryColorLight2 = ThemeSettingsHelper.GetDerivationColor(value, -0.15, 0);
				PrimaryColorLight3 = ThemeSettingsHelper.GetDerivationColor(value, -0.25, 0);
				LinkForeground = value;
				PrimaryBackgroundOpacity1 = ThemeSettingsHelper.GetDerivationColor(value, 0, 0.4);
			}
		}


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryColorForeground { get; set; }


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryColorDark3 { get; set; }


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryColorDark2 { get; set; }


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryColorDark1 { get; set; }


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryColorLight1 { get; set; }


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryColorLight2 { get; set; }


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryColorLight3 { get; set; }


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush LinkForeground { get; set; }


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryBackgroundOpacity1 { get; set; }

		private Brush primaryforeground;


		
		/// Gets or sets the primary foreground color of content area of control in selected theme
		
		/// <example>
		/// <code language="C#">
		/// <![CDATA[
		/// FluentDarkThemeSettings fluentDarkThemeSettings = new FluentDarkThemeSettings();
		/// fluentDarkThemeSettings.PrimaryForeground = Brushes.AntiqueWhite;
		/// SfSkinManager.RegisterThemeSettings("FluentDark", fluentDarkThemeSettings);
		/// ]]>
		/// </code>
		/// </example>
		public Brush PrimaryForeground
		{
			get
			{
				return primaryforeground;
			}
			set
			{
				primaryforeground = value;
				PrimaryForegroundDisabled = ThemeSettingsHelper.GetDerivationColor(value, 0, 0.5);
			}
		}


		[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
		[System.ComponentModel.Browsable(false)]
		public Brush PrimaryForegroundDisabled { get; set; }

        #endregion

        
        /// Helper method to decide on display property name using property mappings 
        
        /// <returns>Dictionary of property mappings</returns>
        /// <exclude/>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.Browsable(false)]
        public Dictionary<string, string> GetPropertyMappings()
        {
            Dictionary<string, string> propertyMappings = new Dictionary<string, string>();
            #region PropertyMappings
			propertyMappings.Add("HeaderFontSize", "HeaderTextStyle");
			propertyMappings.Add("SubHeaderFontSize", "SubHeaderTextStyle");
			propertyMappings.Add("TitleFontSize", "TitleTextStyle");
			propertyMappings.Add("SubTitleFontSize", "SubTitleTextStyle");
			propertyMappings.Add("BodyFontSize", "BodyTextStyle");
			propertyMappings.Add("BodyAltFontSize", "CaptionText");
			propertyMappings.Add("FontFamily", "ThemeFontFamily");

            #endregion
            return propertyMappings;
        }
    }
}
