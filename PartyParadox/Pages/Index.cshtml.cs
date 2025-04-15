using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System;
using System.IO;

namespace PartyParadox.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        [BindProperty]
        public string UserName { get; set; }

        [BindProperty]
        public IFormFile ProfileImage { get; set; }

        public string ProfileImagePath { get; set; }

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public void OnGet()
        {
            // Retrieve session data when user revisits
            UserName = HttpContext.Session.GetString("UserName") ?? string.Empty;
            ProfileImagePath = HttpContext.Session.GetString("ProfileImagePath") ?? "userImgs/Default.png";
        }

        public IActionResult OnPostJoinGame()
        {
            SaveUserToDatabase();
            return RedirectToPage("JoinGame");
        }

        public IActionResult OnPostCreateGame()
        {
            SaveUserToDatabase();
            return RedirectToPage("CreateGame");
        }

        public void SaveUserToDatabase()
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                ModelState.AddModelError(string.Empty, "Username is required.");
                return;
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Insert a new user with an uploaded or default image
                string imagePath = "userImgs/Default.png"; // Default profile image
                var wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var uploadsFolder = Path.Combine(wwwRootPath, "userImgs");
                Directory.CreateDirectory(uploadsFolder);

                _logger.LogInformation($"UserID from session: {HttpContext.Session.GetInt32("ID")}");
                _logger.LogInformation($"ProfileImagePath from session: {HttpContext.Session.GetString("ProfileImagePath")}");

                if (ProfileImage != null)
                {
                    string uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(ProfileImage.FileName)}";
                    imagePath = Path.Combine("userImgs", uniqueFileName);

                    using (var fileStream = new FileStream(Path.Combine(wwwRootPath, imagePath), FileMode.Create))
                    {
                        ProfileImage.CopyTo(fileStream);
                    }
                }

                // Insert the new user
                string insertQuery = "INSERT INTO Users (Name, Image, SessionID) OUTPUT INSERTED.ID VALUES (@Name, @Image, NULL)";
                using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                {
                    insertCommand.Parameters.AddWithValue("@Name", UserName);
                    insertCommand.Parameters.AddWithValue("@Image", imagePath);
                    object newUserId = insertCommand.ExecuteScalar();

                    if (newUserId != null)
                    {
                        HttpContext.Session.SetInt32("ID", Convert.ToInt32(newUserId));
                        HttpContext.Session.SetString("ProfileImagePath", imagePath);
                    }
                }
            }

            HttpContext.Session.SetString("UserName", UserName);
        }

        public IActionResult OnPostClearSession()
        {
            string userId = HttpContext.Session.GetString("ID");

            if (!string.IsNullOrEmpty(userId))
            {
                string connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string deleteQuery = "DELETE FROM Users WHERE ID = @ID";
                    using (SqlCommand command = new SqlCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ID", userId);
                        command.ExecuteNonQuery();
                    }
                }

                // Clear session data
                HttpContext.Session.Clear();
            }
            return new EmptyResult();
        }
    }
}
