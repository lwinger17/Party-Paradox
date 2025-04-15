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
    public class LikeAnsModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LikeAnsModel> _logger;

        public LikeAnsModel(IConfiguration configuration, ILogger<LikeAnsModel> logger)
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

        public List<(int ID, string Name, string Image)> Users { get; private set; } = new();
        public string Word1 { get; set; }
        public string Word2 { get; set; }
        public string UserResponse { get; set; }
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

                    // Log session data
                    UserID = HttpContext.Session.GetInt32("ID");
                    if (UserID == null)
                    {
                        _logger.LogError("UserID is null. User might not be logged in.");
                        return;
                    }

                    _logger.LogInformation($"UserID: {UserID}");

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

                    // Add a fake user if needed
                    if (Users.Count < 2)
                    {
                        _logger.LogWarning("Not enough users to get two different words. Adding a fake user.");
                        // Adding a fake user with ID -1 and test words
                        Users.Add((-1, "TestUser", "default.png"));
                        // Set words for the fake user
                        Word1 = "TestWord1";
                        Word2 = "TestWord2";
                        _logger.LogInformation($"Assigned test words to fake user: {Word1}, {Word2}");
                    }

                    // Select two different users
                    var random = new Random();
                    var randomUser1 = Users[random.Next(Users.Count)];
                    var randomUser2 = Users[random.Next(Users.Count)];

                    // Ensure that the two selected users are different
                    while (randomUser1.ID == randomUser2.ID)
                    {
                        randomUser2 = Users[random.Next(Users.Count)];
                    }

                    _logger.LogInformation($"Random users selected: {randomUser1.Name} and {randomUser2.Name}");

                    // Get words from two different users
                    Word1 = randomUser1.ID != -1 ? GetRandomWordFromUser(conn, randomUser1.ID) : "TestWord1";
                    Word2 = randomUser2.ID != -1 ? GetRandomWordFromUser(conn, randomUser2.ID) : "TestWord2"; // Fake user gets "TestWord2"

                    _logger.LogInformation($"Random words retrieved: {Word1}, {Word2}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnGet: {ex.Message}");
                Console.WriteLine($"Error in OnGet: {ex.Message}");
            }
        }



        public IActionResult OnPost(string Word1, string Word2, string UserResponse)
        {
            try
            {
                _logger.LogInformation("LikeAns OnPost started");
                string connectionString = _configuration.GetConnectionString("DefaultConnection");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Ensure sessionId is retrieved correctly from SessionCode
                    int? sessionId = GetSessionID(conn, SessionCode);
                    if (sessionId == null || sessionId == 0)
                    {
                        _logger.LogWarning("Session not found for code: {SessionCode}", SessionCode);
                        return NotFound(new { success = false, message = "Invalid session." });
                    }

                    SessionID = sessionId.Value;
                    _logger.LogInformation("SessionID: {SessionID}, UserID: {UserID}, Word1: {Word1}, Word2: {Word2}", SessionID, UserID, Word1, Word2);

                    // Retrieve UserID from session
                    UserID = HttpContext.Session.GetInt32("ID");
                    if (UserID == null || UserID == 0)
                    {
                        _logger.LogError("UserID is missing. Possible session issue.");
                        return BadRequest(new { success = false, message = "User authentication failed." });
                    }

                    // Insert the answer into the database
                    string insertQuery = @"
            INSERT INTO LikeabilityAns (SessionID, UserID, Word1, Word2, AnswerText) 
            OUTPUT INSERTED.AnswerID 
            VALUES (@SessionID, @UserID, @Word1, @Word2, @AnswerText)";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        // Set up the parameters for the SQL command
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;
                        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = UserID;
                        cmd.Parameters.Add("@Word1", SqlDbType.NVarChar, 50).Value = Word1;  // Use Word1 from OnGet
                        cmd.Parameters.Add("@Word2", SqlDbType.NVarChar, 50).Value = Word2;  // Use Word2 from OnGet
                        cmd.Parameters.Add("@AnswerText", SqlDbType.NVarChar, -1).Value = UserResponse; // User's response

                        // Execute and get the inserted AnswerID
                        object result = cmd.ExecuteScalar();

                        if (result != null && int.TryParse(result.ToString(), out int answerID))
                        {
                            _logger.LogInformation("Answer inserted successfully! AnswerID: {AnswerID}", answerID);
                            return new JsonResult(new { success = true, message = "Answer submitted successfully!", AnswerID = answerID, UserResponse = UserResponse });
                        }
                        else
                        {
                            _logger.LogError("Insert failed for Word1: {Word1}, Word2: {Word2}", Word1, Word2);
                            return StatusCode(500, new { success = false, message = "Failed to insert answer." });
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




        private string GetRandomWordFromUser(SqlConnection conn, int userId)
        {
            try
            {
                string query = "SELECT TOP 1 Word FROM Likeability WHERE UserID = @UserID ORDER BY NEWID()";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                    var result = cmd.ExecuteScalar();
                    string word = result != null ? result.ToString() : string.Empty;

                    _logger.LogInformation($"GetRandomWordFromUser: UserID {userId}, Word {word}");
                    return word;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetRandomWordFromUser: {ex.Message}");
                return string.Empty;
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
