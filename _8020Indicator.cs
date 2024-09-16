// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace _8020Indicator
{
    /// <summary>
    /// An example of blank indicator. Add your code, compile it and use on the charts in the assigned trading terminal.
    /// Information about API you can find here: http://api.quantower.com
    /// Code samples: https://github.com/Quantower/Examples
    /// </summary>
	public class _8020Indicator : Indicator
    {
        [InputParameter("Core Numbers")]
        public string CoreLevels = "20-80";

        [InputParameter("Secondary Numbers")]
        public string SecondaryLevels = "33.5-46-66-93-3.5";

        [InputParameter("Label Offset")]
        public int LabelOffset = 10;

        [InputParameter("Price Lower Offset")]
        public int PriceLowerOffset = 100;

        [InputParameter("Price Upper Offset")]
        public int PriceUpperOffset = 100;

        [InputParameter("Main Levels Color")]
        public Color CoreColor = Color.Fuchsia;

        [InputParameter("Main Levels Label")]
        public string CoreLabel = "[Main]";

        [InputParameter("Main Levels Width")]
        public int CoreWidth = 2;

        [InputParameter("Main Levels Style")]
        public LineStyle CoreStyle = LineStyle.Solid;

        [InputParameter("Extra Levels Color")]
        public Color SecondaryColor = Color.Aqua;

        [InputParameter("Extra Levels Label")]
        public string SecondaryLabel = "[Extra]";

        [InputParameter("Extra Levels Width")]
        public int SecondaryWidth = 1;

        [InputParameter("Extra Levels Style")]
        public LineStyle SecondaryStyle = LineStyle.Dot;

        private List<(double Price, Color Color, string Label, int Width, LineStyle Style)> levels;

        private double Pivot, R1, R2, S1, S2;

        [InputParameter("Show Pivot Lines", 14)]
        public bool ShowPivotLines = true;

        [InputParameter("Pivot Line Color", 15)]
        public Color PivotColor = Color.Yellow;

        [InputParameter("Resistance Line Color", 16)]
        public Color ResistanceColor = Color.Green;

        [InputParameter("Support Line Color", 17)]
        public Color SupportColor = Color.Red;

        private HistoricalData pivotData;
        private DateTime lastCalculationDate;

        /// <summary>
        /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
        /// </summary>
        public _8020Indicator()
            : base()
        {
            // Defines indicator's name and description.
            Name = "8020Indicator";
            Description = "80/20 Indicator with Pivot Points";

            // By default indicator will be applied on main window of the chart
            SeparateWindow = false;
            levels = new List<(double, Color, string, int, LineStyle)>();
        }

        /// <summary>
        /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
        /// </summary>
        protected override void OnInit()
        {
            // Initialize pivotData
            if (Symbol != null)
            {
                pivotData = Symbol.GetHistory(Period.DAY1, Core.TimeUtils.DateTimeUtcNow.AddDays(-1));
            }
            lastCalculationDate = DateTime.MinValue;
        }

        /// <summary>
        /// Calculation entry point. This function is called when a price data updates. 
        /// Will be runing under the HistoricalBar mode during history loading. 
        /// Under NewTick during realtime. 
        /// Under NewBar if start of the new bar is required.
        /// </summary>
        /// <param name="args">Provides data of updating reason and incoming price.</param>
        protected override void OnUpdate(UpdateArgs args)
        {
            CalculateLevels();

            // Only calculate pivots if it's a new day
            if (IsNewDay())
            {
                CalculatePivots();
            }
        }

        private bool IsNewDay()
        {
            DateTime currentDate = Core.TimeUtils.DateTimeUtcNow.Date;
            return currentDate > lastCalculationDate;
        }

        private void CalculatePivots()
        {
            if (pivotData == null || pivotData.Count < 2)
            {
                return;
            }

            // Get yesterday's daily bar (the second-to-last bar in the daily data)
            HistoryItemBar yesterdayBar = (HistoryItemBar)pivotData[pivotData.Count - 2];

            if (yesterdayBar == null)
                return;

            double high = yesterdayBar.High;
            double low = yesterdayBar.Low;
            double close = yesterdayBar.Close;

            Pivot = (high + low + close) / 3;
            R1 = 2 * Pivot - low;
            S1 = 2 * Pivot - high;
            R2 = Pivot + (R1 - S1);
            S2 = Pivot - (R1 - S1);

            // Update the last calculation date
            lastCalculationDate = Core.TimeUtils.DateTimeUtcNow.Date;
        }

        private void CalculateLevels()
        {
            levels.Clear();
            double close = GetLastPrice();
            double startPrice = Math.Round(close / 100, 0) * 100 - PriceLowerOffset;
            double endPrice = startPrice + PriceUpperOffset;

            string[] coreNumbers = CoreLevels.Split('-');
            string[] secondaryNumbers = SecondaryLevels.Split('-');

            foreach (string coreNum in coreNumbers)
            {
                for (double price = startPrice; price <= endPrice; price += 100)
                {
                    string labelText = CoreLabel == "-" ? "" : coreNum + CoreLabel;
                    levels.Add((double.Parse(coreNum) + price, CoreColor, labelText, CoreWidth, CoreStyle));
                }
            }

            foreach (string secondaryNum in secondaryNumbers)
            {
                for (double price = startPrice; price <= endPrice; price += 100)
                {
                    string labelText = SecondaryLabel == "-" ? "" : secondaryNum + SecondaryLabel;
                    levels.Add((double.Parse(secondaryNum) + price, SecondaryColor, labelText, SecondaryWidth, SecondaryStyle));
                }
            }
        }

        private double GetLastPrice()
        {
            if (HistoricalData != null && HistoricalData.Count > 0)
            {
                var lastBar = HistoricalData[HistoricalData.Count - 1, SeekOriginHistory.Begin] as HistoryItemBar;
                return lastBar?.Close ?? 0;
            }
            return 0; // or some default value
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            Graphics gr = args.Graphics;
            var mainWindow = this.CurrentChart.MainWindow;
            Font font = new Font("Arial", 8, FontStyle.Bold);

            // Get left and right time from visible part of history
            DateTime leftTime = mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Left);
            DateTime rightTime = mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Right);

            // Convert left and right time to index of bar
            double leftIndex = (int)mainWindow.CoordinatesConverter.GetBarIndex(leftTime);
            double rightIndex = (int)Math.Ceiling(mainWindow.CoordinatesConverter.GetBarIndex(rightTime));

            foreach (var level in levels)
            {
                // Convert price to Y-coordinate
                double y = mainWindow.CoordinatesConverter.GetChartY(level.Price);

                // Draw horizontal line
                using (Pen pen = new Pen(level.Color, level.Width))
                {
                    pen.DashStyle = level.Style == LineStyle.Solid ? System.Drawing.Drawing2D.DashStyle.Solid :
                                    level.Style == LineStyle.Dot ? System.Drawing.Drawing2D.DashStyle.Dot :
                                    System.Drawing.Drawing2D.DashStyle.Dash;
                    gr.DrawLine(pen, mainWindow.ClientRectangle.Left, (int)y, mainWindow.ClientRectangle.Right, (int)y);
                }

                // Draw label
                if (!string.IsNullOrEmpty(level.Label))
                {
                    int labelX = mainWindow.ClientRectangle.Right - 40 - LabelOffset;
                    int labelY = (int)y - 15;
                    gr.DrawString(level.Label, font, new SolidBrush(level.Color), labelX, labelY);
                }
            }

            // Draw pivot lines if ShowPivotLines is true
            if (ShowPivotLines)
            {
                DrawPivotLine(gr, mainWindow, Pivot, "P", PivotColor);
                DrawPivotLine(gr, mainWindow, R1, "R1", ResistanceColor);
                DrawPivotLine(gr, mainWindow, R2, "R2", ResistanceColor);
                DrawPivotLine(gr, mainWindow, S1, "S1", SupportColor);
                DrawPivotLine(gr, mainWindow, S2, "S2", SupportColor);
            }
        }

        private void DrawPivotLine(Graphics gr, TradingPlatform.BusinessLayer.Chart.IChartWindow mainWindow, double price, string label, Color color)
        {
            double y = mainWindow.CoordinatesConverter.GetChartY(price);
            Font font = new Font("Arial", 8, FontStyle.Bold);

            using (Pen pen = new Pen(color, 1))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                gr.DrawLine(pen, mainWindow.ClientRectangle.Left, (int)y, mainWindow.ClientRectangle.Right, (int)y);
            }

            int labelX = mainWindow.ClientRectangle.Left + 10;
            int labelY = (int)y - 15;
            gr.DrawString($"{label} ({price:F2})", font, new SolidBrush(color), labelX, labelY);
        }
    }
}
