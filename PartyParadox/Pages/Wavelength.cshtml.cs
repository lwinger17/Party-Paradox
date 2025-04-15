using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace PartyParadox.Pages
{
    public class WavelengthModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private static Random random = new Random();

        public WavelengthModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public string SessionCode { get; set; }

        public List<(int ID, string Name, string Image)> Users { get; set; } = new();

        public string CurrentUser { get; set; }
        public string SelectedBlindfoldedUser { get; set; }

        private int? GetSessionID(SqlConnection conn, string sessionCode, SqlTransaction transaction = null)
        {
            string query = "SELECT SessionID FROM Sessions WHERE SessionCode = @SessionCode";
            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.Add("@SessionCode", SqlDbType.Char, 6).Value = sessionCode;
                object result = cmd.ExecuteScalar();
                return result != null ? (int?)result : null;
            }
        }

        public void OnGet()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                int? sessionId = GetSessionID(conn, SessionCode);
                if (sessionId == null) return;

                int? userIdFromSession = HttpContext.Session.GetInt32("ID");
                if (userIdFromSession == null)
                {
                    Console.WriteLine("Error: User not logged in.");
                    return;
                }

                // Get CurrentUser name
                string userQuery = "SELECT Name FROM Users WHERE ID = @UserID";
                using (SqlCommand cmd = new SqlCommand(userQuery, conn))
                {
                    cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = userIdFromSession;
                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        CurrentUser = result.ToString();
                    }
                }

                // Load users in session
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

                // Randomly choose a blindfolded user
                if (Users.Count > 0)
                {
                    SelectedBlindfoldedUser = Users[random.Next(Users.Count)].Name;
                    Console.WriteLine($"Selected Blindfolded User: {SelectedBlindfoldedUser}");
                }
            }
        }

        [HttpPost]
        public IActionResult OnPostSubmitGameData([FromBody] SubmitGameDataRequest request)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    int? sessionId = GetSessionID(conn, request.SessionCode, transaction);
                    if (sessionId == null)
                    {
                        Console.WriteLine("Error: Invalid session code.");
                        return new JsonResult(new { success = false, message = "Invalid session" });
                    }

                    // Update session mode to Private if it is not already
                    string updateSessionModeQuery = "UPDATE Sessions SET PubPri = 'Private' WHERE SessionID = @SessionID AND PubPri = 'Public'";
                    using (SqlCommand updateCmd = new SqlCommand(updateSessionModeQuery, conn, transaction))
                    {
                        updateCmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        updateCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();

                    // Redirect only the blindfolded user to a different page
                    if (CurrentUser == SelectedBlindfoldedUser)
                    {
                        // You can modify the redirect logic here
                        return new JsonResult(new { success = true, redirectTo = "/BlindfoldedPage" });
                    }

                    return new JsonResult(new { success = true });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    transaction.Rollback();
                    return new JsonResult(new { success = false, message = "Error starting game" });
                }
            }
        }

        [HttpPost]
        public IActionResult OnPostLeaveGame([FromBody] LeaveGameRequest request)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    int? sessionId = GetSessionID(conn, request.SessionCode, transaction);
                    if (sessionId == null) throw new Exception("Invalid session");

                    string removeUserQuery = "UPDATE Users SET SessionID = NULL WHERE ID = @UserID AND SessionID = @SessionID";
                    using (SqlCommand cmd = new SqlCommand(removeUserQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = request.UserID;
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        cmd.ExecuteNonQuery();
                    }

                    string updateUserCountQuery = "UPDATE Sessions SET UserCount = UserCount - 1 WHERE SessionID = @SessionID";
                    using (SqlCommand cmd = new SqlCommand(updateUserCountQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        cmd.ExecuteNonQuery();
                    }

                    string userCountQuery = "SELECT COUNT(*) FROM Users WHERE SessionID = @SessionID";
                    int userCount;
                    using (SqlCommand cmd = new SqlCommand(userCountQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        userCount = (int)cmd.ExecuteScalar();
                    }

                    if (userCount == 0)
                    {
                        string deleteSessionQuery = "DELETE FROM Sessions WHERE SessionID = @SessionID";
                        using (SqlCommand cmd = new SqlCommand(deleteSessionQuery, conn, transaction))
                        {
                            cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                            cmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                    return new JsonResult(new { success = true });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    transaction.Rollback();
                    return new JsonResult(new { success = false, message = "Error leaving game" });
                }
            }
        }

        public class SubmitGameDataRequest
        {
            public string SessionCode { get; set; }
            public List<UserSessionData> Users { get; set; }
        }

        public class UserSessionData
        {
            public int UserID { get; set; }
        }

        public class LeaveGameRequest
        {
            public string SessionCode { get; set; }
            public int UserID { get; set; }
        }
    }
}
