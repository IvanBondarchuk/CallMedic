using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMedic
{
    internal class User
    {
        public string? PhoneNumber { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
    }

    struct Date
    {
        public int day;
        public int month;
        public int year; 
        public int hour;
        public int minute;
    } 
    internal class Ill : User
    {
            public long TelegramId { get; set; }
            public Date date = new Date();
            public string? Reason { get; set; }
            public string? medicname { get; set; }
            public string? callresult { get; set; }
            public bool IsVerified { get; set; } = false;
    }
    internal class Medic : User
    {
        public string password { get; set; }
    }
}
