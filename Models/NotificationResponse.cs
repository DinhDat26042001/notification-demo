using System;

namespace NotificationAPI.Models
{
    public class NotificationResponse
    {
        public int ID { get; set; }
        public string TieuDe { get; set; }
        public string NoiDung { get; set; }
        public string Url { get; set; }
        public bool IsDaXem { get; set; }
        public bool IsDaGui { get; set; }
        public int? IDPhieu { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
