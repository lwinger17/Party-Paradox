using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace PartyParadox.Pages.Wavelength
{
    public class WaveBlindPromptModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<WaveBlindPromptModel> _logger;

        public WaveBlindPromptModel(IConfiguration configuration, ILogger<WaveBlindPromptModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string SessionCode { get; set; }
        public int SessionID { get; private set; }
        public UserInfo BlindedUser { get; private set; }
        public List<UserInfo> Users { get; private set; } = new();

        // GET
        public void OnGet(string sessionCode)
        {
            SessionCode = sessionCode;

            using (var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                string userQuery = "SELECT ID, UserName, UserImage, IsBlinded FROM Users WHERE SessionCode = @SessionCode";
                using (var cmd = new SqlCommand(userQuery, conn))
                {
                    cmd.Parameters.Add("@SessionCode", SqlDbType.NVarChar, 6).Value = sessionCode;
                    using (var reader = cmd.ExecuteReader())
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

                            bool isBlinded = !reader.IsDBNull(3) && reader.GetBoolean(3);
                            if (isBlinded)
                            {
                                BlindedUser = user;
                            }
                        }
                    }
                }

                conn.Close();
            }
        }

        // POST
        public JsonResult OnPost(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                return new JsonResult(new { success = false, message = "Prompt cannot be empty!" });
            }

            var sessionCode = Request.Query["sessionCode"].ToString();
            SessionCode = sessionCode;

            int? sessionId = null;
            using (var conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                sessionId = GetSessionID(conn, sessionCode);
                if (sessionId == null)
                    return new JsonResult(new { success = false, message = "Invalid session code." });

                SessionID = sessionId.Value;

                // Get all users and blinded user
                Dictionary<int, bool> userBlindFlags = new();
                string userQuery = "SELECT ID, IsBlinded FROM Users WHERE SessionCode = @SessionCode";
                using (SqlCommand cmd = new SqlCommand(userQuery, conn))
                {
                    cmd.Parameters.Add("@SessionCode", SqlDbType.NVarChar, 6).Value = sessionCode;
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int userId = reader.GetInt32(0);
                            bool isBlinded = !reader.IsDBNull(1) && reader.GetBoolean(1);
                            userBlindFlags[userId] = isBlinded;
                        }
                    }
                }

                // Insert prompt for each user
                foreach (var kvp in userBlindFlags)
                {
                    string insertQuery = @"
                        INSERT INTO Ratings (SessionID, Prompt, UserID, Blind)
                        VALUES (@SessionID, @Prompt, @UserID, @Blind)";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.Add("@SessionID", SqlDbType.Int).Value = SessionID;
                        cmd.Parameters.Add("@Prompt", SqlDbType.NVarChar).Value = userPrompt;
                        cmd.Parameters.Add("@UserID", SqlDbType.Int).Value = kvp.Key;
                        cmd.Parameters.Add("@Blind", SqlDbType.Int).Value = kvp.Value ? 1 : 0;
                        cmd.ExecuteNonQuery();
                    }
                }

                conn.Close();
            }

            return new JsonResult(new { success = true, prompt = userPrompt });
        }

        private int? GetSessionID(SqlConnection conn, string sessionCode)
        {
            string query = "SELECT SessionID FROM Sessions WHERE SessionCode = @SessionCode";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.Add("@SessionCode", SqlDbType.NVarChar, 6).Value = sessionCode;
                var result = cmd.ExecuteScalar();
                return result != null ? (int?)Convert.ToInt32(result) : null;
            }
        }

        public class UserInfo
        {
            public int UserID { get; set; }
            public string UserName { get; set; }
            public string UserImage { get; set; }
        }
    }
}
