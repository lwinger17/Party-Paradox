using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PartyParadox.Pages
{
    public class LikeViewModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LikeViewModel> _logger;

        public LikeViewModel(IConfiguration configuration, ILogger<LikeViewModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string SessionCode { get; set; }

        public int SessionID { get; private set; }
        public int? UserID { get; set; }
        public string UserName { get; set; }
        public string CurrentUser { get; set; }
        public List<UserResponse> UserResponses { get; set; } = new(); // Store responses

        public List<(int ID, string Name, string Image)> Users { get; private set; } = new();
        public string Word1 { get; set; }
        public string Word2 { get; set; }
        public int AnswerID { get; set; }
        public string AnswerText { get; set; }

        public void OnGet()
        {
            _logger.LogInformation("OnGet() started.");
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    _logger.LogInformation("Database connection opened.");

                    // Get SessionID using SessionCode
                    int? sessionId = GetSessionID(conn, SessionCode);
                    if (sessionId == null)
                    {
                        _logger.LogWarning($"SessionID not found for SessionCode: {SessionCode}");
                        return;
                    }

                    SessionID = sessionId.Value;
                    _logger.LogInformation($"SessionID retrieved: {SessionID}");

                    // Retrieve UserID from session
                    UserID = HttpContext.Session.GetInt32("ID");
                    if (UserID == null)
                    {
                        _logger.LogError("UserID is null. User might not be logged in.");
                        return;
                    }

                    _logger.LogInformation($"UserID: {UserID}");
                    _logger.LogInformation($"SessionID Retrieved: {SessionID}");


                    // Retrieve user's name based on UserID
                    string userQuery = "SELECT Name FROM Users WHERE ID = @UserID";
                    using (SqlCommand cmd = new SqlCommand(userQuery, conn))
                    {
                        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = UserID.Value;
                        var result = cmd.ExecuteScalar();
                        CurrentUser = result?.ToString() ?? "Unknown User";
                        _logger.LogInformation($"CurrentUser: {CurrentUser}");
                    }

                    // Retrieve users in the session
                    string usersQuery = @"SELECT ID, Name, Image FROM Users WHERE SessionID = @SessionID ORDER BY ID ASC";
                    using (SqlCommand cmd = new SqlCommand(usersQuery, conn))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;

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

                    _logger.LogInformation($"Users retrieved: {Users.Count}");


                    // Retrieve answers from LikeabilityAns table for the current session
                    string answersQuery = @"
                        SELECT la.UserID, u.Name, la.AnswerText, la.Word1, la.Word2 
                        FROM LikeabilityAns la
                        JOIN Users u ON la.UserID = u.ID
                        WHERE la.SessionID = @SessionID";

                    using (SqlCommand cmd = new SqlCommand(answersQuery, conn))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int userId = reader.GetInt32(0);
                                string userName = reader.GetString(1);
                                string answerText = reader.GetString(2);
                                string word1 = reader.GetString(3);
                                string word2 = reader.GetString(4);

                                // Logging retrieved data
                                _logger.LogInformation($"User: {userName}, Answer: {answerText}, Word1: {word1}, Word2: {word2}");

                                // Add the response to the list
                                UserResponses.Add(new UserResponse
                                {
                                    UserID = userId,
                                    UserName = userName,
                                    AnswerText = answerText,
                                    Word1 = word1,
                                    Word2 = word2
                                });

                                Word1 = word1;
                                Word2 = word2;
                                _logger.LogInformation($"Updated Model.Word1: {Word1}, Model.Word2: {Word2}");


                            }
                        }
                    }

                    _logger.LogInformation($"Total responses retrieved: {UserResponses.Count}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnGet: {ex.Message}");
                Console.WriteLine($"Error in OnGet: {ex.Message}");
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

        public class UserResponse
        {
            public int UserID { get; set; }
            public string UserName { get; set; }
            public string AnswerText { get; set; }
            public string Word1 { get; set; }
            public string Word2 { get; set; }
        }
    }
}
