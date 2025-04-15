using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System;

namespace PartyParadox.Pages
{
    public class LikeabilityModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public LikeabilityModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public string SessionCode { get; set; }

        public List<(int ID, string Name, string Image)> Users { get; set; } = new();

        // Helper function to get SessionID safely
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

        #region GET Functions

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
                int? userIdFromSession = HttpContext.Session.GetInt32("ID");
                if (userIdFromSession == null)
                {
                    // Handle case where user is not logged in
                    Console.WriteLine("Error: User not logged in.");
                    return;
                }

                Console.WriteLine($"OnGet: Accessing session {SessionCode} with SessionID = {sessionId}. UserID from session = {userIdFromSession}");

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



        #endregion

        #region POST Functions

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
                    // ✅ Get SessionID using SessionCode
                    int? sessionId = GetSessionID(conn, request.SessionCode, transaction);
                    if (sessionId == null)
                    {
                        Console.WriteLine("Error: Invalid session code.");
                        return new JsonResult(new { success = false, message = "Invalid session" });
                    }
                    Console.WriteLine($"SessionID retrieved: {sessionId}");

                    // ✅ Change session mode to "Private" if it is currently "Public"
                    string updateSessionModeQuery = "UPDATE Sessions SET PubPri = 'Private' WHERE SessionID = @SessionID AND PubPri = 'Public'";
                    using (SqlCommand updateCmd = new SqlCommand(updateSessionModeQuery, conn, transaction))
                    {
                        updateCmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        int rowsUpdated = updateCmd.ExecuteNonQuery();
                        Console.WriteLine(rowsUpdated > 0 ? "Game mode changed to Private." : "Game was already Private.");
                    }

                    // ✅ Retrieve all users in the session
                    string getAllUsersQuery = "SELECT ID FROM Users WHERE SessionID = @SessionID";
                    List<int> userIdsInSession = new List<int>();
                    using (SqlCommand cmd = new SqlCommand(getAllUsersQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                userIdsInSession.Add(reader.GetInt32(0)); // Store UserID
                            }
                        }
                    }

                   

                    transaction.Commit();
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





        // Function to handle a user leaving the game
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

                    // Remove user from session
                    string removeUserQuery = "UPDATE Users SET SessionID = NULL WHERE ID = @UserID AND SessionID = @SessionID";
                    using (SqlCommand cmd = new SqlCommand(removeUserQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = request.UserID;
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        cmd.ExecuteNonQuery();
                    }

                    // Decrease UserCount by 1
                    string updateUserCountQuery = "UPDATE Sessions SET UserCount = UserCount - 1 WHERE SessionID = @SessionID";
                    using (SqlCommand cmd = new SqlCommand(updateUserCountQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        cmd.ExecuteNonQuery();
                    }

                    // Check if session is now empty
                    string userCountQuery = "SELECT COUNT(*) FROM Users WHERE SessionID = @SessionID";
                    int userCount;
                    using (SqlCommand cmd = new SqlCommand(userCountQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        userCount = (int)cmd.ExecuteScalar();
                    }

                    // If no users remain, delete the session
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

        #endregion

        #region Request Classes

        // Class to represent the request data for starting the game
        public class SubmitGameDataRequest
        {
            public string SessionCode { get; set; }
            public List<UserSessionData> Users { get; set; }
        }

        // Class to represent each user's session data
        public class UserSessionData
        {
            public int UserID { get; set; }
        }

        // Class to represent the request data when a user leaves the game
        public class LeaveGameRequest
        {
            public string SessionCode { get; set; }
            public int UserID { get; set; }
        }

        #endregion
    }
}
