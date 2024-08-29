using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySchoolDiary.Data;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using MySchoolDiary.Models;
using Azure.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

//TODO: Implement user roles from this video https://www.youtube.com/watch?v=6sMPvucWNRE i shopped on 18:43


builder.Services.AddControllers();

if(builder.Configuration.GetSection("AppSetting:OnUbuntu").Value == "true"){
    var connectionString = builder.Configuration.GetConnectionString("UbuntuConnection");
    builder.Services.AddDbContext<AppDbContext>(
    options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
    );

} else {
    builder.Services.AddDbContext<AppDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    );
}

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });

    options.OperationFilter<SecurityRequirementsOperationFilter>();
});

var tokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    ValidateLifetime = true,
    ValidateAudience = false,
    ValidateIssuer = false,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                builder.Configuration.GetSection("AppSetting:Token").Value))
};

builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.TokenValidationParameters = tokenValidationParameters;
});

builder.Services.AddSingleton(tokenValidationParameters);

builder.Services.AddDefaultIdentity<User>(options => options.SignIn.RequireConfirmedEmail = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    //Adding roles
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    var roles = new[]
    {
        "Admin", "Teacher", "Student"
    };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    //adding admin if admin account isnt alredy exists
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    var admins = await userManager.GetUsersInRoleAsync("Admin");

    if (admins.Count == 0)
    {
        var admin = new User()
        {
            UserName = "Administator",
            Name = "Admin",
            Surname = "",
            FatherName = "",
            Form = ""
        };
        await userManager.CreateAsync(admin, "vk8q#GG{($S!");
        await userManager.AddToRoleAsync(admin, "Admin");
    }
}

app.Run();
