var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 7177; // Ensure it redirects to the correct HTTPS port
});

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache(); // Required for session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(90); // Set session timeout
    options.Cookie.HttpOnly = true;   // Cookie can only be accessed by the server
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".AspNetCore.Session";
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();  // Ensure this is before session and authorization

app.UseSession();  // Enable session BEFORE custom middleware

app.UseAuthorization();

app.MapRazorPages();

app.Run();
