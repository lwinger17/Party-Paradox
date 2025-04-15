using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace PartyParadox.Pages
{
    public class JoinGameModel : PageModel
    {
        private readonly ILogger<JoinGameModel> _logger;
        private readonly IConfiguration _configuration;

        public JoinGameModel(ILogger<JoinGameModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [BindProperty]
        public string LobbyCode { get; set; }

        public string ErrorMessage { get; set; }

        public List<GameSession> PublicGames { get; set; } = new List<GameSession>();

        public void OnGet()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
            SELECT SessionID, SessionName, SessionCode, UserCount, RoomSize, GameChoice 
            FROM [dbo].[Sessions] 
            WHERE PubPri = 'Public' AND UserCount < RoomSize";  // Filter out full games

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                PublicGames.Add(new GameSession
                                {
                                    SessionID = reader.GetInt32(0),
                                    SessionName = reader.GetString(1),
                                    SessionCode = reader.GetString(2),
                                    UserCount = reader.GetInt32(3),
                                    RoomSize = reader.GetInt32(4),
                                    GameChoice = reader.GetString(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching public games: {ex.Message}");
            }
        }

        public class GameSession
        {
            public int SessionID { get; set; }
            public string SessionName { get; set; }
            public string SessionCode { get; set; }
            public int UserCount { get; set; }
            public int RoomSize { get; set; }
            public string GameChoice { get; set; }
        }

       
    }
}
