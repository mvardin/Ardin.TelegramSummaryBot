using System.Globalization;
using System.Text.RegularExpressions;
using Ardin.TelegramSummaryBot.Models.Ardin.TelegramSummaryBot.Models;
using HtmlAgilityPack;

public static class HtmlNewsExtractor
{
    public static List<NewsMessage> BaleExtractMessages(string html)
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
            var model = new NewsMessage
            {
                Photos = new List<string>(),
                Links = new List<string>(),
                Files = new List<string>()
            };

            // --------------------------------------
            // 1) Sid و Timestamp
            // --------------------------------------
            model.Sid = msg.GetAttributeValue("data-sid", null);
            model.DateUnix = msg.GetAttributeValue("data-date", 0L);

            // --------------------------------------
            // 2) متن پیام
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
                        // mention را نادیده بگیر
                        if (span.SelectSingleNode(".//span[contains(@class, 'mention')]") != null)
                            continue;

                        // لینک‌ها داخل span
                        var aTags = span.SelectNodes(".//a[@href]");
                        if (aTags != null)
                        {
                            foreach (var a in aTags)
                            {
                                var href = a.GetAttributeValue("href", null);
                                if (!string.IsNullOrWhiteSpace(href) && !model.Links.Contains(href))
                                    model.Links.Add(href);
                            }
                        }

                        var t = HtmlEntity.DeEntitize(span.InnerText ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(t))
                            parts.Add(t);
                    }

                    model.Text = string.Join(" ", parts).Trim();
                }
            }

            // --------------------------------------
            // 3) ساعت
            // --------------------------------------
            var timeNode = msg.SelectSingleNode(".//p[contains(@class, 'x3ai0M')]");
            model.Time = HtmlEntity.DeEntitize(timeNode?.InnerText ?? string.Empty).Trim();

            // --------------------------------------
            // 4) بازدید
            // --------------------------------------
            var viewNode = msg.SelectSingleNode(".//span[contains(@class, 'DyBAk3')]");
            model.Views = ParseCompactNumber(viewNode?.InnerText);

            // --------------------------------------
            // 5) عکس‌ها
            // --------------------------------------
            var imgs = msg.SelectNodes(".//img");
            if (imgs != null)
            {
                foreach (var im in imgs)
                {
                    var src = im.GetAttributeValue("src", null);
                    if (!string.IsNullOrWhiteSpace(src) && !model.Photos.Contains(src))
                        model.Photos.Add(src);
                }
            }

            // --------------------------------------
            // 6) فایل‌ها
            // --------------------------------------
            var fileLinks = msg.SelectNodes(".//a[contains(@href,'.pdf') or contains(@href,'.zip') or contains(@href,'.doc')]");
            if (fileLinks != null)
            {
                foreach (var f in fileLinks)
                {
                    var href = f.GetAttributeValue("href", null);
                    if (!string.IsNullOrWhiteSpace(href) && !model.Files.Contains(href))
                        model.Files.Add(href);
                }
            }

            // فقط پیام دارای متن
            if (!string.IsNullOrWhiteSpace(model.Text))
                messages.Add(model);
        }

        return messages;
    }

    public static List<NewsMessage> TelegramExtractMessages(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var messages = new List<NewsMessage>();

        var messageNodes = doc.DocumentNode
            .SelectNodes("//div[contains(@class,'Message') and @data-message-id]");

        if (messageNodes == null)
            return messages;

        foreach (var msg in messageNodes)
        {
            var model = new NewsMessage
            {
                Sid = msg.GetAttributeValue("data-message-id", null)
                      ?? ExtractMessageIdFromId(msg.GetAttributeValue("id", null)),
                Photos = new List<string>(),
                Links = new List<string>(),
                Files = new List<string>()
            };

            // --------------------------------------
            // 1) متن پیام
            // --------------------------------------
            var textNode = msg.SelectSingleNode(".//div[contains(@class,'text-content')]");
            if (textNode != null)
            {
                model.Text = ExtractTelegramText(textNode);

                // لینک‌ها از a.text-entity-link و site-name
                var linkNodes = textNode.SelectNodes(".//a[contains(@class,'text-entity-link') or contains(@class,'site-name')]");
                if (linkNodes != null)
                {
                    foreach (var l in linkNodes)
                    {
                        var href = l.GetAttributeValue("href", null);
                        if (!string.IsNullOrWhiteSpace(href) && !model.Links.Contains(href))
                            model.Links.Add(href);
                    }
                }

                // اگر لینک خام هم وجود داشت
                var rawLinks = textNode.SelectNodes(".//a[@href]");
                if (rawLinks != null)
                {
                    foreach (var l in rawLinks)
                    {
                        var href = l.GetAttributeValue("href", null);
                        if (!string.IsNullOrWhiteSpace(href) && !model.Links.Contains(href))
                            model.Links.Add(href);
                    }
                }
            }

            // --------------------------------------
            // 2) زمان
            // --------------------------------------
            var timeNode = msg.SelectSingleNode(".//span[contains(@class,'message-time')]");

            if (timeNode != null)
            {
                model.Time = HtmlEntity.DeEntitize(timeNode.InnerText ?? "")
                    .Replace("edited", string.Empty).Trim();


                var title = timeNode.GetAttributeValue("title", null);

                if (!string.IsNullOrWhiteSpace(title))
                {
                    // گرفتن خط اول
                    var firstLine = title.Split('\n')[0].Trim();

                    if (DateTimeOffset.TryParse(firstLine,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal,
                            out var dto))
                    {
                        model.DateUnix = dto.ToUnixTimeSeconds();
                    }
                }
            }


            // --------------------------------------
            // 3) بازدید
            // --------------------------------------
            var viewsNode = msg.SelectSingleNode(".//span[contains(@class,'message-views')]");
            if (viewsNode != null)
            {
                var title = viewsNode.GetAttributeValue("title", null);
                model.Views = ParseViewsFromTelegram(title, viewsNode.InnerText);
            }

            // --------------------------------------
            // 4) عکس‌ها / مدیا
            // --------------------------------------
            var imgs = msg.SelectNodes(".//img");
            if (imgs != null)
            {
                foreach (var im in imgs)
                {
                    var src = im.GetAttributeValue("src", null);
                    if (string.IsNullOrWhiteSpace(src))
                        continue;

                    // فیلتر آواتار و آیکن‌های غیرمربوط
                    if (src.Contains("avatar", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!model.Photos.Contains(src))
                        model.Photos.Add(src);
                }
            }

            // --------------------------------------
            // 5) فایل‌ها
            // --------------------------------------
            var fileNodes = msg.SelectNodes(".//a[contains(@href,'.pdf') or contains(@href,'.zip') or contains(@href,'.doc') or contains(@href,'.rar')]");
            if (fileNodes != null)
            {
                foreach (var f in fileNodes)
                {
                    var href = f.GetAttributeValue("href", null);
                    if (!string.IsNullOrWhiteSpace(href) && !model.Files.Contains(href))
                        model.Files.Add(href);
                }
            }

            // --------------------------------------
            // 6) فقط پیام‌های دارای متن
            // --------------------------------------
            if (!string.IsNullOrWhiteSpace(model.Text))
                messages.Add(model);
        }

        SetMessageDates(messages);

        return messages;
    }
    public static void SetMessageDates(List<NewsMessage> messages)
    {
        messages.Reverse();
        var lastTime = TimeSpan.MinValue;
        ;
        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            var now = DateTime.Now;
            var messageTime = TimeSpan.Parse(message.Time);
            if (lastTime > messageTime)
                break;
            lastTime = messageTime;

            var messageDatetime = new DateTime(now.Year, now.Month, now.Day, messageTime.Hours, messageTime.Minutes, 0);
            DateTimeOffset dto = new DateTimeOffset(messageDatetime);
            message.DateUnix = dto.ToUnixTimeSeconds();
        }
        
        messages.Reverse();
    }
    private static string ExtractTelegramText(HtmlNode textNode)
    {
        var parts = new List<string>();

        foreach (var node in textNode.ChildNodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var t = HtmlEntity.DeEntitize(node.InnerText).Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    parts.Add(t);
            }
            else if (node.Name.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("\n");
            }
            else if (node.Name.Equals("img", StringComparison.OrdinalIgnoreCase) &&
                     node.GetAttributeValue("class", "").Contains("emoji"))
            {
                var alt = node.GetAttributeValue("alt", "");
                if (!string.IsNullOrWhiteSpace(alt))
                    parts.Add(alt);
            }
            else
            {
                var t = HtmlEntity.DeEntitize(node.InnerText ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(t))
                    parts.Add(t);
            }
        }

        var text = string.Join(" ", parts);

        // تمیزکاری فاصله‌ها
        text = Regex.Replace(text, @"[ \t]+\n", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        text = Regex.Replace(text, @"\s{2,}", " ");

        return text.Trim();
    }

    private static string ExtractMessageIdFromId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return id.StartsWith("message-", StringComparison.OrdinalIgnoreCase)
            ? id.Substring("message-".Length)
            : id;
    }

    private static int ParseCompactNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        value = value.Trim().Replace(",", "").Replace(" ", "");

        if (value.EndsWith("K", StringComparison.OrdinalIgnoreCase))
        {
            var n = value[..^1];
            if (double.TryParse(n, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return (int)(d * 1000);
        }

        if (value.EndsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            var n = value[..^1];
            if (double.TryParse(n, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return (int)(d * 1_000_000);
        }

        if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0;
    }

    private static int ParseViewsFromTelegram(string title, string fallbackText)
    {
        // title نمونه: "Views: 22,842\n Shares: 14"
        var source = !string.IsNullOrWhiteSpace(title) ? title : fallbackText;
        if (string.IsNullOrWhiteSpace(source))
            return 0;

        var m = Regex.Match(source, @"Views:\s*([\d,\.]+)", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var raw = m.Groups[1].Value.Replace(",", "").Trim();
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return (int)d;
        }

        return ParseCompactNumber(source);
    }
}
