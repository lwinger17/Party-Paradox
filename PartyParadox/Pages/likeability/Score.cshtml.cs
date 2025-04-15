using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PartyParadox.Pages
{
    public class LikeScoreModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LikeScoreModel> _logger;

        public LikeScoreModel(IConfiguration configuration, ILogger<LikeScoreModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public class RankedUser
        {
            public int UserID { get; set; }
            public string UserName { get; set; }
            public string UserImage { get; set; }
            public int Points { get; set; }
            public int Rank { get; set; }
        }

        [BindProperty(SupportsGet = true)]
        public string SessionCode { get; set; }

        public int SessionID { get; private set; }
        public int? UserID { get; set; }
        public string UserName { get; set; }
        public string CurrentUser { get; set; }
        public List<RankedUser> RankedUsers { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            _logger.LogInformation($"Retrieving session ID for SessionCode: {SessionCode}");

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    _logger.LogInformation("Database connection opened successfully.");

                    // Get SessionID from SessionCode
                    int? sessionId = GetSessionID(conn, SessionCode);
                    if (sessionId == null)
                    {
                        _logger.LogWarning($"SessionID not found for SessionCode: {SessionCode}");
                        return NotFound(new { success = false, message = "Invalid session." });
                    }

                    SessionID = sessionId.Value;
                    _logger.LogInformation($"SessionID retrieved: {SessionID}");

                    // Retrieve rankings for the session
                    string query = @"
            SELECT u.ID AS UserID, u.Name, u.Image, SUM(la.Points) AS TotalPoints
            FROM LikeabilityAns la
            JOIN Users u ON la.UserID = u.ID
            WHERE la.SessionID = @SessionID
            GROUP BY u.ID, u.Name, u.Image
            ORDER BY TotalPoints DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@SessionID", SessionID);
                        _logger.LogInformation("Executing SQL query...");

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            int rank = 1;
                            while (reader.Read())
                            {
                                var userId = reader.GetInt32(0);
                                var userName = reader.GetString(1);
                                var dbImagePath = reader.IsDBNull(2) ? "userImgs/Default.png" : reader.GetString(2);
                                var userImage = $"../{dbImagePath}";  // Ensures correct path without duplication
                                var totalPoints = reader.GetInt32(3);

                                _logger.LogInformation($"User Retrieved - ID: {userId}, Name: {userName}, Points: {totalPoints}, Rank: {rank}");

                                RankedUsers.Add(new RankedUser
                                {
                                    UserID = userId,
                                    UserName = userName,
                                    UserImage = userImage,
                                    Points = totalPoints,
                                    Rank = rank++
                                });
                            }
                        }
                    }

                    _logger.LogInformation($"Successfully retrieved {RankedUsers.Count} users for session {SessionID}.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving rankings: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Server error." });
            }
            return Page();
        }


        private int? GetSessionID(SqlConnection conn, string sessionCode)
        {
            string query = "SELECT SessionID FROM Sessions WHERE SessionCode = @SessionCode";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@SessionCode", SqlDbType.NVarChar, 6).Value = sessionCode;
                var result = cmd.ExecuteScalar();
                return result != null ? (int?)result : null;
            }
        }
    }
}
