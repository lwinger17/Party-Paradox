using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using static PartyParadox.Pages.LikeVoteModel;

namespace PartyParadox.Pages
{
    public class LikeVoteModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LikeVoteModel> _logger;

        public LikeVoteModel(IConfiguration configuration, ILogger<LikeVoteModel> logger)
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

        public class UserResponse
        {
            public int AnswerID { get; set; }
            public int UserID { get; set; }
            public string UserName { get; set; }
            public string AnswerText { get; set; }
            public int Vote { get; set; } // Ensure this property exists
        }

        public class VoteData
        {
            public Dictionary<int, Vote> Votes { get; set; } = new();
        }

        public class Vote
        {
            public string UserName;
            public int Votes;
        }
        public class VoteRequestModel
        {
            public int AnswerID { get; set; }
            public int VoteCount { get; set; }
        }

        public List<UserResponse> UserResponses { get; set; } = new();
        public Dictionary<int, int> VoteResults { get; set; } = new();

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

                    int? sessionId = GetSessionID(conn, SessionCode);
                    if (sessionId == null)
                    {
                        _logger.LogWarning($"SessionID not found for SessionCode: {SessionCode}");
                        return;
                    }
                    SessionID = sessionId.Value;
                    _logger.LogInformation($"SessionID retrieved: {SessionID}");

                    UserID = HttpContext.Session.GetInt32("ID");
                    if (UserID == null)
                    {
                        _logger.LogError("UserID is null. User might not be logged in.");
                        return;
                    }
                    _logger.LogInformation($"UserID: {UserID}");

                    string userQuery = "SELECT Name FROM Users WHERE ID = @UserID";
                    using (SqlCommand cmd = new SqlCommand(userQuery, conn))
                    {
                        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = UserID.Value;
                        var result = cmd.ExecuteScalar();
                        CurrentUser = result?.ToString() ?? "Unknown User";
                        _logger.LogInformation($"CurrentUser: {CurrentUser}");
                    }

                    string answersQuery = @"
                    SELECT la.AnswerID, la.UserID, u.Name, la.AnswerText, la.Vote, la.Points 
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
                                int answerId = reader.GetInt32(0);
                                int userId = reader.GetInt32(1);
                                string userName = reader.GetString(2);
                                string answerText = reader.GetString(3);
                                int voteCount = reader.GetInt32(4);

                                _logger.LogInformation($"AnswerID: {answerId}, User: {userName}, Answer: {answerText}, Votes: {voteCount}");

                                UserResponses.Add(new UserResponse
                                {
                                    AnswerID = answerId,
                                    UserID = userId,
                                    UserName = userName,
                                    AnswerText = answerText,
                                    Vote = voteCount
                                });
                                VoteResults[answerId] = voteCount;
                            }
                        }
                    }
                    _logger.LogInformation($"Total responses retrieved: {UserResponses.Count}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnGet: {ex.Message}");
            }
        }

        public void OnPost([FromQuery] string sessionCode, [FromForm] int AnswerID)
        {
            _logger.LogInformation($"Received vote request for session: {sessionCode}, AnswerID: {AnswerID}");

            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Update Vote and Points together
                    string updateQuery = "UPDATE LikeabilityAns SET Vote = Vote + 1, Points = Points + 1000 WHERE AnswerID = @AnswerID";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.Add("@AnswerID", SqlDbType.Int).Value = AnswerID;
                        cmd.ExecuteNonQuery();

                        _logger.LogInformation($"Updated Vote and Points for AnswerID: {AnswerID}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnPost: {ex.Message}");
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
    }
}