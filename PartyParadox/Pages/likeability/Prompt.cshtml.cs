using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;

namespace PartyParadox.Pages
{
    public class LikePromptModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LikePromptModel> _logger;

        public LikePromptModel(IConfiguration configuration, ILogger<LikePromptModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string SessionCode { get; set; }

        public int SessionID { get; private set; }

        public int? UserID { get; set; }
        public string UserName { get; set; }

        public List<(int ID, string Name, string Image)> Users { get; private set; } = new();
        public List<(int WordID, string Word)> UserWords { get; private set; } = new();

        public void OnGet()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();


                // Get SessionID using SessionCode
                int? sessionId = GetSessionID(conn, SessionCode);
                if (sessionId == null) return;

                // Log session data when accessing the Likeability page
               UserID = HttpContext.Session.GetInt32("ID");
                if (UserID == null)
                {
                    // Handle case where user is not logged in
                    Console.WriteLine("Error: User not logged in.");
                    return;
                }

                Console.WriteLine($"OnGet: Accessing session {SessionCode} with SessionID = {sessionId}. UserID from session = {UserID}");

                // Retrieve users in the session
                string usersQuery = @"SELECT ID, Name, Image FROM Users WHERE SessionID = @SessionID ORDER BY ID ASC";
                using (SqlCommand cmd = new SqlCommand(usersQuery, conn))
                {
                    cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string name = reader.GetString(1);
                            string image = reader.GetString(2);
                            Users.Add((id, name, image));
                        }
                    }
                }
            }
        }


        public IActionResult OnPost(string Word)
        {
            try
            {
                _logger.LogInformation("LikePrompt OnPost started");
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    int? sessionId = GetSessionID(conn, SessionCode);
                    if (sessionId == null || sessionId == 0)
                    {
                        _logger.LogWarning("Session not found for code: {SessionCode}", SessionCode);
                        return NotFound(new { success = false, message = "Invalid session." });
                    }

                    SessionID = sessionId.Value;
                    _logger.LogInformation("SessionID: {SessionID}, UserID: {UserID}, Word: {Word}", SessionID, UserID, Word);

                    UserID = HttpContext.Session.GetInt32("ID");
                    if (UserID == 0)
                    {
                        _logger.LogError("UserID is missing. Possible session issue.");
                        return BadRequest(new { success = false, message = "User authentication failed." });
                    }

                    string insertQuery = @"
                INSERT INTO Likeability (SessionID, UserID, Word) 
                OUTPUT INSERTED.WordID 
                VALUES (@SessionID, @UserID, @Word)";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;
                        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = UserID;
                        cmd.Parameters.Add("@Word", SqlDbType.NVarChar, 50).Value = Word;

                        object result = cmd.ExecuteScalar();

                        if (result != null && int.TryParse(result.ToString(), out int wordID))
                        {
                            _logger.LogInformation("Word inserted successfully! WordID: {WordID}", wordID);
                            return new JsonResult(new { success = true, message = "Word submitted successfully!", wordID, word = Word });
                        }
                        else
                        {
                            _logger.LogError("Insert failed for Word: {Word}", Word);
                            return StatusCode(500, new { success = false, message = "Failed to insert word." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred in OnPost");
                return StatusCode(500, new { success = false, message = "Server error." });
            }
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

        public IActionResult OnGetCheckSession()
        {
            int? userID = HttpContext.Session.GetInt32("UserID");
            _logger.LogInformation("UserID from session: {UserID}", userID);
            return Content($"UserID from session: {userID}");
        }
    }
}
