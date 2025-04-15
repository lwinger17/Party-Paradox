using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System;

namespace PartyParadox.Pages
{
    public class ChatModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ChatModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public string SessionCode { get; set; }

        [BindProperty(SupportsGet = true)]
        public int UserID { get; set; }

        public List<(string UserName, string ChatColor, string Message, DateTime Timestamp)> Messages { get; set; } = new();

        // Helper function to get SessionID using SessionCode
        private int? GetSessionID(SqlConnection conn, string sessionCode)
        {
            string query = "SELECT SessionID FROM Sessions WHERE SessionCode = @SessionCode";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@SessionCode", SqlDbType.Char, 6).Value = sessionCode;
                object result = cmd.ExecuteScalar();
                return result != null ? (int?)result : null;
            }
        }

        #region GET Functions

        // Function to retrieve all messages in the session
        public void OnGet()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Get SessionID using SessionCode
                int? sessionId = GetSessionID(conn, SessionCode);
                if (sessionId == null) return;

                // Retrieve messages for the current session
                string query = @"SELECT u.Name, u.ChatColor, c.Message, c.Timestamp 
                                 FROM Chat c
                                 JOIN Users u ON c.SenderID = u.ID
                                 WHERE c.SessionID = @SessionID 
                                 ORDER BY c.Timestamp ASC";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string userName = reader.GetString(0);
                            string chatColor = reader.GetString(1);
                            string message = reader.GetString(2);
                            DateTime timestamp = reader.GetDateTime(3);
                            Messages.Add((userName, chatColor, message, timestamp));
                        }
                    }
                }
            }
        }

        #endregion

        #region POST Functions

        // Function to send a new message to the chat
        [HttpPost]
        public IActionResult OnPostSendMessage([FromBody] SendMessageRequest request)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Get the SessionID using the SessionCode
                    int? sessionId = GetSessionID(conn, request.SessionCode);
                    if (sessionId == null)
                    {
                        return new JsonResult(new { success = false, message = "Invalid session" });
                    }

                    // Insert the message into the Chat table
                    string insertQuery = @"INSERT INTO Chat (SenderID, SessionID, Message) 
                                           VALUES (@SenderID, @SessionID, @Message)";
                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@SenderID", SqlDbType.Int).Value = request.UserID;
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        cmd.Parameters.Add("@Message", SqlDbType.NVarChar).Value = request.Message;

                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected <= 0)
                        {
                            return new JsonResult(new { success = false, message = "Failed to send message" });
                        }
                    }

                    transaction.Commit();
                    return new JsonResult(new { success = true });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return new JsonResult(new { success = false, message = ex.Message });
                }
            }
        }

        // Function to handle deleting messages when the session is deleted
        [HttpPost]
        public IActionResult OnPostDeleteSession([FromBody] DeleteSessionRequest request)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Get SessionID using the SessionCode
                    int? sessionId = GetSessionID(conn, request.SessionCode);
                    if (sessionId == null)
                    {
                        return new JsonResult(new { success = false, message = "Invalid session" });
                    }

                    // Delete messages associated with this session
                    string deleteMessagesQuery = "DELETE FROM Chat WHERE SessionID = @SessionID";
                    using (SqlCommand cmd = new SqlCommand(deleteMessagesQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        cmd.ExecuteNonQuery();
                    }

                    // Delete the session itself
                    string deleteSessionQuery = "DELETE FROM Sessions WHERE SessionID = @SessionID";
                    using (SqlCommand cmd = new SqlCommand(deleteSessionQuery, conn, transaction))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = sessionId;
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return new JsonResult(new { success = true });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return new JsonResult(new { success = false, message = ex.Message });
                }
            }
        }

        #endregion

        #region Request Classes

        public class SendMessageRequest
        {
            public string SessionCode { get; set; }
            public int UserID { get; set; }
            public string Message { get; set; }
        }

        public class DeleteSessionRequest
        {
            public string SessionCode { get; set; }
        }

        #endregion
    }
}
