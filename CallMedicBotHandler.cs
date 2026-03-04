// CallMedicBotHandler.cs
using MySql.Data.MySqlClient;
using System.Data;
using System.Data.Common;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using static CallMedic.ListTest;

namespace CallMedic
{


    internal class CallMedicBotHandler : BaseBotHandler
    {
        private readonly MySqlConnection _сonnection;

        private static readonly Dictionary<string, int> CauseTypeMapping = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["температура меньше 38"] = 2,
            ["кашель"] = 3,
            ["слабость"] = 4,
            ["головная боль"] = 6,
            ["плохое самочувствие"] = 7,
            ["сыпь"] = 13,
            ["насморк"] = 14,
            ["рвота"] = 15,
            ["жидкий стул"] = 16,
            ["температура больше 38"] = 17,
            ["горло"] = 24,
            ["боли в мыщцах на фоне температуры выше 38 градусов"] = 30
        };

        public CallMedicBotHandler(MySqlConnection dbConnection)
        {
            _сonnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
        }
        protected override Task InitializeDataAsync()
        {
            CreateTestData();
            return Task.CompletedTask;
        }

        //переделан на работу с бд
        protected override async Task FindUserByPhoneNumberAsync(string phoneNumber)
        {
            /* ----- Это старая реализация метода -----
            var matches = users
                .Where(u => u.PhoneNumber == phoneNumber)
                .Select(u => u.FullName)
                .ToList();

            if (matches.Any())
            {
                numberNames[phoneNumber] = matches;
            }
            return Task.CompletedTask;*/
            try
            {
                if (numberNames.ContainsKey(phoneNumber))
                {
                    return;  
                }

                string query = "SELECT DISTINCT note FROM `HOSPITAL.CALL_SERVICE` WHERE phone = @phoneNumber";

                using (var command = new MySqlCommand(query, _сonnection))
                {
                    command.Parameters.AddWithValue("@phoneNumber", phoneNumber);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var notes = new List<string>();

                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                notes.Add(reader.GetString(0));
                            }
                        }

                        numberNames[phoneNumber] = notes;
                    }
                }
            }
            catch (MySqlException ex)
            {
                // А вдруг ошибока от БД будет?
                Console.WriteLine($"MySQL ошибка: {ex.Number} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Общая ошибка: {ex.Message}");
                throw;
            }
        }

        protected override Task FindMedicByPhoneNumberAsync(string phoneNumber)
        {
            var matches = medics
                .Where(m => m.PhoneNumber == phoneNumber)
                .Select(m => m.password)
                .ToList();

            if (matches.Any())
            {
                numberPass[phoneNumber] = matches;
            }
            return Task.CompletedTask;
        }

        protected override Task FindMedicNameByPhoneNumberAsync(string phoneNumber)
        {
            var matches = medics
                .Where(u => u.PhoneNumber == phoneNumber)
                .Select(u => u.FullName)
                .ToList();

            if (matches.Any())
            {
                numberNames[phoneNumber] = matches;
            }
            return Task.CompletedTask;
        }

        protected override Task FindMedicByPasswordAsync(string password)
        {
            var matches = medics
                .Where(u => u.password == password)
                .Select(u => u.password)
                .ToList();

            if (matches.Any())
            {
                passMedic[password] = matches;
            }
            return Task.CompletedTask;
        }

        // переделано
        protected override async Task FindAddressByFullNameAsync(string fullName)
        {
            /* --- Старая реализация ---
            var matches = users
                .Where(u => u.FullName == fullName)
                .Select(u => u.Address)
                .ToList();

            if (matches.Any())
            {
                nameAddresses[fullName] = matches;
            }
            return Task.CompletedTask; */
            try
            {
                if (nameAddresses.ContainsKey(fullName))
                {
                    return;
                }

                string query = "SELECT DISTINCT address  FROM `HOSPITAL.CALL_SERVICE` WHERE note = @fullName";

                using (var command = new MySqlCommand(query, _сonnection))
                {
                    command.Parameters.AddWithValue("@fullName", fullName);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var notes = new List<string>();

                        while (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                notes.Add(reader.GetString(0));
                            }
                        }

                        nameAddresses[fullName] = notes;
                    }
                }
            }
            catch (MySqlException ex)
            {
                // А вдруг ошибока от БД будет?
                Console.WriteLine($"MySQL ошибка: {ex.Number} - {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Общая ошибка: {ex.Message}");
                throw;
            }
        }

        //это переделать
        //"INSERT INTO `HOSPITAL.CALL_SERVICE` (phone, address, note) VALUES (@phone, @adress, @note)"
        protected override async Task SaveIllnessRequestAsync(Ill request)
        {
            /* --- Старая реализация
            Illness.Add(request);
            return Task.CompletedTask;*/
            int Id = 0;
            string query = "INSERT INTO `HOSPITAL.CALL_SERVICE` (phone, address, note) VALUES (@phone, @adress, @note); SELECT LAST_INSERT_ID();";
            //вот тут добавляется запись в основную таблицу и сразу же берётся id этой записи
            using (var command = new MySqlCommand(query, _сonnection))
            {
                command.Parameters.AddWithValue("@phone", request.PhoneNumber);
                command.Parameters.AddWithValue("@adress", request.Address);
                command.Parameters.AddWithValue("@note", request.FullName);
            //    command.ExecuteNonQuery();
                var result = await command.ExecuteScalarAsync();
                Id = Convert.ToInt32(result);
            }

            var substrings = request.Reason
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
            //а вот тут добавляем связь запроса к таблице с id причин
            foreach (var substring in substrings)
            {
                if (CauseTypeMapping.TryGetValue(substring, out int causeId))
                {
                    string insertQuery = "INSERT INTO `hospital.call_service_cause` (call_service_id, call_service_cause_type_id) VALUES (@serviceid, @causeid)";

                    using (var command = new MySqlCommand(insertQuery, _сonnection))
                    {
                        command.Parameters.AddWithValue("@serviceid", Id);
                        command.Parameters.AddWithValue("@causeid", causeId);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        protected override Task FindAndSortCallsforMedic(string medicName)
        {
            var matches = ListTest.Illness
                .Where(i => i.medicname == medicName && string.IsNullOrEmpty(i.callresult))
                .ToList();
            if (matches.Any())
            {
                var sortedCalls = matches.OrderBy(p => new DateTime(p.date.year, p.date.month, p.date.day, p.date.hour, p.date.minute, 0))
                                    .ToList();
                medicCalls[medicName] = sortedCalls;
            }
            return Task.CompletedTask;
        }

        private List<Ill> GetActiveCallsForDoctor(string doctorName)
        {
            return ListTest.Illness
                .Where(i => i.medicname == doctorName && string.IsNullOrEmpty(i.callresult)) // Активные = без результата
                .OrderBy(i => new DateTime(i.date.year, i.date.month, i.date.day, i.date.hour, i.date.minute, 0)) // Сортировка от ранних к поздним
                .ToList();
        }

        protected override async Task ShowActiveCallsMessage(long chatId, string doctorName)
        {
            var activeCalls = GetActiveCallsForDoctor(doctorName);
            medicActiveCalls[chatId] = activeCalls;

            if (activeCalls.Count == 0)
            {
                await _bot.SendMessage(chatId, $"У врача {doctorName} нет активных вызовов.");
                return;
            }

            // Формируем сообщение с сокращенной инфой
            string message = $"📌 Актуальные вызовы для врача {doctorName}:\n\n";

            for (int i = 0; i < activeCalls.Count; i++)
            {
                var call = activeCalls[i];
                message += $"{i + 1}. 🏠 Адрес: {call.Address}\n";
                message += $"   ❗ Причина: {call.Reason}\n\n";
            }

            // Создаем клавиатуру с номерами
            var numberButtons = activeCalls.Select((_, i) => new KeyboardButton((i + 1).ToString())).ToList();
            var rows = numberButtons
                .Chunk(3)
                .Select(row => row.ToArray())
                .ToArray();
            var kb = new ReplyKeyboardMarkup(rows)
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await _bot.SendMessage(chatId, message, replyMarkup: kb);

            medicChapters[chatId] = 3; // Шаг выбора вызова
        }
    }
}