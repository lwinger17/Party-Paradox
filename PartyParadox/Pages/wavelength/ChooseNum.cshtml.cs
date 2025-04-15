using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;

namespace PartyParadox.Pages
{
    public class WaveChooseNumModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<WaveChooseNumModel> _logger;

        public WaveChooseNumModel(IConfiguration configuration, ILogger<WaveChooseNumModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // Session Code comes from query string
        [BindProperty(SupportsGet = true)]
        public string SessionCode { get; set; }

        // To hold form input when user submits a number
        [BindProperty]
        public int UserResponse { get; set; }

        public int SessionID { get; private set; }

        // Use a proper class instead of a tuple
        public UserInfo BlindedUser { get; private set; }
        public List<UserInfo> Users { get; private set; } = new List<UserInfo>();

        // GET handler
        public void OnGet()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                int? sessionId = GetSessionID(conn, SessionCode);
                if (sessionId == null) return;

                SessionID = sessionId.Value;

                // Get the blinded user (Blind = 1)
                string blindQuery = @"SELECT TOP 1 u.ID, u.Name, u.Image
                                      FROM Users u
                                      JOIN Ratings r ON r.UserID = u.ID
                                      WHERE r.SessionID = @SessionID AND r.Blind = 1";
                using (SqlCommand cmd = new SqlCommand(blindQuery, conn))
                {
                    cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            BlindedUser = new UserInfo
                            {
                                UserID = reader.GetInt32(0),
                                UserName = reader.GetString(1),
                                UserImage = reader.GetString(2)
                            };
                        }
                    }
                }

                // Get other users (non-blinded), filtering out any user with Blind = 1
                string query = @"SELECT DISTINCT u.ID, u.Name, u.Image 
                                 FROM Users u
                                 JOIN Ratings r ON r.UserID = u.ID
                                 WHERE r.SessionID = @SessionID AND (r.Blind IS NULL OR r.Blind = 0)";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var user = new UserInfo
                            {
                                UserID = reader.GetInt32(0),
                                UserName = reader.GetString(1),
                                UserImage = reader.GetString(2)
                            };
                            Users.Add(user);
                        }
                    }
                }
            }
        }

        // POST: Standard form submission to store user's answer
        public IActionResult OnPost()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Replace the UserID value below with your current user logic
                int currentUserID = 1; // TODO: Replace with actual current user identification

                // Insert the response into Ratings table for all users in the session
                string insertQuery = @"
                    INSERT INTO Ratings (SessionID, UserID, AnswerChoice)
                    VALUES (@SessionID, @UserID, @AnswerChoice)";

                // First, get all users in the session
                List<int> userIds = new List<int>();
                string usersQuery = @"SELECT ID FROM Users WHERE SessionID = @SessionID";
                using (SqlCommand cmd = new SqlCommand(usersQuery, conn))
                {
                    cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            userIds.Add(reader.GetInt32(0));
                        }
                    }
                }

                // Insert the answer for each user
                foreach (int userId in userIds)
                {
                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;
                        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userId;
                        cmd.Parameters.Add("@AnswerChoice", SqlDbType.NVarChar).Value = UserResponse.ToString();
                        cmd.ExecuteNonQuery();
                    }
                }

                // Redirect to another page after posting (adjust if necessary)
                return RedirectToPage("/wavelength/AnswerU", new { SessionID });
            }
        }

        // Helper method to get SessionID from SessionCode
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

    // A simple class to model a user's information.
    public class UserInfo
    {
        public int UserID { get; set; }
        public string UserName { get; set; }
        public string UserImage { get; set; }
    }

    // These models remain for any JSON-based endpoints you may need:
    public class UserAnswerModel
    {
        public int SessionID { get; set; }
        public int UserID { get; set; }
        public string AnswerChoice { get; set; }
    }

    public class SessionModel
    {
        public int SessionID { get; set; }
    }
}
