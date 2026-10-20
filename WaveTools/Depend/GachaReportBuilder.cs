using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace WaveTools.Depend
{
    public sealed class GachaReportOptions
    {
        public string Uid { get; set; }
        public int PoolId { get; set; }
        public GachaModel.CardPool Rule { get; set; }
        public bool ShowRecords { get; set; }
        public bool ShowStatistics { get; set; }
        public bool AutoSave { get; set; } = true;
        public bool HideUid { get; set; } = true;
        public bool ShowAllDetails { get; set; }
        public int DetailCount { get; set; } = 12;
    }

    public sealed class GachaReportImage
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Pixels { get; set; }
    }

    public sealed class GachaReportBuilder
    {
        public const int PageWidth = 640;
        public const int ExportScale = 2;
        private const float Margin = 24;
        private const int MaximumPageHeight = 8000;
        private const int DetailRowHeight = 64;
        private const float ContentWidth = PageWidth - Margin * 2;
        private const string Background = "#181a1e";
        private const string Surface = "#22252b";
        private const string Ink = "#e8eaed";
        private const string Muted = "#9ba1aa";
        private const string Divider = "#363a42";
        private const string Gold = "#ddba72";
        private const string Purple = "#b3a2c5";
        private const string FourStar = "#ad9bc9";
        private readonly GachaReportOptions _options;
        private readonly GachaModel.CardPool _rule;
        private readonly string _poolName;
        private readonly string _version;
        private readonly GachaReportImage _logo;
        private readonly Func<GachaModel.GachaRecord, GachaReportImage> _icon;
        private readonly DateTime _generatedAt = DateTime.Now;
        private readonly List<GachaModel.GachaRecord> _records;
        private readonly List<GachaModel.GachaRecord> _recentRecords;
        private readonly List<DateTime> _dates;
        private readonly List<FiveStarEntry> _fiveStars = new();
        private readonly List<FiveStarEntry> _visibleEntries;
        private readonly List<int> _fourIntervals = new();
        private readonly List<int> _upIntervals = new();
        private readonly List<int> _intervals;
        private readonly List<(PageKind Kind, int Offset, int Count, int RecordOffset, int RecordCount)> _pages = new();
        private enum PageKind { Overview, Details, Records }
        private sealed class FiveStarEntry
        {
            public GachaModel.GachaRecord Record { get; set; }
            public int Draws { get; set; }
        }
        public int PageCount => _pages.Count;
        private bool SupportsUp => _rule?.HasFiveStarUp == true;
        private static DateTime? RecordDate(GachaModel.GachaRecord record) =>
            DateTime.TryParse(record.Time, out var date) ? date : null;

        public GachaReportBuilder(List<GachaModel.GachaRecord> source, string poolName,
            GachaReportOptions options, string version, GachaReportImage logo,
            Func<GachaModel.GachaRecord, GachaReportImage> icon)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _rule = options.Rule;
            _poolName = poolName;
            _version = version;
            _logo = logo;
            _icon = icon;
            _records = source.Where(record => record != null).OrderByDescending(RecordDate)
                .ThenByDescending(record => record.Id, StringComparer.Ordinal).ToList();
            if (_records.Count == 0) throw new InvalidOperationException("当前卡池没有抽卡记录");
            int fiveDraws = 0, fourDraws = 0;
            foreach (var record in _records.AsEnumerable().Reverse())
            {
                fiveDraws++;
                fourDraws++;
                if (record.QualityLevel == 5)
                {
                    _fiveStars.Add(new FiveStarEntry { Record = record, Draws = fiveDraws });
                    fiveDraws = 0;
                }
                if (record.QualityLevel == 4)
                {
                    _fourIntervals.Add(fourDraws);
                    fourDraws = 0;
                }
            }
            int sinceUp = 0;
            foreach (var entry in _fiveStars)
            {
                sinceUp += entry.Draws;
                if (_rule?.IsFiveStarUp(entry.Record) == true)
                {
                    _upIntervals.Add(sinceUp);
                    sinceUp = 0;
                }
            }
            _intervals = _fiveStars.Select(entry => entry.Draws).OrderBy(value => value).ToList();
            _dates = _records.Select(RecordDate).Where(date => date.HasValue).Select(date => date.Value).ToList();
            _visibleEntries = _fiveStars.AsEnumerable().Reverse()
                .Take(options.ShowAllDetails ? _fiveStars.Count : Math.Max(1, options.DetailCount)).ToList();
            int detailOffset = 0;
            do
            {
                bool overview = _pages.Count == 0 && options.ShowStatistics;
                int top = overview ? 860 : 166;
                int capacity = (MaximumPageHeight - top - 30 - 44) / DetailRowHeight;
                int count = Math.Min(capacity, _visibleEntries.Count - detailOffset);
                _pages.Add((overview ? PageKind.Overview : PageKind.Details, detailOffset, count, 0, 0));
                detailOffset += count;
            } while (detailOffset < _visibleEntries.Count);
            _recentRecords = options.ShowRecords ? _records.Take(Math.Max(24, _visibleEntries.Count)).ToList() : new();
            int recordOffset = 0;
            while (recordOffset < _recentRecords.Count)
            {
                int lastIndex = _pages.Count - 1;
                var page = _pages[lastIndex];
                int top = page.Kind == PageKind.Overview ? 860 : 166;
                int recordsTop = top + 30 + Math.Max(1, page.Count) * DetailRowHeight + 20;
                int capacity = (MaximumPageHeight - recordsTop - 70 - 44) / 30;
                if (page.Kind != PageKind.Records && capacity > 0)
                {
                    int count = Math.Min(capacity, _recentRecords.Count - recordOffset);
                    _pages[lastIndex] = (page.Kind, page.Offset, page.Count, recordOffset, count);
                    recordOffset += count;
                }
                if (recordOffset < _recentRecords.Count)
                {
                    int count = Math.Min((MaximumPageHeight - 166 - 70 - 44) / 30, _recentRecords.Count - recordOffset);
                    _pages.Add((PageKind.Records, 0, 0, recordOffset, count));
                    recordOffset += count;
                }
            }
        }

        public byte[] RenderPage(int index, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = _pages[index];
            float contentTop = page.Kind == PageKind.Overview ? 860 : 166;
            float detailsBottom = contentTop + 30 + Math.Max(1, page.Count) * DetailRowHeight;
            float recordsTop = page.Kind == PageKind.Records ? contentTop : detailsBottom + 20;
            int height = (int)Math.Ceiling((page.RecordCount > 0
                ? recordsTop + 70 + page.RecordCount * 30 : detailsBottom) + 44);
            using var canvas = new ReportCanvas(PageWidth, height, ExportScale);
            Header(canvas);
            int pity = _records.TakeWhile(record => record.QualityLevel != 5).Count();
            canvas.Text($"共 {_records.Count:N0} 抽     五星 {_fiveStars.Count}     垫了 {pity} 抽", Margin, 130, ContentWidth, 22, 12, Muted);
            if (page.Kind == PageKind.Overview) Overview(canvas);
            if (page.Kind != PageKind.Records) Details(canvas, contentTop, page.Offset, page.Count, cancellationToken);
            if (page.RecordCount > 0) Records(canvas, recordsTop, page.RecordOffset, page.RecordCount, cancellationToken);
            float footer = height - 32;
            canvas.Rule(Margin, footer, ContentWidth);
            canvas.Text("WaveTools · 抽卡记录", Margin, footer + 6, ContentWidth - 100, 18, 10, Muted);
            canvas.Text($"{index + 1:D2} / {PageCount:D2}", PageWidth - Margin - 100, footer + 6, 100, 18, 10, Muted, right: true);
            cancellationToken.ThrowIfCancellationRequested();
            return canvas.Png();
        }

        private void Header(ReportCanvas c)
        {
            float brandX = Margin;
            if (_logo != null)
            {
                c.Picture(_logo, new RectangleF(Margin, 18, 24, 24));
                brandX += 32;
            }
            float brandWidth = c.TextWidth("WaveTools", 17, true);
            c.Text("WaveTools", brandX, 18, brandWidth + 2, 26, 17, Ink, true);
            c.Text(_version, brandX + brandWidth + 6, 22, 150, 20, 11, Muted);
            c.Text(_generatedAt.ToString("yyyy.MM.dd HH:mm"), PageWidth - Margin - 220, 20, 220, 22, 11, Muted, right: true);
            c.Rule(Margin, 54, ContentWidth);
            c.Text(_poolName, Margin, 65, ContentWidth, 34, 23, Ink, true);
            c.Text("UID  " + (_options.HideUid ? "已隐藏" : _options.Uid), Margin, 103, 350, 20, 11, Muted);
            string range = _dates.Count > 0 ? $"{_dates.Min():yyyy.MM.dd} — {_dates.Max():yyyy.MM.dd}" : "暂无有效日期";
            c.Text(range, PageWidth - Margin - 320, 103, 320, 20, 11, Muted, right: true);
        }

        private void Overview(ReportCanvas c)
        {
            var stats = new[]
            {
                ("总抽数", _records.Count.ToString("N0"), Ink),
                ("五星数量", _fiveStars.Count.ToString(), Gold),
                ("五星平均", Average(_intervals, " 抽"), Ink),
                ("五星 UP 平均", SupportsUp ? Average(_upIntervals, " 抽") : "—", Ink),
                ("四星数量", _fourIntervals.Count.ToString(), FourStar),
                ("四星平均", Average(_fourIntervals, " 抽"), Ink),
                ("五星 UP 数量", SupportsUp ? _upIntervals.Count.ToString() : "—", Gold),
                ("五星获取率", (_fiveStars.Count * 100.0 / _records.Count).ToString("F2") + "%", Ink)
            };
            c.Fill(new RectangleF(Margin, 164, ContentWidth, 112), Surface);
            float cellWidth = ContentWidth / 4;
            for (int i = 0; i < stats.Length; i++)
            {
                float x = Margin + i % 4 * cellWidth + 18;
                float y = 168 + i / 4 * 52;
                c.Text(stats[i].Item1, x, y, cellWidth - 36, 20, 11, Muted);
                c.Text(stats[i].Item2, x, y + 19, cellWidth - 36, 28, 21, stats[i].Item3, true);
            }
            for (int i = 1; i < 4; i++) c.Vertical(Margin + i * cellWidth, 176, 88);
            Facts(c, 292);
            c.Rule(Margin, 450, ContentWidth);
            float chartWidth = (ContentWidth - 24) / 2;
            Trend(c, Margin, 466, chartWidth);
            Monthly(c, Margin + chartWidth + 24, 466, chartWidth);
            c.Rule(Margin, 700, ContentWidth);
            Distribution(c, 716);
            c.Rule(Margin, 824, ContentWidth);
            string summary = $"活跃 {_dates.Select(date => date.Date).Distinct().Count()} 天     四星获取率 {_fourIntervals.Count * 100.0 / _records.Count:F2}%     星声折算 {(long)_records.Count * 160:N0}";
            if (_dates.Count != _records.Count) summary += $"     无效日期 {_records.Count - _dates.Count} 条";
            c.Text(summary, Margin, 830, ContentWidth, 22, 11, Muted);
        }

        private void Facts(ReportCanvas c, float y)
        {
            float width = (ContentWidth - 48) / 3;
            float left = Margin;
            c.Title("保底进度", left, y, width);
            Pity(c, left, y + 34, width, "五星", _records.TakeWhile(record => record.QualityLevel != 5).Count(),
                _rule?.HasFiveStarPity == true ? _rule.FiveStarPity : null, Gold);
            Pity(c, left, y + 94, width, "四星及以上", _records.TakeWhile(record => record.QualityLevel < 4).Count(),
                _rule?.HasFourStarPity == true ? _rule.FourStarPity : null, FourStar);
            left += width + 24;
            c.Title("星级构成", left, y, width);
            foreach (int rank in new[] { 5, 4, 3 })
            {
                int count = _records.Count(record => record.QualityLevel == rank);
                c.Pair(rank + " 星", $"{count:N0}  /  {count * 100.0 / _records.Count:F1}%", left, y + 34 + (5 - rank) * 28, width);
            }
            float barX = left;
            foreach (var segment in new[] { (5, Gold), (4, FourStar), (3, "#7791a9") })
            {
                float segmentWidth = width * _records.Count(record => record.QualityLevel == segment.Item1) / _records.Count;
                c.Fill(new RectangleF(barX, y + 131, segmentWidth, 6), segment.Item2);
                barX += segmentWidth;
            }
            left += width + 24;
            c.Title("UP / 出金", left, y, width);
            int lost = _fiveStars.Count(entry => _rule?.IsFiveStarLost(entry.Record) == true);
            c.Pair("UP / 歪了", SupportsUp ? $"{_upIntervals.Count} / {lost}" : "—", left, y + 34, width);
            c.Pair("UP 占比", SupportsUp && _fiveStars.Count > 0 ? (_upIntervals.Count * 100.0 / _fiveStars.Count).ToString("F1") + "%" : "—", left, y + 62, width);
            c.Pair("最快 / 最慢", _intervals.Count > 0 ? $"{_intervals.First()} / {_intervals.Last()} 抽" : "—", left, y + 90, width);
            c.Pair("中位抽数", _intervals.Count > 0
                ? ((_intervals[(_intervals.Count - 1) / 2] + (double)_intervals[_intervals.Count / 2]) / 2).ToString("F1") + " 抽" : "—", left, y + 118, width);
        }

        private static void Pity(ReportCanvas c, float x, float y, float width, string label, int count, int? maximum, string tint)
        {
            c.Pair(label, maximum.HasValue ? $"{count} / {maximum}" : $"{count} 抽", x, y, width);
            if (maximum.HasValue) c.Bar(x, y + 30, width, 6, Math.Min(1, count / (double)maximum.Value), tint);
        }

        private void Trend(ReportCanvas c, float x, float y, float width)
        {
            c.Title("最近 8 次出金", x, y, width);
            var recent = _fiveStars.AsEnumerable().Reverse().Take(8).ToList();
            if (recent.Count == 0) { c.Text("暂无五星", x, y + 40, width, 30, 12, Muted); return; }
            int scale = Math.Max(1, Math.Max(_rule?.HasFiveStarPity == true ? _rule.FiveStarPity.Value : 0, recent.Max(entry => entry.Draws)));
            float slot = width / recent.Count;
            for (int i = 0; i < recent.Count; i++)
            {
                float center = x + slot * (i + 0.5f);
                bool lost = _rule?.IsFiveStarLost(recent[i].Record) == true;
                string tint = DrawCountColor(recent[i].Draws / (double)(_rule?.HasFiveStarPity == true ? _rule.FiveStarPity.Value : scale));
                c.Text(recent[i].Draws.ToString(), center - 23, y + 29, 46, 20, 12, Ink, center: true);
                float height = Math.Max(3, recent[i].Draws * 114f / scale);
                c.Fill(new RectangleF(center - 5, y + 59, 10, 114), Surface);
                c.Fill(new RectangleF(center - 5, y + 173 - height, 10, height), tint);
                if (lost) c.Cross(center, y + 165 - height, Ink);
                if (i == 0) c.Text("最新", center - 23, y + 179, 46, 18, 10, Muted, center: true);
            }
            string legend = SupportsUp ? "歪了 ×" : "";
            c.Text(legend, x, y + 207, width / 2, 18, 10, Muted);
            c.Text($"平均 {recent.Average(entry => entry.Draws):F2} 抽", x + width / 2, y + 207, width / 2, 18, 11, Muted, right: true);
        }

        private void Monthly(ReportCanvas c, float x, float y, float width)
        {
            c.Title("近 6 个月抽卡量", x, y, width);
            if (_dates.Count == 0) { c.Text("暂无有效日期", x, y + 40, width, 30, 12, Muted); return; }
            var latest = _dates.Max();
            var first = new DateTime(latest.Year, latest.Month, 1);
            var months = Enumerable.Range(0, 6).Select(index => first.AddMonths(-index)).ToList();
            var counts = months.Select(month => _dates.Count(date => date.Year == month.Year && date.Month == month.Month)).ToList();
            int maximum = Math.Max(1, counts.Max());
            for (int i = 0; i < months.Count; i++)
            {
                float rowY = y + 32 + i * 27;
                c.Text(months[i].ToString("yyyy.MM"), x, rowY, 74, 22, 11, Muted);
                c.Bar(x + 84, rowY + 8, width - 174, 6, counts[i] / (double)maximum, "#7791a9");
                c.Text($"{counts[i]:N0} 抽", x + width - 82, rowY, 82, 22, 11, Ink, right: true);
            }
            c.Pair("六个月累计", counts.Sum().ToString("N0") + " 抽", x, y + 205, width);
        }

        private void Distribution(ReportCanvas c, float y)
        {
            c.Title("出金抽数分布", Margin, y, ContentWidth);
            if (_intervals.Count == 0) { c.Text("暂无五星", Margin, y + 35, ContentWidth, 22, 12, Muted); return; }
            var ranges = new[] { (1, 20), (21, 40), (41, 60), (61, 80), (81, int.MaxValue) }
                .Where(range => range.Item1 != 81 || _intervals.Any(value => value > 80)).ToList();
            float slot = ContentWidth / ranges.Count;
            for (int i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                int count = _intervals.Count(value => value >= range.Item1 && value <= range.Item2);
                float x = Margin + slot * i;
                c.Text(range.Item1 == 81 ? "80 抽以上" : $"{range.Item1}–{range.Item2} 抽", x, y + 32, slot - 22, 22, 11, Muted);
                c.Text($"{count} 次", x, y + 55, slot / 2, 28, 18, Ink, true);
                c.Text($"{count * 100.0 / _intervals.Count:F0}%", x + slot / 2, y + 59, slot / 2 - 24, 22, 11, Muted, right: true);
                c.Bar(x, y + 91, slot - 24, 4, count / (double)_intervals.Count, Gold);
            }
        }

        private void Details(ReportCanvas c, float y, int offset, int count, CancellationToken token)
        {
            c.Title("五星明细", Margin, y, ContentWidth);
            c.Text(count > 0 ? $"{offset + 1}–{offset + count} / {_fiveStars.Count}" : "0", PageWidth - Margin - 180, y, 180, 24, 11, Muted, right: true);
            if (count == 0) { c.Text("暂无五星", Margin, y + 48, ContentWidth, 22, 12, Muted); return; }
            int scale = Math.Max(1, Math.Max(_rule?.HasFiveStarPity == true ? _rule.FiveStarPity.Value : 0,
                _fiveStars.Count > 0 ? _fiveStars.Max(entry => entry.Draws) : 1));
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                var entry = _visibleEntries[offset + i];
                bool lost = _rule?.IsFiveStarLost(entry.Record) == true;
                float x = Margin;
                float rowY = y + 30 + i * DetailRowHeight;
                var picture = _icon?.Invoke(entry.Record);
                string initial = string.IsNullOrEmpty(entry.Record.Name) ? "★" : entry.Record.Name.Substring(0, 1);
                var avatar = new RectangleF(x, rowY + 6, 48, 48);
                if (picture != null) c.Picture(picture, avatar);
                else { c.Fill(avatar, Surface); c.Text(initial, avatar.X, avatar.Y, avatar.Width, avatar.Height, 20, Muted, center: true); }
                c.Text(entry.Record.Name, x + 60, rowY + 1, ContentWidth - 300, 24, 14, Ink, true, fit: true);
                string time = RecordDate(entry.Record)?.ToString("yyyy.MM.dd HH:mm") ?? entry.Record.Time;
                c.Text(time, x + ContentWidth - 160, rowY + 3, 160, 20, 10, Muted, right: true);
                float barWidth = ContentWidth - 132;
                double fraction = entry.Draws / (double)scale;
                string tint = DrawCountColor(entry.Draws / (double)(_rule?.HasFiveStarPity == true ? _rule.FiveStarPity.Value : scale));
                c.Bar(x + 60, rowY + 35, barWidth, 14, fraction, tint);
                if (lost)
                {
                    float filledWidth = (float)(barWidth * Math.Clamp(fraction, 0, 1));
                    bool fitsInside = filledWidth >= 40;
                    float crossX = x + 60 + filledWidth + (fitsInside ? -7 : 37);
                    string markerColor = fitsInside ? Background : Ink;
                    c.Text("歪了", crossX - 30, rowY + 33, 24, 18, 10, markerColor, true);
                    c.Cross(crossX, rowY + 42, markerColor);
                }
                c.Text(entry.Draws + " 抽", x + ContentWidth - 64, rowY + 29, 64, 24, 15, tint, right: true);
                c.Rule(x, rowY + 61, ContentWidth);
            }
        }

        private void Records(ReportCanvas c, float y, int offset, int count, CancellationToken token)
        {
            c.Title("最近抽卡记录", Margin, y, ContentWidth);
            c.Text($"{offset + 1}–{offset + count} / {_records.Count}", PageWidth - Margin - 200, y, 200, 24, 11, Muted, right: true);
            float rowY = y + 40;
            c.Fill(new RectangleF(Margin, rowY, ContentWidth, 30), Surface);
            RecordRow(c, new[] { "名称", "星级", "类型", "日期", "" }, rowY, Muted);
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                var record = _recentRecords[offset + i];
                rowY += 30;
                RecordRow(c, new[] { record.Name, record.QualityLevel.ToString(), record.ResourceType,
                    record.Time, _rule?.GetFiveStarUpLabel(record) ?? "" }, rowY,
                    record.QualityLevel == 5 ? Gold : record.QualityLevel == 4 ? FourStar : Ink);
                c.Rule(Margin, rowY + 29, ContentWidth);
            }
        }

        private static void RecordRow(ReportCanvas c, string[] values, float y, string tint)
        {
            float[] widths = { 180, 42, 70, 240, 60 };
            float x = Margin;
            for (int i = 0; i < values.Length; i++)
            {
                c.Text(values[i], x + 10, y + 2, widths[i] - 20, 26, 11, i == 0 ? tint : i == 4 ? Purple : Muted);
                x += widths[i];
            }
        }

        private static string DrawCountColor(double fraction)
        {
            fraction = Math.Clamp(fraction, 0, 1);
            var green = Color.FromArgb(98, 180, 154);
            var yellow = Color.FromArgb(208, 180, 101);
            var red = Color.FromArgb(207, 116, 113);
            var start = fraction <= 0.5 ? green : yellow;
            var end = fraction <= 0.5 ? yellow : red;
            double amount = fraction <= 0.5 ? fraction * 2 : (fraction - 0.5) * 2;
            int Blend(int first, int last) => (int)Math.Round(first + (last - first) * amount);
            return $"#{Blend(start.R, end.R):X2}{Blend(start.G, end.G):X2}{Blend(start.B, end.B):X2}";
        }

        private static string Average(List<int> values, string suffix = "") => values.Count > 0 ? values.Average().ToString("F2") + suffix : "—";

        private sealed class ReportCanvas : IDisposable
        {
            private readonly Bitmap _bitmap;
            private readonly Graphics _graphics;
            private readonly Dictionary<(float Size, bool Bold), Font> _fonts = new();
            private readonly Dictionary<GachaReportImage, Bitmap> _images = new();
            public ReportCanvas(int width, int height, int scale)
            {
                _bitmap = new Bitmap(checked(width * scale), checked(height * scale), PixelFormat.Format32bppPArgb);
                _bitmap.SetResolution(96 * scale, 96 * scale);
                _graphics = Graphics.FromImage(_bitmap);
                _graphics.PageUnit = GraphicsUnit.Pixel;
                _graphics.Clear(ColorOf(Background));
                _graphics.ScaleTransform(scale, scale);
                _graphics.SmoothingMode = SmoothingMode.None;
                _graphics.PixelOffsetMode = PixelOffsetMode.None;
                _graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                _graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                _graphics.CompositingQuality = CompositingQuality.HighQuality;
            }
            private static Color ColorOf(string hex) => ColorTranslator.FromHtml(hex);
            private Font Font(float size, bool bold)
            {
                var key = (size, bold);
                if (!_fonts.TryGetValue(key, out var font))
                {
                    font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                    _fonts.Add(key, font);
                }
                return font;
            }
            public float TextWidth(string value, float size, bool bold = false)
            {
                using var format = new StringFormat(StringFormat.GenericTypographic)
                {
                    FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
                };
                return _graphics.MeasureString(value ?? "", Font(size, bold), int.MaxValue, format).Width;
            }
            public void Text(string value, float x, float y, float width, float height, float size = 12,
                string color = Ink, bool bold = false, bool right = false, bool center = false, bool fit = false)
            {
                value ??= "";
                using var format = new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = right ? StringAlignment.Far : center ? StringAlignment.Center : StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                if (fit)
                    while (size > 10 && _graphics.MeasureString(value, Font(size, bold), int.MaxValue, format).Width > width) size -= 0.5f;
                using var brush = new SolidBrush(ColorOf(color));
                _graphics.DrawString(value, Font(size, bold), brush, new RectangleF(x, y, width, height), format);
            }
            public void Title(string value, float x, float y, float width) => Text(value, x, y, width, 24, 14, Ink, true);
            public void Pair(string label, string value, float x, float y, float width)
            {
                Text(label, x, y, width * 0.52f, 22, 11, Muted);
                Text(value, x + width * 0.4f, y, width * 0.6f, 22, 12, Ink, right: true);
            }
            public void Fill(RectangleF rect, string color)
            {
                using var brush = new SolidBrush(ColorOf(color));
                _graphics.FillRectangle(brush, rect);
            }
            public void Rule(float x, float y, float width, string color = Divider, float thickness = 0.5f) => Fill(new RectangleF(x, y, width, thickness), color);
            public void Vertical(float x, float y, float height) => Fill(new RectangleF(x, y, 0.5f, height), Divider);
            public void Bar(float x, float y, float width, float height, double fraction, string tint)
            {
                Fill(new RectangleF(x, y, width, height), Surface);
                Fill(new RectangleF(x, y, (float)(width * Math.Clamp(fraction, 0, 1)), height), tint);
            }
            public void Cross(float x, float y, string color)
            {
                using var pen = new Pen(ColorOf(color), 1);
                _graphics.SmoothingMode = SmoothingMode.AntiAlias;
                _graphics.DrawLine(pen, x - 3, y - 3, x + 3, y + 3);
                _graphics.DrawLine(pen, x + 3, y - 3, x - 3, y + 3);
                _graphics.SmoothingMode = SmoothingMode.None;
            }
            public void Picture(GachaReportImage image, RectangleF bounds)
            {
                if (!_images.TryGetValue(image, out var bitmap))
                {
                    if (image.Width <= 0 || image.Height <= 0 || image.Pixels == null ||
                        image.Pixels.Length != checked(image.Width * image.Height * 4)) return;
                    bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
                    var bits = bitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                    try
                    {
                        for (int row = 0; row < image.Height; row++)
                            Marshal.Copy(image.Pixels, row * image.Width * 4, IntPtr.Add(bits.Scan0, row * bits.Stride), image.Width * 4);
                    }
                    finally { bitmap.UnlockBits(bits); }
                    _images.Add(image, bitmap);
                }
                float ratio = Math.Min(bounds.Width / image.Width, bounds.Height / image.Height);
                var target = new RectangleF(bounds.X + (bounds.Width - image.Width * ratio) / 2,
                    bounds.Y + (bounds.Height - image.Height * ratio) / 2, image.Width * ratio, image.Height * ratio);
                _graphics.DrawImage(bitmap, target, new RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel);
            }
            public byte[] Png()
            {
                _graphics.Flush(FlushIntention.Sync);
                using var stream = new MemoryStream();
                _bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
            public void Dispose()
            {
                foreach (var image in _images.Values) image.Dispose();
                foreach (var font in _fonts.Values) font.Dispose();
                _graphics.Dispose();
                _bitmap.Dispose();
            }
        }
    }
}
