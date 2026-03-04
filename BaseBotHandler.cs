using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace CallMedic
{
    internal abstract class BaseBotHandler
    {
        protected TelegramBotClient _bot = null!;
        // Сегмент для юзеров (для сброса, использовать ResetUserState)
        protected Dictionary<long, int> userChapters = new();
        protected Dictionary<long, string> userNumber = new();
        protected Dictionary<long, Ill> userIll = new();
        protected Dictionary<string, List<string>> numberNames = new(); // phone → [Ф.И.О.]
        protected Dictionary<string, List<string>> nameAddresses = new(); // Ф.И.О. → [Адреса]
        protected Dictionary<long, List<string>> userFullNameOptions = new();
        protected Dictionary<long, List<string>> userAddressOptions = new();
        protected Dictionary<long, string> userReason = new();
        protected Dictionary<long, List<string>> userReasonOptions = new();
        protected Dictionary<long, int> lastReasonMessageId = new();
        // Сегмент для врачей (для сброса, использовать ResetMedicState)
        protected Dictionary<long, int> medicChapters = new();
        protected Dictionary<long, string> medicNumber = new();
        protected Dictionary<long, Medic> userMedic = new();
        protected Dictionary<string, List<string>> numberPass = new(); // phone → [password]
        protected Dictionary<string, List<string>> passMedic = new(); // password → [?]
        protected Dictionary<string, List<Ill>?> medicCalls = new(); // искать вызовы для врача и сортировать их
        protected Dictionary<long, List<Ill>> medicActiveCalls = new(); // Текущий список активных вызовов для chatId
        protected Dictionary<long, Ill> selectedCall = new(); // Выбранный вызов для chatId
        protected Dictionary<long, int> lastDetailsMessageId = new();
        protected Dictionary<long, int> lastMenuMessageId = new();

        protected readonly List<string> chapters = new()
        {
            "Введите свой номер телефона или нажмите кнопку для отправки контакта.", // 0
            "Введите своё Ф.И.О.", // 1
            "Введите адрес.", // 2
            "Введите причину своего вызова.", // 3
            "Ваш запрос отправлен в регистратуру. Ожидайте врача." // 4
        };
        protected readonly List<string> medicAuth = new()
        {
            "Введите свой номер телефона или нажмите кнопку для отправки контакта.", // 0
            "Введите код доступа.", // 1
            "Вход выполнен.", // 2
            "Выберите вызов из списка.", // 3
            "Введите результат вызова." // 4
        };
        // Статичный список причин (12 элементов)
        protected static readonly List<string> Reasons = new()
        {
            "температура больше 38",
            "кашель",
            "насморк",
            "боль в горле",
            "температура от 37 до 38",
            "плохое самочувствие",
            "слабость",
            "головная боль",
            "рвота",
            "боли в мышцах на фоне температуры",
            "сыпь",
            "жидкий стул"
        };
        public async Task StartBot(string token, CancellationToken cancellationToken)
        {
            await InitializeDataAsync();
            _bot = new TelegramBotClient(token);
            var me = await _bot.GetMe(cancellationToken: cancellationToken);
            _bot.OnUpdate += async (update) => await OnUpdate(update);
            Console.WriteLine($"@{me.Username} is running... Press Enter to terminate");
        }
        // --- Абстрактные методы для работы с данными ---
        protected abstract Task InitializeDataAsync();
        protected abstract Task FindUserByPhoneNumberAsync(string phoneNumber);
        protected abstract Task FindMedicByPhoneNumberAsync(string phoneNumber);
        protected abstract Task FindMedicNameByPhoneNumberAsync(string phoneNumber);
        protected abstract Task FindMedicByPasswordAsync(string password);
        protected abstract Task FindAddressByFullNameAsync(string fullName);
        protected abstract Task SaveIllnessRequestAsync(Ill request);
        protected abstract Task FindAndSortCallsforMedic(string medicName); //(ЗАПУСКАТЬ ИСПОЛЬЗУЯ userMedic[chatId].name !!!)
        protected abstract Task ShowActiveCallsMessage(long chatId, string doctorName);
        // ----------------------------------------------
        private async Task OnUpdate(Update update)
        {
            if (update.Type == UpdateType.Message)
            {
                await OnMessage(update.Message);
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await OnCallbackQuery(update.CallbackQuery);
            }
        }
        private async Task OnMessage(Message message)
        {
            long chatId = message.Chat.Id;
            if (message.Type == MessageType.Contact)
            {
                var phone = message.Contact.PhoneNumber;
                if (userChapters.ContainsKey(chatId) && userIll.ContainsKey(chatId))
                {
                    userNumber[chatId] = phone;
                    userIll[chatId].PhoneNumber = phone;
                    await _bot.SendMessage(chatId, $"Номер телефона получен: {phone}.", replyMarkup: new ReplyKeyboardRemove());
                    userChapters[chatId] = 1;
                    await SendChapter(chatId);
                }
                else if (medicChapters.ContainsKey(chatId) && userMedic.ContainsKey(chatId))
                {
                    medicNumber[chatId] = phone;
                    userMedic[chatId].PhoneNumber = phone;
                    await _bot.SendMessage(chatId, $"Номер телефона получен: {phone}.", replyMarkup: new ReplyKeyboardRemove());
                    medicChapters[chatId] = 1;
                    await SendChapterMedic(chatId);
                }
                return;
            }
            if (message.Type == MessageType.Location)
            {
                await _bot.SendMessage(chatId, "Ваше местоположение передано чат боту (функция в разработке).");
                return;
            }
            if (message.Type != MessageType.Text) return;
            var text = message.Text.Trim();
            if (text == "/start")
            {
                ResetUserState(chatId);
                ResetMedicState(chatId);
                var kb = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("Вызвать врача") },
                    new[] { new KeyboardButton("Для персонала (в разработке)") }
                })
                { ResizeKeyboard = true };
                await _bot.SendMessage(chatId, "Добро пожаловать в чат-бот для вызова врача на дом! Нужен ли вам врач? Если да, нажмите на кнопку вызова врача.", replyMarkup: kb);
                return;
            }
            if (text == "Вызвать врача")
            {
                if (userMedic.ContainsKey(chatId))
                {
                    ResetMedicState(chatId);
                }
                ResetUserState(chatId);
                userIll[chatId] = new Ill { TelegramId = chatId };
                userChapters[chatId] = 0;
                userNumber[chatId] = string.Empty;
                userReason[chatId] = string.Empty;
                userReasonOptions[chatId] = new List<string>();
                var phoneKb = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("Отправить номер телефона") { RequestContact = true } }
                })
                { ResizeKeyboard = true, OneTimeKeyboard = true };
                await _bot.SendMessage(chatId, chapters[0], replyMarkup: phoneKb);
                return;
            }
            if (text == "Для персонала (в разработке)")
            {
                if (userIll.ContainsKey(chatId))
                {
                    ResetUserState(chatId);
                }
                ResetMedicState(chatId);
                medicChapters[chatId] = 0;
                userMedic[chatId] = new Medic();
                var phoneKb = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("Отправить номер телефона") { RequestContact = true } }
                })
                { ResizeKeyboard = true, OneTimeKeyboard = true };
                await _bot.SendMessage(chatId, medicAuth[0], replyMarkup: phoneKb);
                return;
            }
            if (!string.IsNullOrEmpty(text) && userChapters.TryGetValue(chatId, out int step))
            {
                var illness = userIll[chatId];
                switch (step)
                {
                    case 0:
                        illness.PhoneNumber = text;
                        userNumber[chatId] = text;
                        userChapters[chatId] = 1;
                        await SendChapter(chatId);
                        return;
                    case 1:
                        illness.FullName = text;
                        userChapters[chatId] = 2;
                        await SendChapter(chatId);
                        return;
                    case 2:
                        illness.Address = text;
                        userChapters[chatId] = 3;
                        await SendChapter(chatId);
                        return;
                    case 3:
                        var currentReasons = userReasonOptions[chatId];
                        if (!currentReasons.Contains(text) && Reasons.Contains(text))
                        {
                            currentReasons.Add(text);
                            userReason[chatId] += text + ", ";
                            illness.Reason = userReason[chatId].TrimEnd(' ', ',');
                        }
                        else if (!Reasons.Contains(text))
                        {
                            userReason[chatId] += text + ", ";
                            illness.Reason = userReason[chatId].TrimEnd(' ', ',');
                        }
                        await SendChapter(chatId);
                        return;
                    case 10:
                        if (int.TryParse(text, out int fnIndex) && fnIndex > 0 && fnIndex <= userFullNameOptions[chatId].Count)
                        {
                            illness.FullName = userFullNameOptions[chatId][fnIndex - 1];
                            userChapters[chatId] = 2;
                            await _bot.SendMessage(chatId, $"Выбрано Ф.И.О.: {illness.FullName}", replyMarkup: new ReplyKeyboardRemove());
                            await SendChapter(chatId);
                        }
                        else if (text == "Ввести Ф.И.О. вручную")
                        {
                            userChapters[chatId] = 1;
                            await _bot.SendMessage(chatId, chapters[1], replyMarkup: new ReplyKeyboardRemove());
                        }
                        return;
                    case 20:
                        if (int.TryParse(text, out int addrIndex) && addrIndex > 0 && addrIndex <= userAddressOptions[chatId].Count)
                        {
                            illness.Address = userAddressOptions[chatId][addrIndex - 1];
                            userChapters[chatId] = 3;
                            await _bot.SendMessage(chatId, $"Выбран адрес: {illness.Address}", replyMarkup: new ReplyKeyboardRemove());
                            await SendChapter(chatId);
                        }
                        else if (text == "Ввести адрес вручную")
                        {
                            userChapters[chatId] = 2;
                            await _bot.SendMessage(chatId, chapters[2], replyMarkup: new ReplyKeyboardRemove());
                        }
                        return;
                }
            }
            else if (medicChapters.TryGetValue(chatId, out int medicStep))
            {
                var medic = userMedic[chatId];
                switch (medicStep)
                {
                    case 0:
                        medic.PhoneNumber = text;
                        medicNumber[chatId] = text;
                        medicChapters[chatId] = 1;
                        await SendChapterMedic(chatId);
                        return;
                    case 1:
                        medic.password = text;
                        await FindMedicByPasswordAsync(text);
                        if (passMedic.TryGetValue(text, out var passwords) && passwords.Count > 0)
                        {
                            medicChapters[chatId] = 2;
                            var msg = $@"✅ АВТОРИЗАЦИЯ ВЫПОЛНЕНА
Приветствуем вас, {medic.FullName}!
Ожидайте появления новых заявок!";
                            await _bot.SendMessage(chatId, msg, replyMarkup: new ReplyKeyboardRemove());

                            // Вызов списка
                            await FindAndSortCallsforMedic(medic.FullName);
                            await ShowActiveCallsMessage(chatId, medic.FullName);
                        }
                        else
                        {
                            await _bot.SendMessage(chatId, "Неверный код доступа! Попробуйте ещё раз.");
                        }
                        return;
                    case 3: // Выбор вызова
                        if (int.TryParse(text, out int callIndex) && callIndex > 0 && callIndex <= medicActiveCalls[chatId].Count)
                        {
                            var call = medicActiveCalls[chatId][callIndex - 1];
                            selectedCall[chatId] = call;

                            var callTime = new DateTime(call.date.year, call.date.month, call.date.day, call.date.hour, call.date.minute, 0);
                            string detailMsg = $"Детали вызова:\n" +
                                               $"👤 Пациент: {call.FullName}\n" +
                                               $"📞 Телефон: {call.PhoneNumber}\n" +
                                               $"🏠 Адрес: {call.Address}\n" +
                                               $"⏰ Время вызова: {callTime:HH:mm dd.MM.yyyy}\n" +
                                               $"❗ Причина: {call.Reason}\n";

                            var detailsSent = await _bot.SendMessage(chatId, detailMsg, replyMarkup: new ReplyKeyboardRemove());
                            lastDetailsMessageId[chatId] = detailsSent.MessageId;

                            // Показать меню с тремя пунктами
                            var menuKb = new ReplyKeyboardMarkup(new[]
                            {
                                new[] { new KeyboardButton("Закончить вызов") },
                                new[] { new KeyboardButton("Вернуться в список") },
                            })
                            { ResizeKeyboard = true, OneTimeKeyboard = true };
                            var menuSent = await _bot.SendMessage(chatId, "Выберите действие:", replyMarkup: menuKb);
                            lastMenuMessageId[chatId] = menuSent.MessageId;

                            medicChapters[chatId] = 5; // Новый шаг для обработки меню после выбора вызова
                        }
                        else
                        {
                            await _bot.SendMessage(chatId, "Неверный выбор. Попробуйте снова.");
                            await ShowActiveCallsMessage(chatId, medic.FullName);
                        }
                        return;
                    case 4: // Ввод результата
                        if (selectedCall.TryGetValue(chatId, out var selected))
                        {
                            selected.callresult = text;
                            await _bot.SendMessage(chatId, "Вызов завершен.");

                            // Обновить список
                            await FindAndSortCallsforMedic(medic.FullName);
                            await ShowActiveCallsMessage(chatId, medic.FullName);
                        }
                        return;
                    case 5: // Обработка меню после выбора вызова
                        if (text == "Закончить вызов")
                        {
                            await _bot.SendMessage(chatId, "Введите результат вызова для завершения:");
                            medicChapters[chatId] = 4; // Перейти к вводу результата
                        }
                        else if (text == "Вернуться в список")
                        {
                            if (lastDetailsMessageId.TryGetValue(chatId, out var detailsId))
                            {
                                try { await _bot.DeleteMessage(chatId, detailsId); } catch { }
                            }
                            if (lastMenuMessageId.TryGetValue(chatId, out var menuId))
                            {
                                try { await _bot.DeleteMessage(chatId, menuId); } catch { }
                            }

                            var activeCalls = medicActiveCalls[chatId];
                            var numberButtons = activeCalls.Select((_, i) => new KeyboardButton((i + 1).ToString())).ToList();
                            var rows = numberButtons.Chunk(3).Select(row => row.ToArray()).ToArray();
                            var kb = new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true, OneTimeKeyboard = true };
                            await _bot.SendMessage(chatId, "Выберите вызов из списка:", replyMarkup: kb);

                            medicChapters[chatId] = 3; // Вернуться к шагу выбора вызова
                        }
                        return;
                }
            }
            if (text == "/calls" && medicChapters.ContainsKey(chatId) && medicChapters[chatId] >= 2) // Только для авторизованных врачей
            {
                var doctor = userMedic[chatId];
                await FindAndSortCallsforMedic(doctor.FullName);
                await ShowActiveCallsMessage(chatId, doctor.FullName);
                return;
            }
        }

        private async Task OnCallbackQuery(CallbackQuery callbackQuery)
        {
            long chatId = callbackQuery.Message.Chat.Id;
            string data = callbackQuery.Data;
            if (userChapters.TryGetValue(chatId, out int step) && step == 3)
            {
                var illness = userIll[chatId];
                var currentReasons = userReasonOptions[chatId];
                if (data == "end_reason")
                {
                    if (string.IsNullOrEmpty(userReason[chatId]))
                    {
                        await _bot.AnswerCallbackQuery(callbackQuery.Id, "Выберите хотя бы одну причину!");
                        return;
                    }
                    userChapters[chatId] = 4;
                    await SendChapter(chatId);
                }
                else
                {
                    if (currentReasons.Contains(data))
                    {
                        currentReasons.Remove(data);
                        userReason[chatId] = userReason[chatId].Replace(data + ", ", "");
                    }
                    else
                    {
                        currentReasons.Add(data);
                        userReason[chatId] += data + ", ";
                    }
                    illness.Reason = userReason[chatId].TrimEnd(' ', ',');
                    await SendChapter(chatId);
                }
                await _bot.AnswerCallbackQuery(callbackQuery.Id);
            }
        }

        protected virtual async Task SendChapterMedic(long chatId)
        {
            var step = medicChapters[chatId];
            if (step == 0)
            {
                var kb = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("Отправить номер телефона") { RequestContact = true } }
                })
                { ResizeKeyboard = true, OneTimeKeyboard = true };
                await _bot.SendMessage(chatId, medicAuth[0], replyMarkup: kb);
            }
            else if (step == 1)
            {
                var phone = userMedic[chatId].PhoneNumber;
                await FindMedicByPhoneNumberAsync(phone);
                await FindMedicNameByPhoneNumberAsync(phone);
                if (numberPass.TryGetValue(phone, out var _passwords) && _passwords.Count > 0 && numberNames.TryGetValue(phone, out var _name) && _name.Count > 0)
                {
                    userMedic[chatId].FullName = _name[0];
                    await _bot.SendMessage(chatId, medicAuth[1]);
                }
            }
        }

        protected virtual async Task SendChapter(long chatId)
        {
            var step = userChapters[chatId];
            if (step == 0)
            {
                var kb = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("Отправить номер телефона") { RequestContact = true } }
                })
                { ResizeKeyboard = true, OneTimeKeyboard = true };
                await _bot.SendMessage(chatId, chapters[0], replyMarkup: kb);
            }
            else if (step == 1)
            {
                var phone = userIll[chatId].PhoneNumber;
                await FindUserByPhoneNumberAsync(phone);
                if (numberNames.TryGetValue(phone, out var fullNames) && fullNames.Count > 0)
                {
                    userFullNameOptions[chatId] = fullNames;
                    var listText = "Найдены следующие записи по вашему номеру:\n";
                    for (int i = 0; i < fullNames.Count; i++)
                    {
                        listText += $"{i + 1}. {fullNames[i]}\n";
                    }
                    listText += "\nВыберите ваше Ф.И.О. или введите вручную.";
                    var numberButtons = fullNames.Select((_, i) => new KeyboardButton((i + 1).ToString())).ToList();
                    numberButtons.Add(new KeyboardButton("Ввести Ф.И.О. вручную"));
                    var rows = numberButtons
                        .Chunk(3)
                        .Select(row => row.ToArray())
                        .ToArray();
                    var kb = new ReplyKeyboardMarkup(rows)
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    };
                    await _bot.SendMessage(chatId, listText, replyMarkup: kb);
                    userChapters[chatId] = 10;
                    return;
                }
                await _bot.SendMessage(chatId, chapters[1]);
            }
            else if (step == 2)
            {
                var fullName = userIll[chatId].FullName;
                await FindAddressByFullNameAsync(fullName);
                if (nameAddresses.TryGetValue(fullName, out var addresses) && addresses.Count > 0)
                {
                    userAddressOptions[chatId] = addresses;
                    var listText = "Найдены следующие адреса для вашего Ф.И.О.:\n";
                    for (int i = 0; i < addresses.Count; i++)
                    {
                        listText += $"{i + 1}. {addresses[i]}\n";
                    }
                    listText += "\nВыберите адрес или введите вручную.";
                    var addressButtons = addresses.Select((_, i) => new KeyboardButton((i + 1).ToString())).ToList();
                    addressButtons.Add(new KeyboardButton("Ввести адрес вручную"));
                    var rows = addressButtons
                        .Chunk(3)
                        .Select(row => row.ToArray())
                        .ToArray();
                    var kb = new ReplyKeyboardMarkup(rows)
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    };
                    await _bot.SendMessage(chatId, listText, replyMarkup: kb);
                    userChapters[chatId] = 20;
                    return;
                }
                await _bot.SendMessage(chatId, chapters[2]);
            }
            else if (step == 3)
            {
                var currentReasons = userReasonOptions[chatId];
                // Создаём inline-клавиатуру
                var buttons = new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("⛔️ Конец ввода", "end_reason") };
                buttons.AddRange(Reasons.Select(r =>
                    InlineKeyboardButton.WithCallbackData(currentReasons.Contains(r) ? $"✅ {r}" : r, r)));
                var rows = buttons.Chunk(2).Select(chunk => chunk.ToArray()).ToArray();
                var kb = new InlineKeyboardMarkup(rows);
                string msg = string.IsNullOrEmpty(userReason[chatId])
                    ? "Выберите причину вызова из списка или введите вручную. Нажмите «Конец ввода» для завершения."
                    : $"Текущие причины: {userReason[chatId].TrimEnd(' ', ',')}.\n\nВыберите следующую причину или нажмите «Конец ввода».";
                // Редактируем сообщение если возможно
                if (lastReasonMessageId.TryGetValue(chatId, out int msgId))
                {
                    try
                    {
                        await _bot.EditMessageText(chatId, msgId, msg, replyMarkup: kb);
                    }
                    catch
                    {
                        var sent = await _bot.SendMessage(chatId, msg, replyMarkup: kb);
                        lastReasonMessageId[chatId] = sent.MessageId;
                    }
                }
                else
                {
                    var sent = await _bot.SendMessage(chatId, msg, replyMarkup: kb);
                    lastReasonMessageId[chatId] = sent.MessageId;
                }
            }
            else if (step == 4)
            {
                // Удаляем последнее сообщение с причинами
                if (lastReasonMessageId.TryGetValue(chatId, out int id))
                {
                    try { await _bot.DeleteMessage(chatId, id); } catch { }
                    lastReasonMessageId.Remove(chatId);
                }
                var illness = userIll[chatId];
                DateTime now = DateTime.Now;
                illness.date.year = now.Year;
                illness.date.month = now.Month;
                illness.date.day = now.Day;
                illness.date.hour = now.Hour;
                illness.date.minute = now.Minute;
                await SaveIllnessRequestAsync(illness);
                var msg = $@"✅ ЗАЯВКА ОТПРАВЛЕНА
📞 Телефон: {illness.PhoneNumber}
👤 Ф.И.О.: {illness.FullName}
🏠 Адрес: {illness.Address}
📋 Причина вызова: {illness.Reason}
Ваша заявка передана в регистратуру. Ожидайте изменения статуса заявки.";
                await _bot.SendMessage(chatId, msg);
                var startKb = new ReplyKeyboardMarkup(new[]
                {
                    new[] { new KeyboardButton("Вызвать врача") },
                    new[] { new KeyboardButton("Для персонала (в разработке)") }
                })
                { ResizeKeyboard = true };
                await _bot.SendMessage(chatId, "Для нового вызова врача нажмите кнопку ниже.", replyMarkup: startKb);
                ResetUserState(chatId);
            }
        }
        private void ResetUserState(long chatId)
        {
            userChapters.Remove(chatId);
            userIll.Remove(chatId);
            userNumber.Remove(chatId);
            userFullNameOptions.Remove(chatId);
            userAddressOptions.Remove(chatId);
            userReason.Remove(chatId);
            userReasonOptions.Remove(chatId);
            lastReasonMessageId.Remove(chatId);
        }
        private void ResetMedicState(long chatId)
        {
            medicChapters.Remove(chatId);
            userMedic.Remove(chatId);
            medicNumber.Remove(chatId);
            medicActiveCalls.Remove(chatId);
            selectedCall.Remove(chatId);
            lastDetailsMessageId.Remove(chatId);
            lastMenuMessageId.Remove(chatId);
        }
    }
}