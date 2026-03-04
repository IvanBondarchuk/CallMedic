using System.Threading;
using MySql.Data.MySqlClient;

namespace CallMedic
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
               string connectionString =
              "server=localhost;" +
              "port=3306;" +
              "database=chat_bot_medic;" +  
              "uid=root;" +                   
              "password=;";         

               var connection = new MySqlConnection(connectionString);
               connection.Open();


            using var cts = new CancellationTokenSource();
            var bot = new CallMedicBotHandler(connection);
            await bot.StartBot("", cts.Token);
            Console.ReadLine();
            cts.Cancel();
        }
    }
}