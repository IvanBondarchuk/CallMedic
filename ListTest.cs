using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMedic
{
    internal class ListTest
    {
        internal static List<User> users = new();
        internal static List<Ill> Illness = new();
        internal static List<Medic> medics = new();
        internal static void CreateTestData()
        {
            var user1 = new User();
            var user2 = new User();
            var user3 = new User();
            var user4 = new User();

            //Некий гражданин Иванов Иван Иванович
            user1.FullName = "Иванов Иван Иванович";
            user1.Address = "Ул. Пушкина, д.17, кв.12";
            user1.PhoneNumber = "79220547445";

            //Несовершеннолетний сын Иванова
            user2.FullName = "Иванов Дмитрий Иванович";
            user2.Address = "Ул. Пушкина, д.17, кв.12";
            user2.PhoneNumber = "79220547445";

            //Какой-то мужик
            user3.FullName = "Константинов Владимир Фёдорович";
            user3.Address = "Ул. Ленина, д.23, кв.10";
            user3.PhoneNumber = "79124045566";

            //Данил Гатиятов
            user4.FullName = "Гатиятов Данил Динисович";
            user4.Address = "Адрес";
            user4.PhoneNumber = "79088802859";

            users.Add(user1);
            users.Add(user2);
            users.Add(user3);
            users.Add(user4);

            //Уже готовые заявки
            var ill1 = new Ill();
            var ill2 = new Ill();
            var ill3 = new Ill();
            var ill4 = new Ill();

            //Первый вызов
            ill1.FullName = "Петров Пётр Петрович";
            ill1.PhoneNumber = "79004563212";

            ill1.date.year = 2025;
            ill1.date.month = 11;
            ill1.date.day = 16;
            ill1.date.hour = 10;
            ill1.date.minute = 0;

            ill1.Address = "Ул. Ленина, д.12, кв 4";
            ill1.TelegramId = 1234567;
            ill1.Reason = "температура больше 38, кашель.";
            ill1.medicname = "Себастьян Перрейро";

            //Второй вызов
            ill2.FullName = "Константинов Владимир Фёдорович";
            ill2.PhoneNumber = "79124045566";

            ill2.date.year = 2025;
            ill2.date.month = 11;
            ill2.date.day = 16;
            ill2.date.hour = 12;
            ill2.date.minute = 20;

            ill2.Address = "Ул. Энтузиастов, д.39, кв 4";
            ill2.TelegramId = 1234576;
            ill2.Reason = "температура от 37 до 38, насморк, слабость.";
            ill2.medicname = "Себастьян Перрейро";

            //Третий вызов

            ill3.FullName = "Иванов Иван Иванович";
            ill3.PhoneNumber = "79220547445";

            ill3.date.year = 2025;
            ill3.date.month = 11;
            ill3.date.day = 16;
            ill3.date.hour = 16;
            ill3.date.minute = 10;

            ill3.Address = "Ул. Пушкина, д.17, кв.12";
            ill3.TelegramId = 1234566;
            ill3.Reason = "температура больше 38, насморк, слабость, кашель.";
            ill3.medicname = "Себастьян Перрейро";

            //Четвёртый вызов

            ill4.FullName = "Чечивицын Василий Фёдорович";
            ill4.PhoneNumber = "79220547499";
               
            ill4.date.year = 2025;
            ill4.date.month = 11;
            ill4.date.day = 16;
            ill4.date.hour = 16;
            ill4.date.minute = 40;
               
            ill4.Address = "Ул. Пушкина, д.17, кв.12";
            ill4.TelegramId = 1234567;
            ill4.Reason = "температура больше 38, насморк, слабость, кашель.";
            ill4.medicname = "Себастьян Перрейро";


            Illness.Add(ill1);
            Illness.Add(ill2);
            Illness.Add(ill3);


            var medic1 = new Medic();

            medic1.FullName = "Себастьян Перрейро";
            medic1.PhoneNumber = "79001002233";
            medic1.Address = "Улица Пушкина, д.30, кв.1";
            medic1.password = "2025";

            medics.Add(medic1);
        }
    }
}