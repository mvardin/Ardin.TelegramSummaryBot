using Ardin.TelegramSummaryBot.Models;
using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using HtmlAgilityPack;

public static class HtmlNewsExtractor
{
    public static List<NewsMessage> ExtractMessages(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var messages = new List<NewsMessage>();

        var messageNodes = doc.DocumentNode
            .SelectNodes("//div[contains(@class, 'message-item')]");

        if (messageNodes == null)
            return messages;

        foreach (var msg in messageNodes)
        {
            var model = new NewsMessage()
            {
                Photos = new List<string>(),
                Links = new List<string>(),
                Files = new List<string>()
            };

            // --------------------------------------
            // 1) استخراج sid و timestamp
            // --------------------------------------
            model.Sid = msg.GetAttributeValue("data-sid", null);
            model.DateUnix = msg.GetAttributeValue("data-date", 0L);


            // --------------------------------------
            // 2) استخراج متن پیام
            // --------------------------------------
            var textContainer = msg.SelectSingleNode(".//div[contains(@class, 'KTwPFW')]");
            if (textContainer != null)
            {
                var spans = textContainer.SelectNodes(".//span[contains(@class, 'p')]");
                if (spans != null)
                {
                    var parts = new List<string>();

                    foreach (var span in spans)
                    {
                        // رد کردن mention
                        if (span.SelectSingleNode(".//span[contains(@class, 'mention')]") != null)
                            continue;

                        var t = span.InnerText?.Trim();
                        if (!string.IsNullOrWhiteSpace(t))
                            parts.Add(t);

                        // استخراج لینک‌ها
                        var aTags = span.SelectNodes(".//a[@href]");
                        if (aTags != null)
                        {
                            foreach (var a in aTags)
                            {
                                var href = a.GetAttributeValue("href", null);
                                if (!string.IsNullOrWhiteSpace(href))
                                    model.Links.Add(href);
                            }
                        }
                    }

                    model.Text = string.Join(" ", parts);
                }
            }


            // --------------------------------------
            // 3) ساعت نمایش‌شده
            // --------------------------------------
            var timeNode = msg.SelectSingleNode(".//p[contains(@class, 'x3ai0M')]");
            model.Time = timeNode?.InnerText?.Trim();


            // --------------------------------------
            // 4) بازدید (view count) — کلاس DyBAk3
            // --------------------------------------
            var viewNode = msg.SelectSingleNode(".//span[contains(@class, 'DyBAk3')]");
            if (viewNode != null)
            {
                var raw = viewNode.InnerText.Trim();
                if (int.TryParse(raw.Replace("K", "000"), out var v))
                    model.Views = v;
                else
                    model.Views = 0;
            }


            // --------------------------------------
            // 5) استخراج عکس‌ها — img تگ‌ها
            // --------------------------------------
            var imgs = msg.SelectNodes(".//img");
            if (imgs != null)
            {
                foreach (var im in imgs)
                {
                    var src = im.GetAttributeValue("src", null);
                    if (!string.IsNullOrWhiteSpace(src))
                        model.Photos.Add(src);
                }
            }


            // --------------------------------------
            // 6) استخراج فایل‌ها — تگ a[file]
            // --------------------------------------
            var fileLinks = msg.SelectNodes(".//a[contains(@href,'.pdf') or contains(@href,'.zip') or contains(@href,'.doc')]");
            if (fileLinks != null)
            {
                foreach (var f in fileLinks)
                {
                    var src = f.GetAttributeValue("href", null);
                    if (!string.IsNullOrWhiteSpace(src))
                        model.Files.Add(src);
                }
            }


            // --------------------------------------
            // 7) نهایی: فقط پیام‌هایی که متن دارند ثبت می‌کنیم
            // --------------------------------------
            if (!string.IsNullOrWhiteSpace(model.Text))
                messages.Add(model);
        }

        return messages;
    }
}
