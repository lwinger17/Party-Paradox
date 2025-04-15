using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.Data.SqlClient;
using System.Text;

namespace PartyParadox.Pages
{
    public class CreateGame : PageModel
    {
        private readonly ILogger<CreateGame> _logger;
        private readonly IConfiguration _configuration;

        public CreateGame(ILogger<CreateGame> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        [BindProperty]
        public string RoomTitle { get; set; }

        [BindProperty]
        public string GameChoice { get; set; }

        [BindProperty]
        public int RoomSize { get; set; }

        [BindProperty]
        public string Privacy { get; set; }

        public int? UserID { get; set; }
        public string UserName { get; set; }

        public void OnGet()
        {
            RoomSize = 4; // Default room size
            Privacy = "public"; // Default privacy setting

            // Retrieve UserID and UserName from session
            UserID = HttpContext.Session.GetInt32("ID");
            UserName = HttpContext.Session.GetString("UserName");
            _logger.LogInformation($"UserID retrieved from session: {UserID}, UserName: {UserName}");
        }


        public IActionResult OnPost()
{
    if (string.IsNullOrEmpty(RoomTitle) || string.IsNullOrEmpty(GameChoice))
    {
        ModelState.AddModelError(string.Empty, "Please fill in all fields.");
        return BadRequest(new { error = "Please fill in all fields." });
    }

    if (Privacy != "public" && Privacy != "private")
    {
        return BadRequest(new { error = "Invalid privacy setting." });
    }

    int userId = HttpContext.Session.GetInt32("ID") ?? -1;
    if (userId == -1)
    {
                return new UnauthorizedObjectResult(new { error = "User not logged in. Please log in again." });
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

    try
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();
            using (SqlTransaction transaction = conn.BeginTransaction())
            {
                try
                {
                    string sessionCode = GenerateUniqueSessionCode(conn, transaction);
                    int sessionId;

                    Console.WriteLine($"UserID set in session: {userId}");
                   

                    string insertSessionQuery = @"
                    INSERT INTO [dbo].[Sessions] (SessionName, PubPri, SessionCode, UserCount, RoomSize, GameChoice) 
                    OUTPUT INSERTED.SessionID
                    VALUES (@SessionName, @PubPri, @SessionCode, 1, @RoomSize, @GameChoice)";

                    using (SqlCommand cmd = new SqlCommand(insertSessionQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SessionName", RoomTitle);
                        cmd.Parameters.AddWithValue("@PubPri", Privacy);
                        cmd.Parameters.AddWithValue("@SessionCode", sessionCode);
                        cmd.Parameters.AddWithValue("@RoomSize", RoomSize);
                        cmd.Parameters.AddWithValue("@GameChoice", GameChoice);

                        sessionId = (int)cmd.ExecuteScalar();
                    }

                    HttpContext.Session.SetString("SessionCode", sessionCode);
                    HttpContext.Session.SetInt32("SessionID", sessionId);

                    string updateUserSessionQuery = @"
                    UPDATE [dbo].[Users] 
                    SET SessionID = @SessionID 
                    WHERE ID = @UserID";

                    using (SqlCommand cmd = new SqlCommand(updateUserSessionQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@SessionID", sessionId);
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    return new JsonResult(new { sessionCode, gameChoice = GameChoice });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError($"Transaction failed: {ex.Message}");
                    return StatusCode(500, new { error = "Error creating session. Please try again." });
                }
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Database connection error: {ex.Message}");
        return StatusCode(500, new { error = "Database error. Please try again later." });
    }
}



        private string GenerateUniqueSessionCode(SqlConnection conn, SqlTransaction transaction)
        {
            string sessionCode;
            bool isUnique;

            do
            {
                sessionCode = GenerateSessionCode();
                isUnique = CheckSessionCodeUnique(conn, sessionCode, transaction); // ✅ Pass transaction
            } while (!isUnique);

            return sessionCode;
        }


        private bool CheckSessionCodeUnique(SqlConnection conn, string sessionCode, SqlTransaction transaction)
        {
            string query = "SELECT COUNT(*) FROM [dbo].[Sessions] WHERE SessionCode = @SessionCode";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@SessionCode", sessionCode);
                int count = (int)cmd.ExecuteScalar();
                return count == 0; // Return true if no duplicates found
            }
        }


        private string GenerateSessionCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            StringBuilder result = new StringBuilder(6);
            Random random = new Random();

            for (int i = 0; i < 6; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return result.ToString();
        }
    }
}
