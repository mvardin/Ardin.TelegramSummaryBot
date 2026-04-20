using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ardin.TelegramSummaryBot.Models
{
    namespace Ardin.TelegramSummaryBot.Models
    {
        public class NewsMessage
        {
            public string Sid { get; set; }            // data-sid
            public long DateUnix { get; set; }         // data-date (timestamp)
            public string Text { get; set; }           // متن
            public string Time { get; set; }           // نمایش ساعت
            public int Views { get; set; }             // بازدید
            public List<string> Photos { get; set; }   // لینک عکس‌ها
            public List<string> Links { get; set; }    // URL های داخل متن
            public List<string> Files { get; set; }    // فایل PDFs و ...
        }
    }

}
